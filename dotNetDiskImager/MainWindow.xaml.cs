using dotNetDiskImager.Buffers;
using dotNetDiskImager.DiskAccess;
using dotNetDiskImager.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using dotNetDiskImager.UI;
using System.Media;
using System.Diagnostics;
using System.Windows.Controls.Primitives;

namespace dotNetDiskImager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Win32 API Stuff
        const int WM_DEVICECHANGE = 0x219;
        const int DBT_DEVICEARRIVAL = 0x8000;
        const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        const int DBT_DEVTYP_VOLUME = 0x02;
        const int WM_SYSTEMMENU = 0xA4;
        const int WP_SYSTEMMENU = 0x02;
        const int WM_QUERYENDSESSION = 0x0011;
        const int WM_ENDSESSION = 0x0016;
        #endregion

        const int windowHeight = 290;
        const int windowWidth = 585;
        const int infoMessageHeight = 40;
        const int infoMessageMargin = 10;
        const int progressPartHeight = 235;
        const int applicationPartHeight = 250;
        const int windowInnerOffset = 10;
        readonly Brush AcceleratorDisabledForegroundBrush = new SolidColorBrush(Color.FromRgb(0xC3, 0xC3, 0xC3));
        readonly Color ActivatedColor = Color.FromRgb(0, 122, 204);
        readonly Brush ActivatedBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        readonly Color WorkingColor = Color.FromRgb(228, 133, 67);
        readonly Brush WorkingBrush = new SolidColorBrush(Color.FromRgb(228, 133, 67));
        readonly Color DeactivatedColor = Color.FromRgb(212, 212, 212);
        readonly Brush DeactivatedBrush = new SolidColorBrush(Color.FromRgb(153, 153, 153));

        public IntPtr Handle
        {
            get
            {
                return new WindowInteropHelper(this).Handle;
            }
        }

        Disk _disk;
        Checksum _checksum;
        CircularBuffer remainingTimeEstimator = new CircularBuffer(5);
        AboutWindow aboutWindow = null;
        SettingsWindow settingsWindow = null;
        Stopwatch elapsedStopwatch = new Stopwatch();
        LastOperationInfo lastOperationInfo = new LastOperationInfo();

        public SpeedGraphModel GraphModel { get; } = new SpeedGraphModel();
        Disk disk
        {
            get
            {
                return _disk;
            }
            set
            {
                _disk = value;
                if (IsActive)
                {
                    window_Activated(null, null);
                }
            }
        }

        Checksum checksum
        {
            get
            {
                return _checksum;
            }
            set
            {
                _checksum = value;
                if (IsActive)
                {
                    window_Activated(null, null);
                }
            }
        }

        bool verifyingAfterOperation = false;
        bool windowContextMenuShown;
        bool closed = false;

        bool acceleratorsVisible = false;
        List<Label> accelerators;

        public MainWindow()
        {
            InitializeComponent();

            InitAccelerators();
            SetAcceleratorsVisibility(false);

            OxyPlot.Wpf.LineAnnotation.PlotViewProperty = speedGraph;
            Topmost = AppSettings.Settings.IsTopMost.Value;

            driveSelectComboBox.SelectionChanged += (s, e) => driveSelectComboBox.SelectedIndex = 0;

            LoadDriveSelectItems(true);

            if (driveSelectComboBox.Items.Count == 2 && AppSettings.Settings.AutoSelectSingleDevice.Value)
            {
                (((driveSelectComboBox.Items[1] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).IsChecked = true;
                DeviceCheckBoxClickHandler();
            }

            if (AppSettings.Settings.Appearance == null)
            {
                AppSettings.Settings.Appearance = Appearance.Light;
            }

            ResourceDictionary dict = new ResourceDictionary();
            if (AppSettings.Settings.Appearance == Appearance.Light)
            {
                dict.Source = new Uri("Resources/LightColor.xaml", UriKind.Relative);
            }
            else
            {
                dict.Source = new Uri("Resources/DarkColor.xaml", UriKind.Relative);
            }
            Application.Current.Resources.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);

            GraphModel.UpdateColors(AppSettings.Settings.Appearance.Value);

            if (AppSettings.Settings.LastWindowPosition != null)
            {
                if (AppSettings.Settings.LastWindowPosition.Top > SystemParameters.VirtualScreenHeight - windowHeight || AppSettings.Settings.LastWindowPosition.Top < 0)
                {
                    AppSettings.Settings.LastWindowPosition.Top = (int)((SystemParameters.VirtualScreenHeight - windowHeight) / 2);
                }

                if (AppSettings.Settings.LastWindowPosition.Left > SystemParameters.VirtualScreenWidth - windowWidth || AppSettings.Settings.LastWindowPosition.Left < 0)
                {
                    AppSettings.Settings.LastWindowPosition.Left = (int)((SystemParameters.VirtualScreenWidth - windowWidth) / 2);
                }

                Top = AppSettings.Settings.LastWindowPosition.Top;
                Left = AppSettings.Settings.LastWindowPosition.Left;
            }

            Loaded += (s, e) =>
            {
                HwndSource source = HwndSource.FromHwnd(Handle);
                source.AddHook(WndProc);

                ProcessCommandLineArguments();
            };

            Closing += (s, e) =>
            {
                if (disk != null)
                {
                    if (MessageBox.Show(this, "Exiting now will result in corruption at the target.\nDo you really want to exit application ?",
                        "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                    disk.CancelOperation();
                }

                if (checksum != null)
                {
                    if (MessageBox.Show(this, "Checksum calculation in progress.\nDo you really want to exit application ?",
                        "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                    checksum.Cancel();
                }

                closed = true;

                HwndSource source = HwndSource.FromHwnd(Handle);
                source.RemoveHook(WndProc);
                AppSettings.Settings.IsTopMost = Topmost;

                if (AppSettings.Settings.LastWindowPosition == null)
                {
                    AppSettings.Settings.LastWindowPosition = new LastWindowPosition();
                }

                AppSettings.Settings.LastWindowPosition.Left = (int)Left;
                AppSettings.Settings.LastWindowPosition.Top = (int)Top;
                AppSettings.SaveSettings();
            };

            if (AppSettings.Settings.CheckForUpdatesOnStartup)
            {
                CheckUpdates();
            }

            Application.Current.SessionEnding += (s, e) =>
            {
                if (!Utils.CanComputerShutdown)
                {
                    e.Cancel = true;
                }
            };

            windowBorderEffect.Color = ActivatedColor;
            windowBorder.BorderBrush = ActivatedBrush;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE && lParam != IntPtr.Zero)
            {
                DEV_BROADCAST_HDR lpdb = Marshal.PtrToStructure<DEV_BROADCAST_HDR>(lParam);

                switch (wParam.ToInt32())
                {
                    case DBT_DEVICEARRIVAL:
                        if (lpdb.dbch_DeviceType == DBT_DEVTYP_VOLUME)
                        {
                            DEV_BROADCAST_VOLUME dbv = Marshal.PtrToStructure<DEV_BROADCAST_VOLUME>(lParam);
                            char driveLetter = Disk.GetFirstDriveLetterFromMask(dbv.dbch_Unitmask);
                            if (driveLetter != 0)
                            {
                                if (driveSelectComboBox.Items.Count == 0)
                                {
                                    LoadDriveSelectItems(false);
                                    if (AppSettings.Settings.AutoSelectSingleDevice.Value)
                                    {
                                        (((driveSelectComboBox.Items[1] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).IsChecked = true;
                                        DeviceCheckBoxClickHandler();
                                    }
                                }
                                else
                                {
                                    InsertDriveItem(driveLetter);
                                }
                            }
                        }
                        break;
                    case DBT_DEVICEREMOVECOMPLETE:
                        if (lpdb.dbch_DeviceType == DBT_DEVTYP_VOLUME)
                        {
                            DEV_BROADCAST_VOLUME dbv = Marshal.PtrToStructure<DEV_BROADCAST_VOLUME>(lParam);
                            char driveLetter = Disk.GetFirstDriveLetterFromMask(dbv.dbch_Unitmask, false);
                            RemoveDriveItem(driveLetter);
                        }
                        break;
                }
            }

            if ((msg == WM_SYSTEMMENU) && (wParam.ToInt32() == WP_SYSTEMMENU))
            {
                ShowContextMenu(false);
                handled = true;
            }

            if (msg == WM_ENDSESSION || msg == WM_QUERYENDSESSION)
            {
                if (!Utils.CanComputerShutdown)
                {

                }
            }

            return IntPtr.Zero;
        }

        private void readButton_Click(object sender, RoutedEventArgs e)
        {
            verifyingAfterOperation = false;
            try
            {
                HandleReadButtonClick();
            }
            catch (Exception ex)
            {
                disk?.Dispose();
                disk = null;
                MessageBox.Show(this, string.Format("Read from device error. {0}", ex.Message), "Unknown error");
            }
        }

        private void writeButton_Click(object sender, RoutedEventArgs e)
        {
            verifyingAfterOperation = false;
            try
            {
                HandleWriteButtonClick();
            }
            catch (Exception ex)
            {
                disk?.Dispose();
                disk = null;
                MessageBox.Show(this, string.Format("Write to device error. {0}", ex.Message), "Unknown error");
            }
        }

        private void verifyImageButton_Click(object sender, RoutedEventArgs e)
        {
            verifyingAfterOperation = false;
            try
            {
                HandleVerifyButtonClick();
            }
            catch (Exception ex)
            {
                disk?.Dispose();
                disk = null;
                MessageBox.Show(this, string.Format("Verify image error. {0}", ex.Message), "Unknown error");
            }
        }

        private void Disk_OperationFinished(object sender, OperationFinishedEventArgs e)
        {
            try
            {
                if (e.Done)
                {
                    elapsedStopwatch.Stop();
                    verifyingAfterOperation = false;
                    lastOperationInfo.OperationFinishedArgs = e;
                    lastOperationInfo.ElapsedTime = elapsedStopwatch.Elapsed;
                    lastOperationInfo.Devices = disk.DriveLetters;

                    Dispatcher.Invoke(() =>
                    {
                        if (e.OperationState == OperationFinishedState.Success && AppSettings.Settings.AutoClose.Value)
                        {
                            disk.Dispose();
                            disk = null;
                            Close();
                            PlayNotifySound(true);
                            return;
                        }

                        lastOperationInfo.ImageFile = imagePathTextBox.Text;
                        PlayNotifySound();
                        this.FlashWindow();
                        SetUIState(true);
                        GraphModel.ResetToNormal();
                        programTaskbar.ProgressValue = 0;
                        programTaskbar.ProgressState = TaskbarItemProgressState.None;
                        programTaskbar.Overlay = null;
                        remainingTimeEstimator.Reset();
                        disk.Dispose();
                        disk = null;

                        DisplayInfoPart(true, false, e.OperationState, CreateInfoMessage(e));

                        Title = "dotNet Disk Imager";
                    });
                }
                else
                {
                    if ((e.DiskOperation & DiskOperation.Verify) > 0)
                    {
                        verifyingAfterOperation = true;
                        Dispatcher.Invoke(() =>
                        {
                            programTaskbar.Overlay = Properties.Resources.check.ToBitmapImage();
                            stepText.Content = "Verifying...";
                            GraphModel.ResetToVerify();
                            remainingTimeEstimator.Reset();
                            timeRemainingText.Content = "Remaining time: Calculating...";
                            if (AppSettings.Settings.TaskbarExtraInfo == TaskbarExtraInfo.RemainingTime)
                            {
                                Title = "[Calculating...] - dotNet Disk Imager";
                            }
                            progressText.Content = "0% Complete";
                        });
                    }
                }
            }
            catch { }
        }

        private void Disk_OperationProgressReport(object sender, OperationProgressReportEventArgs e)
        {
            GraphModel.UpdateSpeedLineValue(e.AverageBps);
            remainingTimeEstimator.Add(e.RemainingBytes / e.AverageBps);

            Dispatcher.Invoke(() =>
            {
                if (remainingTimeEstimator.IsReady)
                {
                    ulong averageSeconds = remainingTimeEstimator.Average();
                    timeRemainingText.Content = string.Format("Remaining time: {0}", Helpers.SecondsToEstimate(averageSeconds));
                    if (AppSettings.Settings.TaskbarExtraInfo == TaskbarExtraInfo.RemainingTime)
                    {
                        Title = string.Format(@"[{0}] - dotNet Disk Imager", Helpers.SecondsToEstimate(averageSeconds, true));
                    }
                }
                transferredText.Content = string.Format("Transferred: {0} of {1}", Helpers.BytesToXbytes(e.TotalBytesProcessed), Helpers.BytesToXbytes(e.TotalBytesProcessed + e.RemainingBytes));
                if (AppSettings.Settings.TaskbarExtraInfo == TaskbarExtraInfo.CurrentSpeed)
                {
                    Title = string.Format(@"[{0}/s] - dotNet Disk Imager", Helpers.BytesToXbytes(e.AverageBps));
                }
            });
        }

        private void Disk_OperationProgressChanged(object sender, OperationProgressChangedEventArgs e)
        {
            GraphModel.UpdateSpeedLineValue(e.AverageBps);
            GraphModel.AddDataPoint(e.Progress, e.AverageBps);
            Dispatcher.Invoke(() =>
            {
                if (verifyCheckBox.IsChecked.Value && e.DiskOperation != DiskOperation.Verify)
                {
                    if (verifyingAfterOperation)
                    {
                        programTaskbar.ProgressValue = ((e.Progress / 100.0) / 2.0) + 0.5;
                    }
                    else
                    {
                        programTaskbar.ProgressValue = ((e.Progress / 100.0) / 2.0);
                    }
                }
                else
                {
                    programTaskbar.ProgressValue = e.Progress / 100.0;
                }
                progressText.Content = string.Format("{0}% Complete", e.Progress);

                switch (AppSettings.Settings.TaskbarExtraInfo)
                {
                    case TaskbarExtraInfo.Percent:
                        Title = string.Format("[{0}%] - dotNet Disk Imager", (int)(programTaskbar.ProgressValue * 100));
                        break;
                    case TaskbarExtraInfo.CurrentSpeed:
                        Title = string.Format(@"[{0}/s] - dotNet Disk Imager", Helpers.BytesToXbytes(e.AverageBps));
                        break;
                }
            });

        }

        private void fileSelectDialogButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = false;
            OpenFileDialog dlg = new OpenFileDialog()
            {
                CheckFileExists = false,
                Title = "Select a disk image file",
                Filter = "Supported Disk image files|*.zip;*.img;*.eimg|Zipped Disk image file (*.zip)|*.zip|Disk image file (*.img)|*.img|Encrypted Disk image file (*.eimg)|*.img|Any file|*.*",
                InitialDirectory = AppSettings.Settings.DefaultFolder == DefaultFolder.LastUsed ? AppSettings.Settings.LastFolderPath : AppSettings.Settings.UserSpecifiedFolder
            };

            foreach (var customPlace in AppSettings.Settings.CustomPlaces)
            {
                try
                {
                    if (Directory.Exists(customPlace))
                    {
                        dlg.CustomPlaces.Add(new FileDialogCustomPlace(customPlace));
                    }
                }
                catch { }
            }

            try
            {
                result = dlg.ShowDialog().Value;
            }
            catch
            {
                dlg.InitialDirectory = "";
                result = dlg.ShowDialog().Value;
            }

            if (result)
            {
                AppSettings.Settings.LastFolderPath = new FileInfo(dlg.FileName).DirectoryName;
                imagePathTextBox.Text = dlg.FileName;
                if (new FileInfo(dlg.FileName).Extension == ".zip")
                {
                    onTheFlyZipCheckBox.IsChecked = true;
                }
                else
                {
                    onTheFlyZipCheckBox.IsChecked = false;
                }
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {            
            if (disk != null)
            {
                if (MessageBox.Show(this, "Canceling current operation will result in corruption at the target.\nDo you really want to cancel current operation ?",
                    "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    disk?.CancelOperation();
                }
            }

            if (checksum != null)
            {
                if (MessageBox.Show(this, "Do you really want to cancel checksum calculation ?",
                    "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    checksum?.Cancel();
                }
            }
        }

        private async void wipeDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await HandleWipeButtonClick();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format("Wipe device error. {0}", ex.Message), "Unknown error");
            }

            disk?.Dispose();
            disk = null;
            SetUIState(true, false);
            Mouse.OverrideCursor = null;
        }

        private void calculateChecksumButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HandleCalculateChecksum();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format("Calculate checksum error. {0}", ex.Message), "Unknown error");
            }
        }

        private void closeInfoButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayInfoPart(false);
        }

        private bool IsAnyAcceleratorControlFocused()
        {
            return fileSelectDialogButton.IsFocused || driveSelectComboBox.IsFocused || checksumTextBox.IsFocused || checksumComboBox.IsFocused || calculateChecksumButton.IsFocused ||
                readOnlyAllocatedCheckBox.IsFocused || verifyCheckBox.IsFocused || onTheFlyZipCheckBox.IsFocused || readButton.IsFocused || writeButton.IsFocused ||
                verifyImageButton.IsFocused || wipeDeviceButton.IsFocused || cancelButton.IsFocused || encryptDecryptCheckBox.IsFocused;
        }

        private void window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
            {
                Close();
            }

            if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ShowAboutWindow();
                e.Handled = true;
            }

            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ShowSettingsWindow();
                e.Handled = true;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (e.SystemKey == Key.Space)
                {
                    ShowContextMenu(false, true);
                    e.Handled = true;
                }
            }

            if (e.Key == Key.System && Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.LeftAlt)
            {
                if (!acceleratorsVisible)
                {
                    acceleratorsVisible = true;
                    SetAcceleratorsVisibility(true);
                }
                else
                {
                    acceleratorsVisible = false;
                    SetAcceleratorsVisibility(false);
                }
                e.Handled = true;
            }
            else
            {
                if (acceleratorsVisible || IsAnyAcceleratorControlFocused())
                {
                    bool handled = true;
                    acceleratorsVisible = false;
                    SetAcceleratorsVisibility(false);

                    Key key = e.Key;

                    if (e.Key == Key.System || e.SystemKey != Key.None)
                    {
                        key = e.SystemKey;
                    }

                    switch (key)
                    {
                        case Key.R:
                            if (disk == null && checksum == null)
                            {
                                readButton_Click(null, null);
                            }
                            break;
                        case Key.W:
                            if (disk == null && checksum == null)
                            {
                                writeButton_Click(null, null);
                            }
                            break;
                        case Key.V:
                            if (disk == null && checksum == null)
                            {
                                verifyImageButton_Click(null, null);
                            }
                            break;
                        case Key.P:
                            if (disk == null && checksum == null)
                            {
                                wipeDeviceButton_Click(null, null);
                            }
                            break;
                        case Key.C:
                            cancelButton_Click(null, null);
                            break;
                        case Key.O:
                            if (disk == null && checksum == null)
                            {
                                fileSelectDialogButton_Click(null, null);
                            }
                            break;
                        case Key.A:
                            if (disk == null && checksum == null)
                            {
                                readOnlyAllocatedCheckBox.IsChecked = !readOnlyAllocatedCheckBox.IsChecked.Value;
                            }
                            break;
                        case Key.Z:
                            if (disk == null && checksum == null)
                            {
                                onTheFlyZipCheckBox.IsChecked = !onTheFlyZipCheckBox.IsChecked.Value;
                            }
                            break;
                        case Key.F:
                            if (disk == null && checksum == null)
                            {
                                verifyCheckBox.IsChecked = !verifyCheckBox.IsChecked.Value;
                            }
                            break;
                        case Key.E:
                            if (disk == null && checksum == null)
                            {
                                encryptDecryptCheckBox.IsChecked = !encryptDecryptCheckBox.IsChecked.Value;
                            }
                            break;
                        case Key.H:
                            if (disk == null && checksum == null)
                            {
                                calculateChecksumButton_Click(null, null);
                            }
                            break;
                        case Key.D:
                            if (disk == null && checksum == null)
                            {
                                driveSelectComboBox.IsDropDownOpen = true;
                                driveSelectComboBox.Focus();
                            }
                            break;
                        case Key.G:
                            if (disk == null && checksum == null)
                            {
                                HandleRefreshButton();
                            }
                            break;
                        case Key.I:
                            if (disk == null && checksum == null)
                            {
                                imagePathTextBox.Focus();
                                imagePathTextBox.CaretIndex = imagePathTextBox.Text.Length;
                            }
                            break;
                        case Key.U:
                            if (disk == null && checksum == null)
                            {
                                checksumTextBox.Focus();
                            }
                            break;
                        case Key.M:
                            if (disk == null && checksum == null)
                            {
                                checksumComboBox.IsDropDownOpen = true;
                                checksumComboBox.Focus();
                            }
                            break;
                        default:
                            handled = false;
                            break;
                    }

                    e.Handled = handled;
                }
            }
        }

        private void program_Drop(object sender, DragEventArgs e)
        {
            if (disk != null)
                return;

            HandleDrop(e);
        }

        private void imagePathTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void program_DragEnter(object sender, DragEventArgs e)
        {
            if (disk != null)
                return;

            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    HideShowWindowOverlay(true);
                    e.Handled = true;
                }
            }
            catch { }
        }

        private void program_DragLeave(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                HideShowWindowOverlay(false);
                e.Handled = true;
            }
        }

        private void checksumTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            checksumTextBox.SelectAll();
        }

        private void imagePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(imagePathTextBox.Text))
                {
                    var fileInfo = new FileInfo(imagePathTextBox.Text);
                    if (fileInfo.Extension == ".zip")
                    {
                        onTheFlyZipCheckBox.IsChecked = true;

                        if (Disk.CheckZipFileEncryption(imagePathTextBox.Text))
                        {
                            encryptDecryptCheckBox.IsChecked = true;
                        }
                        else
                        {
                            encryptDecryptCheckBox.IsChecked = false;
                        }
                    }
                    else
                    {
                        onTheFlyZipCheckBox.IsChecked = false;

                        if (Disk.CheckRawFileEncryption(imagePathTextBox.Text))
                        {
                            encryptDecryptCheckBox.IsChecked = true;
                        }
                        else
                        {
                            encryptDecryptCheckBox.IsChecked = false;
                        }
                    }
                }
            }
            catch { }
        }

        private void HandleCalculateChecksum()
        {
            if (string.IsNullOrEmpty(imagePathTextBox.Text))
            {
                MessageBox.Show("Cannot calculate checksum. No file selected.\nPlease select file first", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!new FileInfo(imagePathTextBox.Text).Exists)
            {
                MessageBox.Show("Cannot calculate checksum. Selected file doesn't exist.\nPlease select valid file first", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (new FileInfo(imagePathTextBox.Text).Length == 0)
            {
                MessageBox.Show("Cannot calculate checksum. Selected file is empty.\nPlease select valid file first", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            checksum = new Checksum();

            checksum.ChecksumProgressChanged += (s, ea) =>
            {
                Dispatcher.Invoke(() =>
                {
                    checksumProgressBar.Value = ea.Progress;
                    programTaskbar.ProgressValue = ea.Progress / 100.0;
                });
            };

            checksum.ChecksumDone += (s, ea) =>
            {
                Dispatcher.Invoke(() =>
                {
                    checksumProgressBar.Visibility = Visibility.Collapsed;
                    checksumTextBox.Text = ea.Checksum;
                    SetUIState(true, false);
                    checksum.Dispose();
                    checksum = null;
                    programTaskbar.ProgressState = TaskbarItemProgressState.None;
                });
            };

            ChecksumType checksumType = ChecksumType.MD5;

            switch (checksumComboBox.SelectedIndex)
            {
                case 0:
                    checksumType = ChecksumType.MD5;
                    break;
                case 1:
                    checksumType = ChecksumType.SHA1;
                    break;
                case 2:
                    checksumType = ChecksumType.SHA256;
                    break;
            }

            if (!checksum.BeginChecksumCalculation(imagePathTextBox.Text, checksumType))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);

                MessageBox.Show(this, string.Format("Cannot calculate checksum. Unable to read from file {0}\nFile is probably in use by another application.", fileInfo.Name), "Unable to open file", MessageBoxButton.OK, MessageBoxImage.Error);
                checksum.Dispose();
                checksum = null;
                return;
            }
            checksumProgressBar.Value = 0;
            checksumProgressBar.Visibility = Visibility.Visible;
            programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
            DisplayInfoPart(false);
            SetUIState(false, false);
        }

        private void DisplayInfoPart(bool display, bool noAnimation = false, OperationFinishedState state = OperationFinishedState.Error, string message = "")
        {
            if (display)
            {
                switch (state)
                {
                    case OperationFinishedState.Success:
                        infoContainer.Style = FindResource("InfoContainerSuccess") as Style;
                        infoText.Content = message;
                        infoText.Foreground = new SolidColorBrush(Color.FromRgb(0, 70, 0));
                        infoSymbol.Content = FindResource("checkIcon");
                        break;
                    case OperationFinishedState.Canceled:
                        infoContainer.Style = FindResource("InfoContainerWarning") as Style;
                        infoText.Content = message;
                        infoText.Foreground = new SolidColorBrush(Color.FromRgb(116, 86, 25));
                        infoSymbol.Content = FindResource("warningIcon");
                        break;
                    case OperationFinishedState.Error:
                        infoContainer.Style = FindResource("InfoContainerError") as Style;
                        infoText.Content = message;
                        infoText.Foreground = new SolidColorBrush(Color.FromRgb(128, 5, 5));
                        infoSymbol.Content = FindResource("warningIcon");
                        break;
                }

                DoubleAnimation windowAnimation = new DoubleAnimation(windowHeight + infoMessageHeight, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 250 : 0));
                DoubleAnimation containerAnimation = new DoubleAnimation(infoMessageHeight - infoMessageMargin, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 250 : 0));
                windowAnimation.Completed += (s, e) =>
                {
                    closeInfoButton.Visibility = Visibility.Visible;
                    showInfoButton.Visibility = Visibility.Visible;
                };
                BeginAnimation(HeightProperty, windowAnimation);
                infoContainer.BeginAnimation(HeightProperty, containerAnimation);
                infoContainer.Visibility = Visibility.Visible;
            }
            else
            {
                if (noAnimation)
                {
                    Height = windowHeight;
                    infoContainer.Visibility = Visibility.Collapsed;
                    closeInfoButton.Visibility = Visibility.Collapsed;
                    showInfoButton.Visibility = Visibility.Collapsed;
                    return;
                }
                else
                {
                    DoubleAnimation windowAnimation = new DoubleAnimation(windowHeight, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 250 : 0));
                    DoubleAnimation containerAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 250 : 0));
                    windowAnimation.Completed += (s, e) =>
                    {
                        infoContainer.Visibility = Visibility.Collapsed;
                    };
                    closeInfoButton.Visibility = Visibility.Collapsed;
                    showInfoButton.Visibility = Visibility.Collapsed;
                    BeginAnimation(HeightProperty, windowAnimation);
                    infoContainer.BeginAnimation(HeightProperty, containerAnimation);
                }
            }
        }

        void HandleReadButtonClick()
        {
            InitOperationResult result = null;

            try
            {
                if (GetSelectedDevices().Length > 1)
                {
                    throw new ArgumentException("Cannot read from multiple devices at once.\nPlease select only one device");
                }
                ValidateInputs();
            }
            catch (ArgumentException ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Cannot read from device. {0}", ex.Message), "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(imagePathTextBox.Text))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);
                if (fileInfo.Length > 0)
                {
                    DisplayInfoPart(false);
                    if (MessageBox.Show(this, string.Format("File {0} already exists and it's size is {1}.\nWould you like to overwrite it ?", fileInfo.Name, Helpers.BytesToXbytes((ulong)fileInfo.Length)),
                        "File already exists", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            try
            {
                if (onTheFlyZipCheckBox.IsChecked.Value)
                {
                    disk = new DiskZip(GetSelectedDevices());
                }
                else
                {
                    disk = new DiskRaw(GetSelectedDevices());
                }

                if (encryptDecryptCheckBox.IsChecked.Value)
                {
                    var passwordWindow = new PasswordWindow("", onTheFlyZipCheckBox.IsChecked.Value);

                    if (!passwordWindow.ShowDialogAndVerify(this))
                    {
                        disk?.Dispose();
                        disk = null;
                        return;
                    }

                    disk.EnableEncryption(passwordWindow.Password);
                }

                result = disk.InitReadImageFromDevice(imagePathTextBox.Text, readOnlyAllocatedCheckBox.IsChecked.Value);
            }
            catch (Exception ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Unable to init read from device.\n{0}", ex.Message), "Error occured", MessageBoxButton.OK, MessageBoxImage.Error);
                disk?.Dispose();
                disk = null;
                return;
            }

            if (result.Result)
            {
                DisplayInfoPart(false, true);
                disk.OperationProgressChanged += Disk_OperationProgressChanged;
                disk.OperationProgressReport += Disk_OperationProgressReport;
                disk.OperationFinished += Disk_OperationFinished;

                timeRemainingText.Content = "Remaining time: Calculating...";
                transferredText.Content = string.Format("Transferred: 0 B of {0}", Helpers.BytesToXbytes(result.RequiredSpace));
                progressText.Content = "0% Complete";
                stepText.Content = "Reading...";

                elapsedStopwatch.Restart();
                disk.BeginReadImageFromDevice(verifyCheckBox.IsChecked.Value);
                SetUIState(false);
                programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                programTaskbar.Overlay = Properties.Resources.read.ToBitmapImage();

                switch (AppSettings.Settings.TaskbarExtraInfo)
                {
                    case TaskbarExtraInfo.ActiveDevice:
                        Title = string.Format(@"[{0}:\] - dotNet Disk Imager", disk.DriveLetters[0]);
                        break;
                    case TaskbarExtraInfo.ImageFileName:
                        Title = string.Format(@"[{0}] - dotNet Disk Imager", new FileInfo(imagePathTextBox.Text).Name);
                        break;
                    case TaskbarExtraInfo.RemainingTime:
                        Title = "[Calculating...] - dotNet Disk Imager";
                        break;
                }
            }
            else
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("There is not enough free space on target device [{0}:\\].\nFree space availible {1}\nFree space required {2}",
                    imagePathTextBox.Text[0], Helpers.BytesToXbytes(result.AvailibleSpace), Helpers.BytesToXbytes(result.RequiredSpace)),
                    "Not enough free space", MessageBoxButton.OK, MessageBoxImage.Warning
                    );
                disk.Dispose();
                disk = null;
            }
        }

        void HandleWriteButtonClick()
        {
            InitOperationResult result = null;

            try
            {
                ValidateInputs();
            }
            catch (ArgumentException ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Cannot write to device. {0}", ex.Message), "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(imagePathTextBox.Text))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);
                if (fileInfo.Length == 0)
                {
                    DisplayInfoPart(false);
                    MessageBox.Show(this, string.Format("Cannot write to device. File {0} exists but has no size. Aborting.", fileInfo.Name),
                        "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Cannot write to device. File {0} does not exist. Aborting.", imagePathTextBox.Text.Split('\\', '/').Last()),
                    "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var driveLetter in GetSelectedDevices())
            {
                if (Disk.IsDriveReadOnly(string.Format(@"{0}:\", driveLetter)))
                {
                    DisplayInfoPart(false);
                    MessageBox.Show(this, string.Format(@"Cannot write to device. Device [{0}:\ - {1}] is read only. Aborting.", driveLetter, Disk.GetModelFromDrive(driveLetter)),
                        "Read only device", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                if (onTheFlyZipCheckBox.IsChecked.Value)
                {
                    disk = new DiskZip(GetSelectedDevices());
                }
                else
                {
                    disk = new DiskRaw(GetSelectedDevices());
                }

                if (encryptDecryptCheckBox.IsChecked.Value)
                {
                    var passwordWindow = new PasswordWindow(imagePathTextBox.Text, onTheFlyZipCheckBox.IsChecked.Value);

                    if (!passwordWindow.ShowDialogAndVerify(this))
                    {
                        disk?.Dispose();
                        disk = null;
                        return;
                    }

                    disk.EnableEncryption(passwordWindow.Password);
                }

                result = disk.InitWriteImageToDevice(imagePathTextBox.Text);
            }
            catch (Exception ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Unable to init write to device.\n{0}", ex.Message), "Error occured", MessageBoxButton.OK, MessageBoxImage.Error);
                disk?.Dispose();
                disk = null;
                return;
            }

            if (result.Result)
            {
                DisplayInfoPart(false);
                if (AppSettings.Settings.DisplayWriteWarnings)
                {
                    if (MessageBox.Show(this, string.Format("Writing to the {0}\ncan corrupt the device(s).\nMake sure you have selected correct device(s) and you know what you are doing.\nWe are not responsible for any damage done.\nAre you sure you want to continue ?", Helpers.GetDevicesListWithModel(GetSelectedDevices())),
                        "Confirm write", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        disk?.Dispose();
                        disk = null;
                        return;
                    }
                }
                disk.OperationProgressChanged += Disk_OperationProgressChanged;
                disk.OperationProgressReport += Disk_OperationProgressReport;
                disk.OperationFinished += Disk_OperationFinished;

                timeRemainingText.Content = "Remaining time: Calculating...";
                transferredText.Content = string.Format("Transferred: 0 B of {0}", Helpers.BytesToXbytes(result.RequiredSpace));
                progressText.Content = "0% Complete";
                stepText.Content = "Writing...";

                elapsedStopwatch.Restart();
                disk.BeginWriteImageToDevice(verifyCheckBox.IsChecked.Value);
                SetUIState(false);
                programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                programTaskbar.Overlay = Properties.Resources.write.ToBitmapImage();

                switch (AppSettings.Settings.TaskbarExtraInfo)
                {
                    case TaskbarExtraInfo.ActiveDevice:
                        Title = string.Format(@"{0} - dotNet Disk Imager", Helpers.GetDevicesListShort(disk.DriveLetters));
                        break;
                    case TaskbarExtraInfo.ImageFileName:
                        Title = string.Format(@"[{0}] - dotNet Disk Imager", new FileInfo(imagePathTextBox.Text).Name);
                        break;
                    case TaskbarExtraInfo.RemainingTime:
                        Title = "[Calculating...] - dotNet Disk Imager";
                        break;
                }
            }
            else
            {
                DisplayInfoPart(false);
                if (MessageBox.Show(this, string.Format("Target device [{0}:\\] hasn't got enough capacity.\nSpace availible {1}\nSpace required {2}\n" +
                    "The extra space ({3}) {4} appear to contain any data.\nWould you like to write data up to device size ?", result.AffectedDevice,
                    Helpers.BytesToXbytes(result.AvailibleSpace), Helpers.BytesToXbytes(result.RequiredSpace), Helpers.BytesToXbytes(result.RequiredSpace - result.AvailibleSpace), result.DataFound ? "DOES" : "does not"),
                    "Not enough capacity", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (AppSettings.Settings.DisplayWriteWarnings)
                    {
                        if (MessageBox.Show(this, string.Format("Writing to the {0}\n can corrupt the device(s).\nMake sure you have selected correct device(s) and you know what you are doing.\nWe are not responsible for any damage done.\nAre you sure you want to continue ?", Helpers.GetDevicesListWithModel(GetSelectedDevices())),
                            "Confirm write", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        {
                            disk?.Dispose();
                            disk = null;
                            return;
                        }
                    }
                    disk.OperationProgressChanged += Disk_OperationProgressChanged;
                    disk.OperationProgressReport += Disk_OperationProgressReport;
                    disk.OperationFinished += Disk_OperationFinished;

                    timeRemainingText.Content = "Remaining time: Calculating...";
                    transferredText.Content = string.Format("Transferred: 0 B of {0}", Helpers.BytesToXbytes(result.RequiredSpace));
                    progressText.Content = "0% Complete";
                    stepText.Content = "Writing...";

                    elapsedStopwatch.Restart();
                    disk.BeginWriteImageToDevice(verifyCheckBox.IsChecked.Value, true);
                    SetUIState(false);
                    programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                    programTaskbar.Overlay = Properties.Resources.write.ToBitmapImage();

                    switch (AppSettings.Settings.TaskbarExtraInfo)
                    {
                        case TaskbarExtraInfo.ActiveDevice:
                            Title = string.Format(@"{0} - dotNet Disk Imager", Helpers.GetDevicesListShort(disk.DriveLetters));
                            break;
                        case TaskbarExtraInfo.ImageFileName:
                            Title = string.Format(@"[{0}] - dotNet Disk Imager", new FileInfo(imagePathTextBox.Text).Name);
                            break;
                        case TaskbarExtraInfo.RemainingTime:
                            Title = "[Calculating...] - dotNet Disk Imager";
                            break;
                    }
                }
                else
                {
                    disk.Dispose();
                    disk = null;
                }
            }
        }

        void HandleVerifyButtonClick()
        {
            VerifyInitOperationResult result = null;

            try
            {
                ValidateInputs();
            }
            catch (ArgumentException ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Cannot verify. {0}", ex.Message), "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(imagePathTextBox.Text))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);
                if (fileInfo.Length == 0)
                {
                    DisplayInfoPart(false);
                    MessageBox.Show(this, string.Format("Nothing to verify. File {0} exists but has no size. Aborting.", fileInfo.Name)
                        , "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Nothing to verify. File {0} does not exist. Aborting.", imagePathTextBox.Text.Split('\\', '/').Last())
                        , "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (onTheFlyZipCheckBox.IsChecked.Value)
                {
                    disk = new DiskZip(GetSelectedDevices());
                }
                else
                {
                    disk = new DiskRaw(GetSelectedDevices());
                }

                if (encryptDecryptCheckBox.IsChecked.Value)
                {
                    var passwordWindow = new PasswordWindow(imagePathTextBox.Text, onTheFlyZipCheckBox.IsChecked.Value);

                    if (!passwordWindow.ShowDialogAndVerify(this))
                    {
                        disk?.Dispose();
                        disk = null;
                        return;
                    }

                    disk.EnableEncryption(passwordWindow.Password);
                }

                result = disk.InitVerifyImageAndDevice(imagePathTextBox.Text, readOnlyAllocatedCheckBox.IsChecked.Value);
            }
            catch (Exception ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(this, string.Format("Unable to init verify.\n{0}", ex.Message), "Error occured", MessageBoxButton.OK, MessageBoxImage.Error);
                disk?.Dispose();
                disk = null;
                return;
            }

            if (result.Result)
            {
                DisplayInfoPart(false, true);
                disk.OperationProgressChanged += Disk_OperationProgressChanged;
                disk.OperationProgressReport += Disk_OperationProgressReport;
                disk.OperationFinished += Disk_OperationFinished;

                timeRemainingText.Content = "Remaining time: Calculating...";
                transferredText.Content = string.Format("Transferred: 0 B of {0}", Helpers.BytesToXbytes(result.ImageSize));
                progressText.Content = "0% Complete";
                stepText.Content = "Verifying...";

                elapsedStopwatch.Restart();
                disk.BeginVerifyImageAndDevice(result.ImageSize);
                SetUIState(false);
                GraphModel.ResetToVerify();
                programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                programTaskbar.Overlay = Properties.Resources.check.ToBitmapImage();

                switch (AppSettings.Settings.TaskbarExtraInfo)
                {
                    case TaskbarExtraInfo.ActiveDevice:
                        Title = string.Format(@"{0} - dotNet Disk Imager", Helpers.GetDevicesListShort(disk.DriveLetters));
                        break;
                    case TaskbarExtraInfo.ImageFileName:
                        Title = string.Format(@"[{0}] - dotNet Disk Imager", new FileInfo(imagePathTextBox.Text).Name);
                        break;
                    case TaskbarExtraInfo.RemainingTime:
                        Title = "[Calculating...] - dotNet Disk Imager";
                        break;
                }
            }
            else
            {
                DisplayInfoPart(false);
                if (MessageBox.Show(this, string.Format("Image and device size does not match.\nImage size: {0}\nDevice size: {1}\nWould you like to verify data up to {2} size?",
                    string.Format("{0}{1}", Helpers.BytesToXbytes(result.ImageSize), result.ImageSize > result.DeviceSize ? string.Format(" - larger by {0}", Helpers.BytesToXbytes(result.ImageSize - result.DeviceSize)) : ""),
                    string.Format("{0}{1}", Helpers.BytesToXbytes(result.DeviceSize), result.DeviceSize > result.ImageSize ? string.Format(" - larger by {0}", Helpers.BytesToXbytes(result.DeviceSize - result.ImageSize)) : ""),
                    (result.DeviceSize > result.ImageSize ? "image" : "device")),
                    "Size does not match", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ulong bytesToRead = result.DeviceSize > result.ImageSize ? result.ImageSize : result.DeviceSize;
                    disk.OperationProgressChanged += Disk_OperationProgressChanged;
                    disk.OperationProgressReport += Disk_OperationProgressReport;
                    disk.OperationFinished += Disk_OperationFinished;

                    timeRemainingText.Content = "Remaining time: Calculating...";
                    transferredText.Content = string.Format("Transferred: 0 B of {0}", Helpers.BytesToXbytes(bytesToRead));
                    progressText.Content = "0% Complete";
                    stepText.Content = "Verifying...";

                    elapsedStopwatch.Restart();
                    disk.BeginVerifyImageAndDevice(bytesToRead);
                    SetUIState(false);
                    GraphModel.ResetToVerify();
                    programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                    programTaskbar.Overlay = Properties.Resources.check.ToBitmapImage();

                    switch (AppSettings.Settings.TaskbarExtraInfo)
                    {
                        case TaskbarExtraInfo.ActiveDevice:
                            Title = string.Format(@"{0} - dotNet Disk Imager", Helpers.GetDevicesListShort(disk.DriveLetters));
                            break;
                        case TaskbarExtraInfo.ImageFileName:
                            Title = string.Format(@"[{0}] - dotNet Disk Imager", new FileInfo(imagePathTextBox.Text).Name);
                            break;
                        case TaskbarExtraInfo.RemainingTime:
                            Title = "[Calculating...] - dotNet Disk Imager";
                            break;
                    }
                }
                else
                {
                    disk.Dispose();
                    disk = null;
                }
            }
        }

        private void SetUIState(bool enabled, bool? displayProgressPart = null)
        {
            if (displayProgressPart == null)
            {
                DisplayProgressPart(!enabled);
            }
            else
            {
                DisplayProgressPart(displayProgressPart.Value);
            }

            readButton.IsEnabled = enabled;
            writeButton.IsEnabled = enabled;
            verifyImageButton.IsEnabled = enabled;
            wipeDeviceButton.IsEnabled = enabled;
            onTheFlyZipCheckBox.IsEnabled = enabled;
            imagePathTextBox.IsEnabled = enabled;
            driveSelectComboBox.IsEnabled = enabled;
            verifyCheckBox.IsEnabled = enabled;
            readOnlyAllocatedCheckBox.IsEnabled = enabled;
            fileSelectDialogButton.IsEnabled = enabled;
            calculateChecksumButton.IsEnabled = enabled;
            checksumComboBox.IsEnabled = enabled;
            encryptDecryptCheckBox.IsEnabled = enabled;
            refreshDevicesButton.IsEnabled = enabled;

            foreach (var accelerator in accelerators)
            {
                if (accelerator != acceleratorLabel_cancel)
                {
                    accelerator.Foreground = enabled ? Brushes.White : AcceleratorDisabledForegroundBrush;
                    accelerator.Opacity = enabled ? 1 : 0.8;
                }
            }

            if (!enabled)
            {
                windowBorderEffect.Color = WorkingColor;
                windowBorder.BorderBrush = WorkingBrush;
            }
            else
            {
                windowBorderEffect.Color = ActivatedColor;
                windowBorder.BorderBrush = ActivatedBrush;
            }
        }

        private void DisplayProgressPart(bool display)
        {
            if (display)
            {
                DoubleAnimation windowAnimation = new DoubleAnimation(windowHeight + progressPartHeight - windowInnerOffset, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 500 : 0));
                BeginAnimation(HeightProperty, windowAnimation);
                progressPartGrid.Visibility = Visibility.Visible;
                progressPartRow.Height = new GridLength(progressPartHeight, GridUnitType.Pixel);
                applicationPartRow.Height = new GridLength(applicationPartHeight, GridUnitType.Pixel);
            }
            else
            {
                DoubleAnimation windowAnimation = new DoubleAnimation(windowHeight, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 500 : 0));
                BeginAnimation(HeightProperty, windowAnimation);
                progressPartGrid.Visibility = Visibility.Collapsed;
                progressPartRow.Height = new GridLength(0, GridUnitType.Pixel);
                applicationPartRow.Height = new GridLength(applicationPartHeight + windowInnerOffset, GridUnitType.Pixel);
            }
        }

        private void ValidateInputs()
        {
            if (string.IsNullOrEmpty(imagePathTextBox.Text))
                throw new ArgumentException("Image file was not selected.");
            if (driveSelectComboBox.Items.Count == 0)
                throw new ArgumentException("No supported device found.");
            if (GetSelectedDevices().Length == 0)
                throw new ArgumentException("No device selected.\nPlease select at least one device.");
            foreach (var driveLetter in GetSelectedDevices())
            {
                if (imagePathTextBox.Text[0] == driveLetter)
                {
                    throw new ArgumentException("Image file cannot be located on the device.");
                }
            }
        }

        private static string CreateInfoMessage(OperationFinishedEventArgs e)
        {
            string message;
            if ((e.DiskOperation & DiskOperation.Read) > 0)
            {
                message = "Reading";
                if ((e.DiskOperation & DiskOperation.Verify) > 0)
                {
                    message += " and verify";
                }
            }
            else if ((e.DiskOperation & DiskOperation.Write) > 0)
            {
                message = "Writing";
                if ((e.DiskOperation & DiskOperation.Verify) > 0)
                {
                    message += " and verify";
                }
            }
            else
            {
                message = "Verifying";
            }

            switch (e.OperationState)
            {
                case OperationFinishedState.Success:
                    message += " was finished successfully";
                    break;
                case OperationFinishedState.Canceled:
                    message += " was canceled";
                    break;
                case OperationFinishedState.Error:
                    message += " was unsuccessful";
                    break;
            }

            return message;
        }

        private void HandleDrop(DragEventArgs e)
        {
            if (disk != null)
                return;
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                    if (files != null && files.Length > 0)
                    {
                        var file = files[0];
                        if (file.Length <= 3)
                        {
                            if (file[1] == ':' && file[2] == '\\')
                            {
                                for (int i = 1; i < driveSelectComboBox.Items.Count; i++)
                                {
                                    var item = driveSelectComboBox.Items[i];
                                    var device = (((item as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem);
                                    if (device.DriveLetter == file[0])
                                    {
                                        device.IsChecked = true;
                                        DeviceCheckBoxClickHandler();
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            imagePathTextBox.Text = file;
                        }
                    }
                }
            }
            catch { }
            HideShowWindowOverlay(false);
            Activate();
        }

        public void HideShowWindowOverlay(bool show)
        {
            if (show)
            {
                windowOverlay.Visibility = Visibility.Visible;
                DoubleAnimation opacityAnim = new DoubleAnimation(0, 0.7, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 250 : 0));
                windowOverlay.BeginAnimation(OpacityProperty, opacityAnim);
            }
            else
            {
                windowOverlay.Opacity = 0;
                windowOverlay.Visibility = Visibility.Collapsed;
            }
        }

        void CheckUpdates(bool displayNoUpdatesAvailible = false)
        {
#if DEBUG
            if (displayNoUpdatesAvailible)
            {
                MessageBox.Show(this, "Debug build doesn't support updates.", "Updates not availible", MessageBoxButton.OK, MessageBoxImage.Information);
            }
#else
            new Task(() =>
            {
                var result = Updater.IsUpdateAvailible();
                if (!string.IsNullOrEmpty(result))
                {
                    if (!closed)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            notificationButton.Visibility = Visibility.Visible;
                            newVersionRun.Text = result;
                        });
                    }
                }
                else
                {
                    if (displayNoUpdatesAvailible)
                    {
                        if (result == null)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(this, "Unable to query update server.\nPlease try again later.", "Unable to query update server", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(this, "You are using the latest version", "No Update Availible", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                    }
                }
            }).Start();
#endif
        }

        private void ShowSettingsWindow()
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow(this);
                settingsWindow.Closed += (s, e) =>
                {
                    settingsWindow = null;
                    Activate();
                };
                settingsWindow.AppearanceChanged += (a) =>
                {
                    GraphModel.UpdateColors(a);
                };
            }
            settingsWindow.Show();
            settingsWindow.Activate();
        }

        private void ShowAboutWindow()
        {
            if (aboutWindow == null)
            {
                aboutWindow = new AboutWindow(this);
                aboutWindow.Closed += (s, e) =>
                {
                    aboutWindow = null;
                    Activate();
                };
            }
            aboutWindow.Show();
            aboutWindow.Activate();
        }

        private void PlayNotifySound(bool sync = false)
        {
            if (AppSettings.Settings.EnableSoundNotify.Value)
            {
                using (Stream str = Properties.Resources.notify)
                using (SoundPlayer snd = new SoundPlayer(str))
                {
                    if (sync)
                    {
                        snd.PlaySync();
                    }
                    else
                    {
                        snd.Play();
                    }
                }
            }
        }

        private void LoadDriveSelectItems(bool getImmediate)
        {
            var drives = Disk.GetLogicalDrives();
            driveSelectComboBox.Items.Clear();

            if (drives.Length > 0)
            {
                driveSelectComboBox.Items.Add(new ComboBoxItem() { Visibility = Visibility.Collapsed });

                foreach (var drive in drives)
                {
                    var deviceCheckBox = new CheckBoxDeviceItem(drive, getImmediate)
                    {
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Height = 20,
                        Width = 510,
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    if (AppSettings.Settings.OmitDrivesOverSize.Value)
                    {
                        if (deviceCheckBox.DeviceLength <= AppSettings.Settings.OmitDrivesThreshold.Value * 1073741824) // conversion from GB to B
                        {
                            if (!getImmediate && drives.Length == 1)
                            {
                                deviceCheckBox.ModelAcquired += (s, e) =>
                                {
                                    DeviceCheckBoxClickHandler();
                                };
                            }

                            deviceCheckBox.Click += DeviceCheckBox_Click;

                            var deviceInfoButton = new DeviceButton(drive)
                            {
                                BorderThickness = new Thickness(0),
                                Width = 20,
                                Height = 20,
                                Content = FindResource("infoIcon") as Viewbox,
                                ToolTip = "Displays device info"
                            };

                            deviceInfoButton.Click += DeviceInfoButton_Click;

                            var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
                            stackPanel.Children.Add(deviceCheckBox);
                            stackPanel.Children.Add(deviceInfoButton);

                            var comboBoxItem = new ComboBoxItem()
                            {
                                Padding = new Thickness(0),
                                Content = stackPanel
                            };

                            comboBoxItem.PreviewKeyDown += (s, e) =>
                            {
                                if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None)
                                {
                                    deviceCheckBox.IsChecked = !deviceCheckBox.IsChecked;
                                    DeviceCheckBoxClickHandler();
                                }
                            };

                            driveSelectComboBox.Items.Add(comboBoxItem);
                        }
                    }
                }

                driveSelectComboBox.SelectedIndex = 0;
            }

            if (driveSelectComboBox.Items.Count == 1)
            {
                driveSelectComboBox.Items.Clear();
            }
        }

        private void InsertDriveItem(char driveLetter)
        {
            bool inserted = false;
            for (int i = 1; i < driveSelectComboBox.Items.Count; i++)
            {
                if ((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).DriveLetter > driveLetter)
                {
                    var deviceCheckBox = new CheckBoxDeviceItem(driveLetter)
                    {
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Height = 20,
                        Width = 510,
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    deviceCheckBox.Click += DeviceCheckBox_Click;

                    var deviceInfoButton = new DeviceButton(driveLetter)
                    {
                        BorderThickness = new Thickness(0),
                        Width = 20,
                        Height = 20,
                        Content = FindResource("infoIcon") as Viewbox,
                        ToolTip = "Displays device info"
                    };

                    deviceInfoButton.Click += DeviceInfoButton_Click;

                    var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
                    stackPanel.Children.Add(deviceCheckBox);
                    stackPanel.Children.Add(deviceInfoButton);

                    driveSelectComboBox.Items.Insert(i,
                        new ComboBoxItem()
                        {
                            Padding = new Thickness(0),
                            Content = stackPanel
                        });
                    inserted = true;
                    break;
                }
            }

            if (!inserted)
            {
                var deviceCheckBox = new CheckBoxDeviceItem(driveLetter)
                {
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Height = 20,
                    Width = 510,
                    Margin = new Thickness(5, 0, 0, 0)
                };

                deviceCheckBox.Click += DeviceCheckBox_Click;

                var deviceInfoButton = new DeviceButton(driveLetter)
                {
                    BorderThickness = new Thickness(0),
                    Width = 20,
                    Height = 20,
                    Content = FindResource("infoIcon") as Viewbox,
                    ToolTip = "Displays device info"
                };

                deviceInfoButton.Click += DeviceInfoButton_Click;

                var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
                stackPanel.Children.Add(deviceCheckBox);
                stackPanel.Children.Add(deviceInfoButton);

                driveSelectComboBox.Items.Add(new ComboBoxItem()
                {
                    Padding = new Thickness(0),
                    Content = stackPanel
                });
            }
        }

        private void DeviceCheckBox_Click(object sender, RoutedEventArgs e)
        {
            DeviceCheckBoxClickHandler();
        }

        private void DeviceInfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                new DeviceInfoWindow(this, (sender as DeviceButton).DeviceLetter).Show();
            }
            catch
            {
                MessageBox.Show(this, "Unable to get device information.\nCheck if device is inserted and accessible.", "Unable to get device information.", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeviceCheckBoxClickHandler()
        {
            string str = "";
            bool first = true;
            bool hasMultiple = false;

            for (int i = 1, count = 0; i < driveSelectComboBox.Items.Count; i++)
            {
                if ((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBox).IsChecked.Value)
                {
                    if (++count == 2)
                    {
                        hasMultiple = true;
                        break;
                    }
                }
            }

            for (int i = 1; i < driveSelectComboBox.Items.Count; i++)
            {
                if ((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBox).IsChecked.Value)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        str += ", ";
                    }
                    if (hasMultiple)
                    {
                        str += (((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).ShortName;
                    }
                    else
                    {
                        str += (((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).Content;
                    }
                }

                (driveSelectComboBox.Items[0] as ComboBoxItem).Content = str;
            }
        }

        private char[] GetSelectedDevices()
        {
            List<char> selectedDevices = new List<char>();

            if (driveSelectComboBox.Items.Count != 0)
            {
                for (int i = 1; i < driveSelectComboBox.Items.Count; i++)
                {
                    if ((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).IsChecked.Value)
                    {
                        selectedDevices.Add((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).DriveLetter);
                    }
                }
            }

            return selectedDevices.ToArray();
        }

        private void HandleEnableLinkedConnectionCommand()
        {
            if (!Utils.CheckMappedDrivesEnable())
            {
                if (Utils.SetMappedDrivesEnable())
                {
                    MessageBox.Show(this, "Enabling mapped drives was successful.\nComputer restart is required to make the feature work.", "Mapped drives", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(this, "Enabling mapped drives was not successful.", "Mapped drives", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show(this, "Mapped drives are already enabled.\nComputer restart is required to make the feature work.", "Mapped drives", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void HandleAlwaysOnTopCommand(MenuItem item)
        {
            if (!item.IsChecked)
            {
                Topmost = false;
            }
            else
            {
                Topmost = true;
            }
        }

        private void RemoveDriveItem(char driveLetter)
        {
            if (driveLetter != 0)
            {
                for (int i = 1; i < driveSelectComboBox.Items.Count; i++)
                {
                    if ((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).DriveLetter == driveLetter)
                    {
                        driveSelectComboBox.Items.RemoveAt(i);
                        DeviceCheckBoxClickHandler();
                        break;
                    }
                }
                if (driveSelectComboBox.Items.Count == 1)
                {
                    driveSelectComboBox.Items.Clear();
                }
            }
        }

        private async Task HandleWipeButtonClick()
        {
            var devices = GetSelectedDevices();

            DisplayInfoPart(false);

            if (devices.Length == 0)
            {
                MessageBox.Show(this, "Cannot wipe device. No device selected.\nPlease select at least one device.", "No device selected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var device in devices)
            {
                if (Disk.IsDriveReadOnly(string.Format(@"{0}:\", device)))
                {
                    MessageBox.Show(this, string.Format(@"Cannot wipe device. Device [{0}:\ - {1}] is read only. Aborting.", device, Disk.GetModelFromDrive(device)),
                        "Read only device", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            if (MessageBox.Show(this, string.Format("Wiping {0}\ncan corrupt the device(s).\nMake sure you have selected correct device(s) and you know what you are doing.\nWe are not responsible for any damage done.\nAre you sure you want to continue ?", Helpers.GetDevicesListWithModel(GetSelectedDevices())),
                "Confirm wipe", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            SetUIState(false, false);
            Mouse.OverrideCursor = Cursors.Wait;
            disk = new DiskRaw(devices);
            await disk.WipeFileSystemAndPartitions();

            MessageBox.Show(this, "Device(s) wiped successfully.\nNow you need to reformat them using Windows format dialog or any other formatting software\nto filesystem of your selection.",
                "Wipe successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void ProcessCommandLineArguments()
        {
            CommandLineArguments args = null;
            try
            {
                args = CommandLineArguments.Parse(Environment.GetCommandLineArgs());
            }
            catch
            {
                MessageBox.Show(this, "Arguments error", "Unable to parse command line arguments.\nMake sure you entered valid command line arguments.", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            imagePathTextBox.Text = args.ImagePath;

            foreach (var device in args.Devices)
            {
                for (int i = 1; i < driveSelectComboBox.Items.Count; i++)
                {
                    if ((((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).DriveLetter == device)
                    {
                        (((driveSelectComboBox.Items[i] as ComboBoxItem).Content as StackPanel).Children[0] as CheckBoxDeviceItem).IsChecked = true;
                        DeviceCheckBoxClickHandler();
                        break;
                    }
                }
            }

            readOnlyAllocatedCheckBox.IsChecked = args.ReadOnlyAllocated;

            if (args.Zip != null)
            {
                onTheFlyZipCheckBox.IsChecked = args.Zip.Value;
            }

            if (args.AutoStart)
            {
                if (args.Read && args.Write)
                {
                    MessageBox.Show(this, "Arguments error", "Write and read cannot be started together.\nSelect only one action.", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if ((args.Read || args.Write) && args.Verify)
                {
                    verifyCheckBox.IsChecked = true;
                }

                if (args.Read)
                {
                    verifyingAfterOperation = false;

                    try
                    {
                        HandleReadButtonClick();
                    }
                    catch (Exception ex)
                    {
                        disk?.Dispose();
                        disk = null;
                        MessageBox.Show(this, ex.Message, "Unknown error");
                    }
                    return;
                }

                if (args.Write)
                {
                    verifyingAfterOperation = false;

                    try
                    {
                        HandleWriteButtonClick();
                    }
                    catch (Exception ex)
                    {
                        disk?.Dispose();
                        disk = null;
                        MessageBox.Show(this, ex.Message, "Unknown error");
                    }
                    return;
                }

                if (args.Verify)
                {
                    verifyingAfterOperation = false;
                    try
                    {
                        HandleVerifyButtonClick();
                    }
                    catch (Exception ex)
                    {
                        disk?.Dispose();
                        disk = null;
                        MessageBox.Show(this, ex.Message, "Unknown error");
                    }
                    return;
                }
            }
            else
            {
                if (args.Verify)
                {
                    verifyCheckBox.IsChecked = true;
                }
            }
        }

        private void showInfoButton_Click(object sender, RoutedEventArgs e)
        {
            new OperationFinishedInfoWindow(this, lastOperationInfo).ShowDialog();
        }

        private void InitAccelerators()
        {
            accelerators = new List<Label>(16);
            accelerators.Add(acceleratorLabel_cancel);
            accelerators.Add(acceleratorLabel_open);
            accelerators.Add(acceleratorLabel_read);
            accelerators.Add(acceleratorLabel_verify);
            accelerators.Add(acceleratorLabel_wipe);
            accelerators.Add(acceleratorLabel_write);
            accelerators.Add(acceleratorLabel_allocatedPartitons);
            accelerators.Add(acceleratorLabel_compression);
            accelerators.Add(acceleratorLabel_verifyWhenFinished);
            accelerators.Add(acceleratorLabel_encryption);
            accelerators.Add(acceleratorLabel_hash);
            accelerators.Add(acceleratorLabel_devices);
            accelerators.Add(acceleratorLabel_refreshDevices);
            accelerators.Add(acceleratorLabel_image);
            accelerators.Add(acceleratorLabel_checksum);
            accelerators.Add(acceleratorLabel_checksumType);
        }

        private void SetAcceleratorsVisibility(bool visible)
        {
            foreach (var accelerator in accelerators)
            {
                accelerator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (acceleratorsVisible)
            {
                acceleratorsVisible = false;
                SetAcceleratorsVisibility(false);
            }
        }

        private void windowTop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void window_Activated(object sender, EventArgs e)
        {
            if (disk == null && checksum == null)
            {
                windowBorderEffect.Color = ActivatedColor;
                windowBorder.BorderBrush = ActivatedBrush;
            }
            else
            {
                windowBorderEffect.Color = WorkingColor;
                windowBorder.BorderBrush = WorkingBrush;
            }
            windowTitleLabel.Foreground = FindResource("Foreground") as Brush;
        }

        private void window_Deactivated(object sender, EventArgs e)
        {
            windowBorderEffect.Color = DeactivatedColor;
            windowBorder.BorderBrush = DeactivatedBrush;
            windowTitleLabel.Foreground = FindResource("WindowInactiveTitleColor") as Brush;
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void minimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void notificationButton_Click(object sender, RoutedEventArgs e)
        {
            notificationContainer.Visibility = Visibility.Visible;
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private void ShowContextMenu(bool highlightClose, bool placeToIcon = false)
        {
            var cm = new ContextMenu()
            {
                Style = FindResource("StandardContextMenu") as Style
            };

            var cmItemRestore = new MenuItem()
            {
                Header = "Restore down",
                IsEnabled = false,
                Foreground = FindResource("MenuDisabledForeground") as Brush,//new SolidColorBrush(WindowState == WindowState.Maximized ? Color.FromRgb(30, 30, 30) : Color.FromRgb(96, 96, 96)),
                Icon = FindResource("restore_icon") as Canvas
            };

            var cmItemMove = new MenuItem()
            {
                Header = "Move"
            };

            cmItemMove.Click += (s, e) =>
            {
                //SetCursorPos((int)(Left + (ActualWidth / 2)), (int)(Top + 15));
            };

            var cmItemSize = new MenuItem()
            {
                Header = "Size",
                IsEnabled = false,
                Foreground = FindResource("MenuDisabledForeground") as Brush
            };

            var cmItemMinimize = new MenuItem()
            {
                Header = "Minimize",
                Foreground = (Brush)FindResource("Foreground"),
                Icon = FindResource("minimize_icon_menu") as Canvas
            };

            cmItemMinimize.Click += (s, e) =>
            {
                WindowState = WindowState.Minimized;
            };

            var cmItemMaximize = new MenuItem()
            {
                Header = "Maximize",
                IsEnabled = false,
                Foreground = FindResource("MenuDisabledForeground") as Brush,
                Icon = FindResource("maximize_icon") as Canvas
            };

            var cmItemOptions = new MenuItem()
            {
                Header = "Settings",
                InputGestureText = "Ctrl+O",
                Icon = FindResource("settings_icon_menu") as Viewbox
            };

            cmItemOptions.Click += (s, e) =>
            {
                ShowSettingsWindow();
            };

            var cmItemOnTop = new MenuItem()
            {
                Header = "Always on top",
                IsCheckable = true,
                IsChecked = Topmost
            };

            cmItemOnTop.Click += (s, e) =>
            {
                HandleAlwaysOnTopCommand(s as MenuItem);
                e.Handled = true;
            };

            var cmItemUpdates = new MenuItem()
            {
                Header = "Check for updates"
            };

            cmItemUpdates.Click += (s, e) =>
            {
                CheckUpdates(true);
            };

            var cmItemMappedDrives = new MenuItem()
            {
                Header = "Enable mapped drives",
                Icon = FindResource("mapped_drives_icon") as Viewbox
            };

            cmItemMappedDrives.Click += (s, e) =>
            {
                Utils.SetMappedDrivesEnable();
            };

            var cmItemAbout = new MenuItem()
            {
                Header = "About",
                InputGestureText = "F1",
                Icon = FindResource("about_icon") as Viewbox
            };

            cmItemAbout.Click += (s, e) =>
            {
                ShowAboutWindow();
            };

            var cmItemClose = new MenuItem()
            {
                Header = "Close",
                InputGestureText = "Alt+F4",
                FontWeight = highlightClose ? FontWeights.Bold : FontWeights.Normal,
                Foreground = (Brush)FindResource("Foreground"),
                Icon = FindResource("close_icon_menu") as Canvas
            };

            cmItemClose.Click += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            cm.Items.Add(cmItemRestore);
            cm.Items.Add(cmItemMove);
            cm.Items.Add(cmItemSize);
            cm.Items.Add(cmItemMinimize);
            cm.Items.Add(cmItemMaximize);
            cm.Items.Add(new Separator());
            cm.Items.Add(cmItemOptions);
            cm.Items.Add(cmItemOnTop);
            cm.Items.Add(cmItemUpdates);
            if (Utils.CheckMappedDrivesEnable())
            {
                cm.Items.Add(cmItemMappedDrives);
            }
            cm.Items.Add(cmItemAbout);
            cm.Items.Add(new Separator());
            cm.Items.Add(cmItemClose);

            if (highlightClose || placeToIcon)
            {
                cm.PlacementTarget = windowIcon;
                cm.Placement = PlacementMode.Bottom;
            }

            cm.IsOpen = true;
            cm.Closed += (s, e) => windowContextMenuShown = false;
            windowContextMenuShown = true;
        }

        private void Label_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Right)
            {
                ShowContextMenu(false);
                e.Handled = true;
            }
        }

        private async void windowIcon_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                Application.Current.Shutdown();
                e.Handled = true;
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (!windowContextMenuShown)
                {
                    await Task.Delay(250);
                    ShowContextMenu(true);
                    windowContextMenuShown = true;
                }
                else
                {
                    windowContextMenuShown = false;
                }
            }
            e.Handled = true;
        }

        private void closeNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            notificationContainer.Visibility = Visibility.Collapsed;
        }

        private void visitWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("http://dotnetdiskimager.sourceforge.net/"));
        }

        private void refreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            HandleRefreshButton();
        }

        void HandleRefreshButton()
        {
            LoadDriveSelectItems(false);
        }
    }
}
