using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public class OperationFinishedEventArgs
    {
        public bool Done { get; }
        public bool Success { get; }
        public OperationFinishedState OperationState { get; }
        public DiskOperation DiskOperation { get; }
        public Exception Exception {get;}

        public OperationFinishedEventArgs(bool done, bool success, OperationFinishedState operationState, DiskOperation diskOperation, Exception exception)
        {
            Done = done;
            Success = success;
            OperationState = operationState;
            DiskOperation = diskOperation;
            Exception = exception;
        }
    }
}
