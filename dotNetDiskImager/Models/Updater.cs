using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public class Updater
    {
        public static string IsUpdateAvailible()
        {
            
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            string currentVersion = string.Format("{0}.{1}.{2}.{3}", appVersion.Major, appVersion.Minor, appVersion.Build, appVersion.Revision);

            try
            {
                using (var webClient = new WebClient())
                {
                    string contents = webClient.DownloadString("http://dotnetdiskimager.sourceforge.net/version.txt");
                    if (contents != currentVersion)
                    {
                        return contents;
                    }
                }
            }
            catch { return null; }

            return "";
        }
    }
}
