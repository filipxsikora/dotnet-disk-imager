using dotNetDiskImager.DiskAccess;
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
    /// Interaction logic for PasswordWindow.xaml
    /// </summary>
    public partial class PasswordWindow : Window
    {
        readonly Color ActivatedColor = Color.FromRgb(0, 122, 204);
        readonly Brush ActivatedBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        readonly Color DeactivatedColor = Color.FromRgb(212, 212, 212);
        readonly Brush DeactivatedBrush = new SolidColorBrush(Color.FromRgb(153, 153, 153));

        string imagePath;
        bool isZipped;

        public bool Result { get; private set; } = false;
        public string Password { get; private set; }

        public PasswordWindow(string imagePath, bool isZipped)
        {
            InitializeComponent();
            this.imagePath = imagePath;
            this.isZipped = isZipped;
            DoCapsLock();
            passwordBox.Focus();
        }

        public bool ShowDialogAndVerify(Window owner)
        {
            Owner = owner;
            ShowDialog();

            return Result;
        }

        private void windowTop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void showPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (passwordBox.Visibility == Visibility.Visible)
            {
                passwordTextBox.Text = passwordBox.Password;
                passwordTextBox.Visibility = Visibility.Visible;
                passwordBox.Visibility = Visibility.Collapsed;
                passwordTextBox.Focus();
            }
            else
            {
                passwordBox.Password = passwordTextBox.Text;
                passwordBox.Visibility = Visibility.Visible;
                passwordTextBox.Visibility = Visibility.Collapsed;
                passwordBox.Focus();
            }
        }

        private void showPasswordButtonDown_Click(object sender, MouseButtonEventArgs e)
        {
            passwordTextBox.Text = passwordBox.Password;
            passwordTextBox.Visibility = Visibility.Visible;
            passwordBox.Visibility = Visibility.Collapsed;
        }

        private void showPasswordButtonUp_Click(object sender, MouseButtonEventArgs e)
        {
            passwordBox.Password = passwordTextBox.Text;
            passwordBox.Visibility = Visibility.Visible;
            passwordTextBox.Visibility = Visibility.Collapsed;
            passwordBox.Focus();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            windowBorderEffect.Color = ActivatedColor;
            windowBorder.BorderBrush = ActivatedBrush;
            windowTitleLabel.Foreground = FindResource("Foreground") as Brush;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            windowBorderEffect.Color = DeactivatedColor;
            windowBorder.BorderBrush = DeactivatedBrush;
            windowTitleLabel.Foreground = FindResource("WindowInactiveTitleColor") as Brush;
        }

        private void passwordBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ValidatePassword();
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.CapsLock)
            {
                DoCapsLock();
            }

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void enterPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
        }

        void DoCapsLock()
        {
            if (Keyboard.IsKeyToggled(Key.CapsLock))
            {
                statusIconLabel.Content = FindResource("warningIcon") as Viewbox;
                statusTextBlock.Text = "Caps lock is ON";
            }
            else
            {
                statusIconLabel.Content = null;
                statusTextBlock.Text = "";
            }
        }

        void ValidatePassword()
        {
            string password = passwordBox.Password;

            if (password.Length < 4 || password.Length > 32)
            {
                statusIconLabel.Content = FindResource("invalid_icon") as Viewbox;
                statusTextBlock.Text = "Password must have between 4 and 32 characters";

                return;
            }

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    bool result = false;

                    if (isZipped)
                    {
                        result = Disk.CheckZipFilePassword(imagePath, password);
                    }
                    else
                    {
                        result = Disk.CheckRawFilePassword(imagePath, password);
                    }

                    if (!result)
                    {
                        statusIconLabel.Content = FindResource("invalid_icon") as Viewbox;
                        statusTextBlock.Text = "Invalid password";
                        return;
                    }
                }
                catch
                {
                    statusIconLabel.Content = FindResource("invalid_icon") as Viewbox;
                    statusTextBlock.Text = "Invalid encrypted image.";
                    return;
                }
            }

            Result = true;
            Password = password;

            Close();
        }
    }
}
