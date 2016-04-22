using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public class Utils
    {
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
    }
}
