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

namespace dotNetDiskImager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int WM_DEVICECHANGE = 0x219;
        const int DBT_DEVICEARRIVAL = 0x8000;
        const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        const int DBT_DEVTYP_VOLUME = 0x02;

        Disk disk;
        CircularBuffer remainingTimeEstimator = new CircularBuffer(5);

        public SpeedGraphModel GraphModel { get; } = new SpeedGraphModel();
        HwndSourceHook messageHook;
        bool verifyingAfterOperation = false;

        public MainWindow()
        {
            InitializeComponent();
            OxyPlot.Wpf.LineAnnotation.PlotViewProperty = speedGraph;

            foreach (var drive in Disk.GetLogicalDrives())
            {
                driveSelectComboBox.Items.Add(new ComboBoxDeviceItem(drive));
            }

            messageHook = new HwndSourceHook(WndProc);

            Loaded += (s, e) =>
            {
                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.AddHook(messageHook);
            };

            Closing += (s, e) =>
            {
                if (disk != null)
                {
                    if (MessageBox.Show("Exiting now will result in corruption at the target.\nDo you really want to exit application ?",
                    "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                    disk.CancelOperation();
                }
                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source.RemoveHook(messageHook);
            };
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
                                driveSelectComboBox.Items.Add(new ComboBoxDeviceItem(driveLetter));
                            }
                        }
                        break;
                    case DBT_DEVICEREMOVECOMPLETE:
                        if (lpdb.dbch_DeviceType == DBT_DEVTYP_VOLUME)
                        {
                            DEV_BROADCAST_VOLUME dbv = Marshal.PtrToStructure<DEV_BROADCAST_VOLUME>(lParam);
                            char driveLetter = Disk.GetFirstDriveLetterFromMask(dbv.dbch_Unitmask, false);
                            if (driveLetter != 0)
                            {
                                foreach (var item in driveSelectComboBox.Items)
                                {
                                    if ((item as ComboBoxDeviceItem).DriveLetter == driveLetter)
                                    {
                                        driveSelectComboBox.Items.Remove(item);
                                        break;
                                    }
                                }
                            }
                        }
                        break;
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
                MessageBox.Show(ex.Message, "Unknown error");
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
                MessageBox.Show(ex.Message, "Unknown error");
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
                MessageBox.Show(ex.Message, "Unknown error");
            }
        }

        private void Disk_OperationFinished(object sender, OperationFinishedEventArgs eventArgs)
        {
            if (eventArgs.Done)
            {
                verifyingAfterOperation = false;
                Dispatcher.Invoke(() =>
                {
                    this.FlashWindow();
                    SetUIState(true);
                    GraphModel.ResetToNormal();
                    programTaskbar.ProgressValue = 0;
                    programTaskbar.ProgressState = TaskbarItemProgressState.None;
                    programTaskbar.Overlay = null;
                    remainingTimeEstimator.Reset();
                    disk.Dispose();
                    disk = null;

                    DisplayInfoPart(true, false, eventArgs.OperationState, CreateInfoMessage(eventArgs));
                });
            }
            else
            {
                if ((eventArgs.DiskOperation & DiskOperation.Verify) > 0)
                {
                    verifyingAfterOperation = true;
                    Dispatcher.Invoke(() =>
                    {
                        programTaskbar.Overlay = Properties.Resources.check.ToBitmapImage();
                        stepText.Content = "Verifying...";
                        GraphModel.ResetToVerify();
                        remainingTimeEstimator.Reset();
                        timeRemainingText.Content = "Remaining time: Calculating...";
                        progressText.Content = "0% Complete";
                    });
                }
            }
        }

        private void Disk_OperationProgressReport(object sender, OperationProgressReportEventArgs eventArgs)
        {
            GraphModel.UpdateSpeedLineValue(eventArgs.AverageBps);
            Dispatcher.Invoke(() =>
            {
                remainingTimeEstimator.Add(eventArgs.RemainingBytes / eventArgs.AverageBps);
                if (remainingTimeEstimator.IsReady)
                {
                    timeRemainingText.Content = string.Format("Remaining time: {0}", Helpers.SecondsToEstimate(remainingTimeEstimator.Average()));
                }
                transferredText.Content = string.Format("Transferred: {0} of {1}", Helpers.BytesToXbytes(eventArgs.TotalBytesProcessed), Helpers.BytesToXbytes(eventArgs.TotalBytesProcessed + eventArgs.RemainingBytes));
            });
        }

        private void Disk_OperationProgressChanged(object sender, OperationProgressChangedEventArgs eventArgs)
        {
            GraphModel.UpdateSpeedLineValue(eventArgs.AverageBps);
            GraphModel.AddDataPoint(eventArgs.Progress, eventArgs.AverageBps);
            Dispatcher.Invoke(() =>
            {
                if (verifyCheckBox.IsChecked.Value && eventArgs.DiskOperation != DiskOperation.Verify)
                {
                    if (verifyingAfterOperation)
                    {
                        programTaskbar.ProgressValue = ((eventArgs.Progress / 100.0) / 2.0) + 0.5;
                    }
                    else
                    {
                        programTaskbar.ProgressValue = ((eventArgs.Progress / 100.0) / 2.0);
                    }
                }
                else
                {
                    programTaskbar.ProgressValue = eventArgs.Progress / 100.0;
                }
                progressText.Content = string.Format("{0}% Complete", eventArgs.Progress);
            });

        }

        private void fileSelectDialogButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog()
            {
                CheckFileExists = false,
                CreatePrompt = false,
                OverwritePrompt = false,
                Title = "Select a disk image file",
                Filter = "Disk image file (*.img)|*.img|Any file|*.*",
            };

            if (dlg.ShowDialog().Value)
            {
                imagePathTextBox.Text = dlg.FileName;
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (disk != null)
            {
                if (MessageBox.Show("Canceling current operation will result in corruption at the target.\nDo you really want to cancel current operation ?",
                    "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    disk.CancelOperation();
                }
            }
        }

        private void closeInfoButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayInfoPart(false);
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
                        closeInfoButton.Style = FindResource("CloseInfoButton") as Style;
                        infoSymbol.Content = FindResource("checkIcon");
                        break;
                    case OperationFinishedState.Canceled:
                        infoContainer.Style = FindResource("InfoContainerWarning") as Style;
                        infoText.Content = message;
                        infoText.Foreground = new SolidColorBrush(Color.FromRgb(116, 86, 25));
                        closeInfoButton.Style = FindResource("CloseInfoButton") as Style;
                        infoSymbol.Content = FindResource("warningIcon");
                        break;
                    case OperationFinishedState.Error:
                        infoContainer.Style = FindResource("InfoContainerError") as Style;
                        infoText.Content = message;
                        infoText.Foreground = new SolidColorBrush(Color.FromRgb(128, 5, 5));
                        closeInfoButton.Style = FindResource("CloseInfoButton") as Style;
                        infoSymbol.Content = FindResource("warningIcon");
                        break;
                }

                DoubleAnimation windowAnimation = new DoubleAnimation(260, TimeSpan.FromMilliseconds(250));
                DoubleAnimation containerAnimation = new DoubleAnimation(30, TimeSpan.FromMilliseconds(250));
                windowAnimation.Completed += (s, e) =>
                {
                    closeInfoButton.Visibility = Visibility.Visible;
                };
                BeginAnimation(HeightProperty, windowAnimation);
                infoContainer.BeginAnimation(HeightProperty, containerAnimation);
                infoContainer.Visibility = Visibility.Visible;
            }
            else
            {
                if (noAnimation)
                {
                    Height = 220;
                    infoContainer.Visibility = Visibility.Collapsed;
                    closeInfoButton.Visibility = Visibility.Collapsed;
                    return;
                }
                else
                {
                    DoubleAnimation windowAnimation = new DoubleAnimation(220, TimeSpan.FromMilliseconds(250));
                    DoubleAnimation containerAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
                    windowAnimation.Completed += (s, e) =>
                    {
                        infoContainer.Visibility = Visibility.Collapsed;
                    };
                    closeInfoButton.Visibility = Visibility.Collapsed;
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
                ValidateInputs();
            }
            catch (ArgumentException ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(ex.Message, "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(imagePathTextBox.Text))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);
                if (fileInfo.Length > 0)
                {
                    DisplayInfoPart(false);
                    if (MessageBox.Show(string.Format("File {0} already exists and it's size is {1}.\nWould you like to overwrite it ?", fileInfo.Name, Helpers.BytesToXbytes((ulong)fileInfo.Length))
                        , "File already exist", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            try
            {
                disk = new Disk((driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter);
                result = disk.InitReadImageFromDevice(imagePathTextBox.Text, readOnlyAllocatedCheckBox.IsChecked.Value);
            }
            catch (Exception ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(ex.Message, "Error occured", MessageBoxButton.OK, MessageBoxImage.Error);
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

                disk.BeginReadImageFromDevice(verifyCheckBox.IsChecked.Value);
                SetUIState(false);
                programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                programTaskbar.Overlay = Properties.Resources.read.ToBitmapImage();
            }
            else
            {
                DisplayInfoPart(false);
                MessageBox.Show(string.Format("There is not enough free space on target device [{0}:\\].\nFree space availible {1}\nFree space required {2}",
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
                MessageBox.Show(ex.Message, "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(imagePathTextBox.Text))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);
                if (fileInfo.Length == 0)
                {
                    DisplayInfoPart(false);
                    MessageBox.Show(string.Format("File {0} exists but has no size. Aborting.", fileInfo.Name)
                        , "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                DisplayInfoPart(false);
                MessageBox.Show(string.Format("File {0} does not exist. Aborting.", imagePathTextBox.Text.Split('\\', '/').Last())
                        , "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Disk.IsDriveReadOnly(string.Format(@"{0}:\", (driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter)))
            {
                DisplayInfoPart(false);
                MessageBox.Show(string.Format(@"Device [{0}:\] is read only. Aborting.", (driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter)
                        , "Read only device", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                disk = new Disk((driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter);
                result = disk.InitWriteImageToDevice(imagePathTextBox.Text);
            }
            catch (Exception ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(ex.Message, "Error occured", MessageBoxButton.OK, MessageBoxImage.Error);
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
                stepText.Content = "Writing...";

                disk.BeginWriteImageToDevice(verifyCheckBox.IsChecked.Value);
                SetUIState(false);
                programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                programTaskbar.Overlay = Properties.Resources.write.ToBitmapImage();
            }
            else
            {
                DisplayInfoPart(false);
                if (MessageBox.Show(string.Format("Target device [{0}:\\] hasn't got enough capacity.\nSpace availible {1}\nSpace required {2}\n" +
                    "The extra space {3} appears to contain any data.\nWould you like to continue anyway ?", (driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter,
                    Helpers.BytesToXbytes(result.AvailibleSpace), Helpers.BytesToXbytes(result.RequiredSpace), result.DataFound ? "DOES" : "does not"),
                    "Not enough capacity", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    disk.OperationProgressChanged += Disk_OperationProgressChanged;
                    disk.OperationProgressReport += Disk_OperationProgressReport;
                    disk.OperationFinished += Disk_OperationFinished;

                    timeRemainingText.Content = "Remaining time: Calculating...";
                    transferredText.Content = string.Format("Transferred: 0 B of {0}", Helpers.BytesToXbytes(result.RequiredSpace));
                    progressText.Content = "0% Complete";
                    stepText.Content = "Writing...";

                    disk.BeginWriteImageToDevice(true);
                    SetUIState(false);
                    programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                    programTaskbar.Overlay = Properties.Resources.write.ToBitmapImage();
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
                MessageBox.Show(ex.Message, "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (File.Exists(imagePathTextBox.Text))
            {
                var fileInfo = new FileInfo(imagePathTextBox.Text);
                if (fileInfo.Length == 0)
                {
                    DisplayInfoPart(false);
                    MessageBox.Show(string.Format("File {0} exists but has no size. Aborting.", fileInfo.Name)
                        , "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                DisplayInfoPart(false);
                MessageBox.Show(string.Format("File {0} does not exist. Aborting.", imagePathTextBox.Text.Split('\\', '/').Last())
                        , "File invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                disk = new Disk((driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter);
                result = disk.InitVerifyImageAndDevice(imagePathTextBox.Text, readOnlyAllocatedCheckBox.IsChecked.Value);
            }
            catch (Exception ex)
            {
                DisplayInfoPart(false);
                MessageBox.Show(ex.Message, "Error occured", MessageBoxButton.OK, MessageBoxImage.Error);
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

                disk.BeginVerifyImageAndDevice(result.ImageSize);
                SetUIState(false);
                programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                programTaskbar.Overlay = Properties.Resources.check.ToBitmapImage();
            }
            else
            {
                DisplayInfoPart(false);
                if (MessageBox.Show(string.Format("Image and device size does not match.\nImage size: {0}\nDevice size: {1}\nWould you like to verify data up to {2} size?",
                    Helpers.BytesToXbytes(result.ImageSize), Helpers.BytesToXbytes(result.DeviceSize), (result.DeviceSize > result.ImageSize ? "image" : "device")),
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

                    disk.BeginVerifyImageAndDevice(bytesToRead);
                    SetUIState(false);
                    programTaskbar.ProgressState = TaskbarItemProgressState.Normal;
                    programTaskbar.Overlay = Properties.Resources.check.ToBitmapImage();
                }
                else
                {
                    disk.Dispose();
                    disk = null;
                }
            }
        }

        private void SetUIState(bool enabled)
        {
            DisplayProgressPart(!enabled);
            readButton.IsEnabled = enabled;
            writeButton.IsEnabled = enabled;
            verifyImageButton.IsEnabled = enabled;
            imagePathTextBox.IsEnabled = enabled;
            driveSelectComboBox.IsEnabled = enabled;
            verifyCheckBox.IsEnabled = enabled;
            readOnlyAllocatedCheckBox.IsEnabled = enabled;
            fileSelectDialogButton.IsEnabled = enabled;
        }

        private void DisplayProgressPart(bool display)
        {
            if (display)
            {
                DoubleAnimation windowAnimation = new DoubleAnimation(430, TimeSpan.FromMilliseconds(500));
                BeginAnimation(HeightProperty, windowAnimation);
                progressPartGrid.Visibility = Visibility.Visible;
                progressPartRow.Height = new GridLength(220, GridUnitType.Pixel);
                applicationPartRow.Height = new GridLength(180, GridUnitType.Pixel);
            }
            else
            {
                DoubleAnimation windowAnimation = new DoubleAnimation(220, TimeSpan.FromMilliseconds(500));
                BeginAnimation(HeightProperty, windowAnimation);
                progressPartGrid.Visibility = Visibility.Collapsed;
                progressPartRow.Height = new GridLength(0, GridUnitType.Pixel);
                applicationPartRow.Height = new GridLength(190, GridUnitType.Pixel);
            }
        }

        private void ValidateInputs()
        {
            if (string.IsNullOrEmpty(imagePathTextBox.Text))
                throw new ArgumentException("Image file was not selected.");
            if (driveSelectComboBox.Items.Count == 0)
                throw new ArgumentException("No supported device found.");
            if (driveSelectComboBox.SelectedIndex < 0)
                throw new ArgumentException("Device was not selected.");
            if ((driveSelectComboBox.SelectedItem as ComboBoxDeviceItem).DriveLetter == imagePathTextBox.Text[0])
                throw new ArgumentException("Image file cannot be located on the device.");
        }

        private static string CreateInfoMessage(OperationFinishedEventArgs eventArgs)
        {
            string message;
            if ((eventArgs.DiskOperation & DiskOperation.Read) > 0)
            {
                message = "Reading";
                if ((eventArgs.DiskOperation & DiskOperation.Verify) > 0)
                {
                    message += " and verify";
                }
            }
            else if ((eventArgs.DiskOperation & DiskOperation.Write) > 0)
            {
                message = "Writing";
                if ((eventArgs.DiskOperation & DiskOperation.Verify) > 0)
                {
                    message += " and verify";
                }
            }
            else
            {
                message = "Verifying";
            }

            switch (eventArgs.OperationState)
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
    }
}
