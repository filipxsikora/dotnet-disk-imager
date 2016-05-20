using dotNetDiskImager.DiskAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public class Helpers
    {
        readonly static string[] byteUnits = { "", "k", "M", "G", "T" };

        public static string BytesToXbytes(ulong bytes)
        {
            double speedValue = bytes;
            int i = 0;
            while (speedValue > 1024 && i < byteUnits.Length)
            {
                i++;
                speedValue /= 1024.0;
            }

            return string.Format("{0:0.00} {1}B", speedValue, byteUnits[i]);
        }

        public static string BytesToClosestXbytes(ulong bytes)
        {
            double result = 0;
            string unit = "";

            if (bytes >= 966367641)
            {
                unit = "GB";
                result = bytes / 1073741824.0;
            }
            else
            {
                result = bytes / 1048576.0;
                unit = "MB";
            }

            if (result >= 100)
            {
                return string.Format("{0:0.} {1}", Math.Truncate(result), unit);
            }
            else if (result >= 10)
            {
                return string.Format("{0:0.0} {1}", Math.Truncate(result * 10) / 10, unit);
            }
            else
            {
                return string.Format("{0:0.00} {1}", Math.Truncate(result * 100) / 100, unit);
            }
        }

        public static string SecondsToEstimate(ulong seconds, bool shortFormat = false)
        {
            TimeSpan time;
            if (seconds > 86400)
            {
                return shortFormat ? ">1 Day" : "More than one day";
            }

            if (seconds >= 3570)
            {
                time = TimeSpan.FromSeconds(((seconds / 300) + 1) * 300);
                if (shortFormat)
                {
                    return string.Format("~{0}h:{1}m", time.Hours, time.Minutes);
                }
                else
                {
                    return string.Format("About {0} hour{1}{2}", time.Hours, time.Hours != 1 ? "s" : "", time.Minutes != 0 ? string.Format(" and {0} minutes", time.Minutes) : "");
                }
            }

            if (seconds >= 545)
            {
                time = TimeSpan.FromSeconds(((seconds / 30) + 1) * 30);
                if (shortFormat)
                {
                    return string.Format("~{0}m:{1}s", time.Minutes, time.Seconds);
                }
                else
                {
                    return string.Format("About {0} minute{1}{2}", time.Minutes, time.Minutes != 1 ? "s" : "", time.Seconds != 0 ? string.Format(" and {0} seconds", time.Seconds) : "");
                }
            }

            if (seconds >= 55)
            {
                time = TimeSpan.FromSeconds(((seconds / 15) + 1) * 15);
                if (shortFormat)
                {
                    return string.Format("~{0}m:{1}s", time.Minutes, time.Seconds);
                }
                else
                {
                    return string.Format("About {0} minute{1}{2}", time.Minutes, time.Minutes != 1 ? "s" : "", time.Seconds != 0 ? string.Format(" and {0} seconds", time.Seconds) : "");
                }
            }

            time = TimeSpan.FromSeconds(((seconds / 5) + 1) * 5);
            if (shortFormat)
            {
                return string.Format("~{0}s", time.Seconds);
            }
            else
            {
                return string.Format("About {0} seconds", time.Seconds);
            }
        }

        public static string GetDevicesListWithModel(char[] deviceLetters)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var deviceLetter in deviceLetters)
            {
                sb.Append(string.Format("\n[{0}:\\ - {1}]", deviceLetter, Disk.GetModelFromDrive(deviceLetter)));
            }

            return sb.ToString();
        }

        public static string GetDevicesListShort(char[] deviceLetters)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < deviceLetters.Length; i++)
            {
                sb.Append(string.Format("[{0}:\\]", deviceLetters[i]));
                if (i < deviceLetters.Length - 1)
                {
                    sb.Append(", ");
                }
            }

            return sb.ToString();
        }
    }
}
