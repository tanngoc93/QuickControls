using System;
using System.IO;
using System.Xml.Serialization;
using QuickControls.Models;

namespace QuickControls.Services
{
    public sealed class SettingsStore
    {
        private readonly string _directoryPath;
        private readonly string _filePath;

        public SettingsStore()
        {
            _directoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickControls");
            _filePath = Path.Combine(_directoryPath, "settings.xml");
        }

        public string FilePath
        {
            get { return _filePath; }
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return AppSettings.CreateDefaults();
                }

                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (FileStream stream = File.OpenRead(_filePath))
                {
                    AppSettings settings = serializer.Deserialize(stream) as AppSettings;
                    if (settings == null)
                    {
                        return AppSettings.CreateDefaults();
                    }

                    Normalize(settings);
                    return settings;
                }
            }
            catch
            {
                return AppSettings.CreateDefaults();
            }
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(_directoryPath);
            string temporaryPath = _filePath + ".tmp";
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    serializer.Serialize(stream, settings);
                    stream.Flush();
                }

                if (File.Exists(_filePath))
                {
                    File.Replace(temporaryPath, _filePath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, _filePath);
                }
            }
            finally
            {
                DeleteFileQuietly(temporaryPath);
            }
        }

        private static void DeleteFileQuietly(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static void Normalize(AppSettings settings)
        {
            AppSettings defaults = AppSettings.CreateDefaults();
            if (settings.StepPercent != 2 && settings.StepPercent != 5 && settings.StepPercent != 10)
            {
                settings.StepPercent = 5;
            }

            if (settings.VolumeUp == null) settings.VolumeUp = defaults.VolumeUp;
            if (settings.VolumeDown == null) settings.VolumeDown = defaults.VolumeDown;
            if (settings.BrightnessUp == null) settings.BrightnessUp = defaults.BrightnessUp;
            if (settings.BrightnessDown == null) settings.BrightnessDown = defaults.BrightnessDown;
            if (settings.ToggleMute == null) settings.ToggleMute = defaults.ToggleMute;
            if (settings.TogglePanel == null) settings.TogglePanel = defaults.TogglePanel;
            if (settings.SelectedDisplayId == null) settings.SelectedDisplayId = string.Empty;
        }
    }
}
