using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.DiskAccess
{
    public class InitOperationResult
    {
        public bool Result { get; }
        public ulong RequiredSpace { get; }
        public ulong AvailibleSpace { get; }
        public bool DataFound { get; }

        public InitOperationResult(bool result, ulong requiredSpace, ulong availibleSpace, bool dataFound)
        {
            Result = result;
            RequiredSpace = requiredSpace;
            AvailibleSpace = availibleSpace;
            DataFound = dataFound;
        }
    }

    public class VerifyInitOperationResult
    {
        public bool Result { get; }
        public ulong ImageSize { get; }
        public ulong DeviceSize { get; }

        public VerifyInitOperationResult(bool result, ulong imageSize, ulong deviceSize)
        {
            Result = result;
            ImageSize = imageSize;
            DeviceSize = deviceSize;
        }
    }
}
