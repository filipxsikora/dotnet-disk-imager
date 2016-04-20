using dotNetDiskImager.DiskAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace dotNetDiskImager.Models
{
    public class ComboBoxDeviceItem : ComboBoxItem
    {
        public char DriveLetter { get; set; }
        public string Model { get; private set; }

        public ComboBoxDeviceItem(char driveLetter) : base()
        {
            DriveLetter = driveLetter;
            Content = string.Format(@"[{0}:\] - getting device information", driveLetter);
            IsEnabled = false;

            new Thread(() =>
            {
                int maxTries = 10;
                string model = "";

                while (maxTries-- > 0)
                {
                    Thread.Sleep(1000);
                    try
                    {
                        model = Disk.GetModelFromDrive(DriveLetter);
                    }
                    catch { continue; }

                    Dispatcher.Invoke(() =>
                    {
                        Content = string.Format(@"[{0}:\] - {1}", DriveLetter, model);
                        Model = model;
                        IsEnabled = true;
                    });

                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    Content = string.Format(@"[{0}:\] - n/a", DriveLetter);
                    Model = "n/a";
                    IsEnabled = true;
                });
            })
            { IsBackground = true }.Start();
        }
    }
}
