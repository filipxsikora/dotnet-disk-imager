using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            versionLabel.Content = string.Format("version {0}.{1} (b{2}.{3})", appVersion.Major, appVersion.Minor, appVersion.Build, appVersion.Revision);
            if(DateTime.Now.Year > 2016)
            {
                copyrightLabel.Content = string.Format("© FxS 2016 - {0}", DateTime.Now.Year);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
