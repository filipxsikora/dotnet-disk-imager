using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public enum PartitionType { RAW, MBR, GPT }

    public class DiskPartitionInfo
    {
        public PartitionType PartitionType { get; set; }
        public List<ulong> PartitionSizes { get; set; } = new List<ulong>();
        public ulong UnallocatedSize { get; set; }
        public ulong DiskTotalSize { get; set; }
    }
}
