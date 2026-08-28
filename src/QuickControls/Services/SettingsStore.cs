using System;
using System.IO;
using System.Xml;
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

                XmlDocument document = new XmlDocument();
                document.Load(_filePath);
                bool hasSettingsVersion = HasDirectChild(document.DocumentElement, "SettingsVersion");
                bool hasPanelLayoutMode = HasDirectChild(document.DocumentElement, "PanelLayoutMode");

                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (XmlNodeReader reader = new XmlNodeReader(document))
                {
                    AppSettings settings = serializer.Deserialize(reader) as AppSettings;
                    if (settings == null)
                    {
                        return AppSettings.CreateDefaults();
                    }

                    Normalize(settings, hasSettingsVersion, hasPanelLayoutMode);
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
            if (settings == null) throw new ArgumentNullException("settings");
            Normalize(settings, true, true);
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

        private static void Normalize(AppSettings settings, bool hasSettingsVersion, bool hasPanelLayoutMode)
        {
            AppSettings defaults = AppSettings.CreateDefaults();
            bool shouldMigrateLegacyCompact = !hasSettingsVersion && !hasPanelLayoutMode && settings.PanelCompact;

            if (!Enum.IsDefined(typeof(PanelLayoutMode), settings.PanelLayoutMode))
            {
                settings.PanelLayoutMode = PanelLayoutMode.Full;
            }
            if (shouldMigrateLegacyCompact)
            {
                settings.PanelLayoutMode = PanelLayoutMode.HorizontalMini;
            }
            if (!Enum.IsDefined(typeof(PanelDockEdge), settings.DockEdge))
            {
                settings.DockEdge = PanelDockEdge.Automatic;
            }
            settings.LanguageCode = NormalizeLanguageCode(settings.LanguageCode);
            settings.SettingsVersion = AppSettings.CurrentSettingsVersion;

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

        private static bool HasDirectChild(XmlElement parent, string localName)
        {
            if (parent == null) return false;
            for (XmlNode node = parent.FirstChild; node != null; node = node.NextSibling)
            {
                if (node.NodeType == XmlNodeType.Element &&
                    string.Equals(node.LocalName, localName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string NormalizeLanguageCode(string value)
        {
            return AppText.NormalizeLanguageCode(value);
        }
    }
}
