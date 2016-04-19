using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public enum DiskOperation : uint
    {
        Read = 1, Write = 2, Verify = 4
    }
}
