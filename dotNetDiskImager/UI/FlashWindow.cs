using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace dotNetDiskImager.UI
{
    public static class Flasher
    {
        private const uint FLASHW_STOP = 0; //Stop flashing. The system restores the window to its original state.        private const UInt32 FLASHW_CAPTION = 1; //Flash the window caption.        
        private const uint FLASHW_TRAY = 2; //Flash the taskbar button.        
        private const uint FLASHW_ALL = 3; //Flash both the window caption and taskbar button.        
        private const uint FLASHW_TIMER = 4; //Flash continuously, until the FLASHW_STOP flag is set.        
        private const uint FLASHW_TIMERNOFG = 12; //Flash continuously until the window comes to the foreground.  


        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize; //The size of the structure in bytes.            
            public IntPtr hwnd; //A Handle to the Window to be Flashed. The window can be either opened or minimized.
            public uint dwFlags; //The Flash Status.            
            public uint uCount; // number of times to flash the window            
            public uint dwTimeout; //The rate at which the Window is to be flashed, in milliseconds. If Zero, the function uses the default cursor blink rate.        
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        public static void FlashWindow(this MainWindow win, uint count = uint.MaxValue)
        {
            //Don't flash if the window is active            
            if (win.IsActive) return;
            WindowInteropHelper h = new WindowInteropHelper(win);
            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = h.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = count,
                dwTimeout = 0
            };

            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            FlashWindowEx(ref info);
        }

        public static void StopFlashingWindow(this Window win)
        {
            WindowInteropHelper h = new WindowInteropHelper(win);
            FLASHWINFO info = new FLASHWINFO();
            info.hwnd = h.Handle;
            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            info.dwFlags = FLASHW_STOP;
            info.uCount = uint.MaxValue;
            info.dwTimeout = 0;
            FlashWindowEx(ref info);
        }
    }
}
