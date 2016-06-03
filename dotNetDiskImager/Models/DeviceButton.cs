using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace dotNetDiskImager.Models
{
    public class DeviceButton : Button
    {
        public char DeviceLetter { get; }

        public DeviceButton(char deviceLetter) : base()
        {
            DeviceLetter = deviceLetter;
        }
    }
}
