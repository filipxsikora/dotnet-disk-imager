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
            double result = Math.Ceiling(bytes / 1000000000.0);
            //if(result > 0)
            return string.Format("{0:0.0} GB", result);
        }

        public static string SecondsToEstimate(ulong seconds, bool shortFormat = false)
        {
            TimeSpan time;
            if (seconds > 86400)
            {
                return shortFormat ? "More than one day" : ">1 Day";
            }

            string pre = shortFormat ? "~" : "About ";

            if (seconds > 3600)
            {
                time = TimeSpan.FromSeconds(((seconds / 300) + 1) * 300);
                return string.Format("{0}{1} hour{2}{3}", pre, time.Hours, time.Hours != 1 ? "s" : "", time.Minutes != 0 ? string.Format(" and {0} minutes", time.Minutes) : "");
            }

            if (seconds > 600)
            {
                time = TimeSpan.FromSeconds(((seconds / 30) + 1) * 30);
                return string.Format("{0}{1} minute{2}{3}", pre, time.Minutes, time.Minutes != 1 ? "s" : "", time.Seconds != 0 ? string.Format(" and {0} seconds", time.Seconds) : "");
            }

            if (seconds >= 55)
            {
                time = TimeSpan.FromSeconds(((seconds / 15) + 1) * 15);
                return string.Format("{0}{1} minute{2}{3}", pre, time.Minutes, time.Minutes != 1 ? "s" : "", time.Seconds != 0 ? string.Format(" and {0} seconds", time.Seconds) : "");
            }

            time = TimeSpan.FromSeconds(((seconds / 5) + 1) * 5);
            return string.Format("{0}{1} seconds", pre, time.Seconds);

        }
    }
}
