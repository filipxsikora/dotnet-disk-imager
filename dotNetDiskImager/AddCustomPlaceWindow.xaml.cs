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
    /// Interaction logic for AddCustomPlaceWindow.xaml
    /// </summary>
    public partial class AddCustomPlaceWindow : Window
    {
        public string Path
        {
            get
            {
                return pathTextBox.Text;
            }
        }

        public AddCustomPlaceWindow(Window owner, string path)
        {
            InitializeComponent();
            Owner = owner;
            pathTextBox.Text = path;
        }

        private void folderDialogButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.DialogResult result = System.Windows.Forms.DialogResult.None;
            var dlg = new System.Windows.Forms.FolderBrowserDialog();

            dlg.SelectedPath = pathTextBox.Text;

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
                pathTextBox.Text = dlg.SelectedPath;
            }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (pathTextBox.Text.Length > 0)
            {
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    e.Handled = true;
                    Close();
                }

                if (e.Key == Key.Enter)
                {
                    if (pathTextBox.Text.Length > 0)
                    {
                        DialogResult = true;
                    }
                    else
                    {
                        DialogResult = false;
                    }
                    e.Handled = true;
                    Close();
                }
            }
        }
    }
}
