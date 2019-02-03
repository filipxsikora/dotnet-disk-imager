using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace dotNetDiskImager.Models
{
    public class Utils
    {
        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [Flags]
        public enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }

        [DllImport("kernel32.dll")]
        public static extern ErrorModes SetErrorMode(ErrorModes uMode);

        [DllImport("user32.dll")]
        extern static bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);
        [DllImport("user32.dll")]
        extern static bool ShutdownBlockReasonDestroy(IntPtr hWnd);

        public static bool CanComputerShutdown { get; set; } = true;

        public static bool CheckMappedDrivesEnable()
        {
            try
            {
                int value = (int)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLinkedConnections", -1);
                if (value == 1)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static bool SetMappedDrivesEnable()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLinkedConnections", 1, RegistryValueKind.DWord);
                return true;
            }
            catch { }
            return false;
        }

        public static bool PreventComputerSleepAndShutdown()
        {
            CanComputerShutdown = false;
            ShutdownBlockReasonCreate(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle, "Operation is still in progress");
            return (SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED) != 0);
        }

        public static bool AllowComputerSleepAndShutdown()
        {
            CanComputerShutdown = true;
            ShutdownBlockReasonDestroy(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);
            return (SetThreadExecutionState(ES_CONTINUOUS) != 0);
        }
    }
}
