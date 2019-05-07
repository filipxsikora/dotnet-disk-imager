using dotNetDiskImager.DiskAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace dotNetDiskImager.Models
{
    public class CheckBoxDeviceItem : CheckBox
    {
        public delegate void ModelAcquiredEventHandler(object sender, EventArgs e);
        public event ModelAcquiredEventHandler ModelAcquired;

        public char DriveLetter { get; }
        public string Model { get; private set; }
        public ulong DeviceLength { get; }

        public string ShortName
        {
            get
            {
                return string.Format(@"[{0}:\] ({1})", DriveLetter, Helpers.BytesToClosestXbytes(DeviceLength));
            }
        }

        public CheckBoxDeviceItem(char driveLetter, bool getImmediate = false) : base()
        {
            Style = FindResource("standardCheckBox") as Style;
            DriveLetter = driveLetter;
            int deviceID = NativeDiskWrapper.CheckDriveType(string.Format(@"\\.\{0}:\", DriveLetter));
            DeviceLength = Disk.GetDeviceLength(deviceID);

            if (getImmediate)
            {
                try
                {
                    string model = Disk.GetModelFromDrive(DriveLetter);
                    Content = string.Format(@"[{0}:\] ({1}) - {2}", DriveLetter, Helpers.BytesToClosestXbytes(DeviceLength), model);
                    Model = model;
                    return;
                }
                catch
                {
                    Content = string.Format(@"[{0}:\] ({1}) - getting device information", driveLetter, Helpers.BytesToClosestXbytes(DeviceLength));
                }
            }
            else
            {
                Content = string.Format(@"[{0}:\] ({1}) - getting device information", driveLetter, Helpers.BytesToClosestXbytes(DeviceLength));
            }

            IsEnabled = false;

            Task.Run(() =>
            {
                int maxTries = 20;
                string model = "";

                while (maxTries-- > 0)
                {
                    Task.Delay(500);

                    try
                    {
                        model = Disk.GetModelFromDrive(DriveLetter);
                    }
                    catch
                    {
                        continue;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        Content = string.Format(@"[{0}:\] ({1}) - {2}", DriveLetter, Helpers.BytesToClosestXbytes(DeviceLength), model);
                        Model = model;
                        IsEnabled = true;
                        ModelAcquired?.Invoke(this, new EventArgs());
                    });

                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    Content = string.Format(@"[{0}:\] - n/a", DriveLetter);
                    Model = "n/a";
                    IsEnabled = true;
                    ModelAcquired?.Invoke(this, new EventArgs());
                });
            });
        }
    }
}
