using dotNetDiskImager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.UI
{
    public class WindowContextMenu
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool InsertMenu(IntPtr hMenu, int wPosition, int wFlags, int wIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool ModifyMenu(IntPtr hMenu, int wPosition, int wFlags, int wIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint GetMenuState(IntPtr hMenu, int wPosition, int wFlags);

        public const int WM_SYSCOMMAND = 0x112;
        const int MF_SEPARATOR = 0x800;
        const int MF_BYPOSITION = 0x400;
        const int MF_STRING = 0x0;
        const int MF_CHECKED = 0x08;

        const int WM_SYSTEMMENU = 0xA4;
        const int WP_SYSTEMMENU = 0x02;

        public const int SettingsCommand = 0x100;
        public const int AlwaysOnTopCommand = 0x150;
        public const int AboutCommand = 0x200;
        public const int CheckUpdatesCommand = 0x300;
        public const int EnableLinkedConn = 0x400;

        public static void CreateWindowMenu(IntPtr windowHandle)
        {
            IntPtr systemMenuHandle = GetSystemMenu(windowHandle, false);

            InsertMenu(systemMenuHandle, 5, MF_BYPOSITION | MF_SEPARATOR, 0, string.Empty);
            InsertMenu(systemMenuHandle, 6, MF_BYPOSITION, SettingsCommand, "Settings\tCtrl+O");
            InsertMenu(systemMenuHandle, 7, MF_BYPOSITION | (AppSettings.Settings.IsTopMost.Value ? MF_CHECKED : 0), AlwaysOnTopCommand, "Always on top");
            InsertMenu(systemMenuHandle, 8, MF_BYPOSITION, CheckUpdatesCommand, "Check for updates");
            if (!Utils.CheckMappedDrivesEnable())
            {
                InsertMenu(systemMenuHandle, 9, MF_BYPOSITION, EnableLinkedConn, "Enable mapped drives");
                InsertMenu(systemMenuHandle, 10, MF_BYPOSITION, AboutCommand, "About\tF1");
            }
            else
            {
                InsertMenu(systemMenuHandle, 9, MF_BYPOSITION, AboutCommand, "About\tF1");
            }
        }

        public static bool IsAlwaysOnTopChecked(IntPtr windowHandle)
        {
            IntPtr systemMenuHandle = GetSystemMenu(windowHandle, false);

            if ((GetMenuState(systemMenuHandle, 7, MF_BYPOSITION) & MF_CHECKED) > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void SetAlwaysOnTopChecked(IntPtr windowHandle, bool isChecked)
        {
            IntPtr systemMenuHandle = GetSystemMenu(windowHandle, false);

            if (isChecked)
            {
                ModifyMenu(systemMenuHandle, 7, MF_BYPOSITION | MF_CHECKED, AlwaysOnTopCommand, "Always on top");
            }
            else
            {
                ModifyMenu(systemMenuHandle, 7, MF_BYPOSITION, AlwaysOnTopCommand, "Always on top");
            }
        }
    }
}
