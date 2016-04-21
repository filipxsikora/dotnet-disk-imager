using dotNetDiskImager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace dotNetDiskImager
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            LoadAppSettingsToGUI();
        }

        void LoadAppSettingsToGUI()
        {
            displayPreWriteWarnings.IsChecked = AppSettings.Settings.DisplayWriteWarnings;

            if (AppSettings.Settings.DefaultFolder == DefaultFolder.LastUsed)
            {
                defaultFolder_lastUsed.IsChecked = true;
                defaultFolder_userSpecify.IsChecked = false;
                defaultFolderDialogButton.IsEnabled = false;
                defaultFolder_userSpecifyValue.IsEnabled = false;
            }
            else
            {
                defaultFolder_lastUsed.IsChecked = false;
                defaultFolder_userSpecify.IsChecked = true;
                defaultFolderDialogButton.IsEnabled = true;
                defaultFolder_userSpecifyValue.IsEnabled = true;
                defaultFolder_userSpecifyValue.Text = AppSettings.Settings.DefaultFolderPath;
            }

            switch (AppSettings.Settings.AnimationsSettings)
            {
                case Animations.AlwaysOn:
                    animationsOptions.SelectedIndex = 0;
                    break;
                case Animations.OnlyOnAcPower:
                    animationsOptions.SelectedIndex = 1;
                    break;
                case Animations.Never:
                    animationsOptions.SelectedIndex = 2;
                    break;
            }

            switch (AppSettings.Settings.TaskbarExtraInfo)
            {
                case TaskbarExtraInfo.Nothing:
                    showMoreOptions.SelectedIndex = 0;
                    break;
                case TaskbarExtraInfo.Percent:
                    showMoreOptions.SelectedIndex = 1;
                    break;
                case TaskbarExtraInfo.CurrentSpeed:
                    showMoreOptions.SelectedIndex = 2;
                    break;
                case TaskbarExtraInfo.RemainingTime:
                    showMoreOptions.SelectedIndex = 3;
                    break;
                case TaskbarExtraInfo.ActiveDevice:
                    showMoreOptions.SelectedIndex = 4;
                    break;
                case TaskbarExtraInfo.ImageFileName:
                    showMoreOptions.SelectedIndex = 5;
                    break;
            }
        }

        private void defaultFolder_lastUsed_Click(object sender, RoutedEventArgs e)
        {
            defaultFolderDialogButton.IsEnabled = false;
            defaultFolder_userSpecifyValue.IsEnabled = false;
        }

        private void defaultFolder_userSpecify_Click(object sender, RoutedEventArgs e)
        {
            defaultFolderDialogButton.IsEnabled = true;
            defaultFolder_userSpecifyValue.IsEnabled = true;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Settings.DisplayWriteWarnings = displayPreWriteWarnings.IsChecked.Value;

            if (defaultFolder_lastUsed.IsChecked.Value)
            {
                AppSettings.Settings.DefaultFolder = DefaultFolder.LastUsed;
            }
            else
            {
                AppSettings.Settings.DefaultFolder = DefaultFolder.UserDefined;
                AppSettings.Settings.DefaultFolderPath = defaultFolder_userSpecifyValue.Text;
            }

            switch (animationsOptions.SelectedIndex)
            {
                case 0:
                    AppSettings.Settings.AnimationsSettings = Animations.AlwaysOn;
                    break;
                case 1:
                    AppSettings.Settings.AnimationsSettings = Animations.OnlyOnAcPower;
                    break;
                case 2:
                    AppSettings.Settings.AnimationsSettings = Animations.Never;
                    break;
            }

            switch (showMoreOptions.SelectedIndex)
            {
                case 0:
                    AppSettings.Settings.TaskbarExtraInfo = TaskbarExtraInfo.Nothing;
                    break;
                case 1:
                    AppSettings.Settings.TaskbarExtraInfo = TaskbarExtraInfo.Percent;
                    break;
                case 2:
                    AppSettings.Settings.TaskbarExtraInfo = TaskbarExtraInfo.CurrentSpeed;
                    break;
                case 3:
                    AppSettings.Settings.TaskbarExtraInfo = TaskbarExtraInfo.RemainingTime;
                    break;
                case 4:
                    AppSettings.Settings.TaskbarExtraInfo = TaskbarExtraInfo.ActiveDevice;
                    break;
                case 5:
                    AppSettings.Settings.TaskbarExtraInfo = TaskbarExtraInfo.ImageFileName;
                    break;
            }

            AppSettings.SaveSettings();
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void defaultFolderDialogButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.DialogResult result = System.Windows.Forms.DialogResult.None;
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.SelectedPath = string.IsNullOrEmpty(defaultFolder_userSpecifyValue.Text) ? AppSettings.Settings.DefaultFolderPath : defaultFolder_userSpecifyValue.Text;

            try
            {
                result = dlg.ShowDialog();
            }
            catch
            {
                dlg.SelectedPath = "";
                result = dlg.ShowDialog();
            }
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                defaultFolder_userSpecifyValue.Text = dlg.SelectedPath;
            }
        }
    }
}
