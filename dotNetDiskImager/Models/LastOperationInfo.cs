using dotNetDiskImager.DiskAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public class LastOperationInfo
    {
        public TimeSpan ElapsedTime { get; set; }
        public OperationFinishedEventArgs OperationFinishedArgs { get; set; }
        public char[] Devices { get; set; }
        public string ImageFile { get; set; }
    }
}
