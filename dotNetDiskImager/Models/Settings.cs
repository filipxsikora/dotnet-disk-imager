using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace dotNetDiskImager.Models
{
    public enum DefaultFolder { LastUsed, UserDefined }
    public enum Animations { AlwaysOn, OnlyOnAcPower, Never }
    public enum TaskbarExtraInfo { Nothing, Percent, CurrentSpeed, RemainingTime, ActiveDevice, ImageFileName }

    public static class AppSettings
    {
        public static SettingsInternal Settings { get; set; } = SettingsInternal.Default;

        public static void SaveSettings()
        {
            string directory = string.Format(@"{0}\dotNetDiskImager", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            using (var stream = File.Create(string.Format(@"{0}\settings.xml", directory )))
            {
                var serializer = new XmlSerializer(typeof(SettingsInternal));
                serializer.Serialize(stream, Settings);
            }
        }

        public static void LoadSettings()
        {
            string file = string.Format(@"{0}\dotNetDiskImager\settings.xml", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            if (!File.Exists(file))
                return;
            using (var stream = File.Open(file, FileMode.Open))
            {
                var serializer = new XmlSerializer(typeof(SettingsInternal));
                Settings = serializer.Deserialize(stream) as SettingsInternal;
            }
        }
    }

    public class SettingsInternal
    {
        public bool DisplayWriteWarnings { get; set; }
        public DefaultFolder DefaultFolder { get; set; }
        public string DefaultFolderPath { get; set; }
        public Animations AnimationsSettings { get; set; }
        public TaskbarExtraInfo TaskbarExtraInfo { get; set; }

        public static SettingsInternal Default
        {   get
            {
                return new SettingsInternal()
                {
                    AnimationsSettings = Animations.AlwaysOn,
                    DefaultFolder = DefaultFolder.LastUsed,
                    DefaultFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    DisplayWriteWarnings = true,
                    TaskbarExtraInfo = TaskbarExtraInfo.Nothing
                };
            }
        } 
    }
}
