using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public class OperationProgressReportEventArgs
    {
        public ulong AverageBps { get; }
        public ulong TotalBytesProcessed { get; }
        public ulong RemainingBytes { get; }

        public OperationProgressReportEventArgs(ulong averageBps, ulong totalBytesProcessed, ulong remainingBytes)
        {
            AverageBps = averageBps;
            TotalBytesProcessed = totalBytesProcessed;
            RemainingBytes = remainingBytes;
        }
    }
}
