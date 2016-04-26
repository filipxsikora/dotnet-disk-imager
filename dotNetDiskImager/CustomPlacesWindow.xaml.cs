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
    /// Interaction logic for CustomPlacesWindow.xaml
    /// </summary>
    public partial class CustomPlacesWindow : Window
    {
        public CustomPlacesWindow()
        {
            InitializeComponent();
            customPlacesListBox.ItemsSource = AppSettings.Settings.CustomPlaces;
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AppSettings.Settings.CustomPlaces.Add(dlg.SelectedPath);
                customPlacesListBox.Items.Refresh();
                customPlacesListBox.Focus();
            }
        }

        private void editButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = customPlacesListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                System.Windows.Forms.DialogResult result = System.Windows.Forms.DialogResult.None;
                var dlg = new System.Windows.Forms.FolderBrowserDialog();

                dlg.SelectedPath = AppSettings.Settings.CustomPlaces[selectedIndex];

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
                    AppSettings.Settings.CustomPlaces[selectedIndex] = dlg.SelectedPath;
                    customPlacesListBox.Items.Refresh();
                    customPlacesListBox.Focus();
                }
            }
        }

        private void removeButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = customPlacesListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                AppSettings.Settings.CustomPlaces.RemoveAt(selectedIndex);
                customPlacesListBox.Items.Refresh();
                customPlacesListBox.Focus();
            }
        }

        private void downButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = customPlacesListBox.SelectedIndex;
            if (selectedIndex != -1 && selectedIndex < (customPlacesListBox.Items.Count - 1))
            {
                var temp = AppSettings.Settings.CustomPlaces[selectedIndex + 1];
                AppSettings.Settings.CustomPlaces[selectedIndex + 1] = AppSettings.Settings.CustomPlaces[selectedIndex];
                AppSettings.Settings.CustomPlaces[selectedIndex] = temp;
                customPlacesListBox.SelectedIndex++;
                customPlacesListBox.Items.Refresh();
                customPlacesListBox.Focus();
            }
        }

        private void upButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = customPlacesListBox.SelectedIndex;
            if (selectedIndex > 0)
            {
                var temp = AppSettings.Settings.CustomPlaces[selectedIndex - 1];
                AppSettings.Settings.CustomPlaces[selectedIndex - 1] = AppSettings.Settings.CustomPlaces[selectedIndex];
                AppSettings.Settings.CustomPlaces[selectedIndex] = temp;
                customPlacesListBox.SelectedIndex--;
                customPlacesListBox.Items.Refresh();
                customPlacesListBox.Focus();
            }
        }
    }
}
