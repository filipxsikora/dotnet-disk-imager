using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public class OperationProgressChangedEventArgs
    {
        public DiskOperation DiskOperation { get; }
        public int Progress { get; }
        public ulong AverageBps { get; }

        public OperationProgressChangedEventArgs(int progress, ulong averageBps, DiskOperation diskOperation)
        {
            Progress = progress;
            AverageBps = averageBps;
            DiskOperation = diskOperation;
        }
    }
}
