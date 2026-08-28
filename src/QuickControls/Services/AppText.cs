using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QuickControls.Services
{
    public sealed class AppLanguageOption
    {
        internal AppLanguageOption(string code, string nativeName, string englishName)
        {
            Code = code;
            NativeName = nativeName;
            EnglishName = englishName;
        }

        public string Code { get; private set; }
        public string NativeName { get; private set; }
        public string EnglishName { get; private set; }

        public override string ToString()
        {
            return NativeName;
        }
    }

    /// <summary>
    /// Runtime text catalog for the Quick Controls application. Language packs are
    /// compiled into the application so an installation never depends on external
    /// translation files or satellite assemblies.
    /// </summary>
    public static class AppText
    {
        public const string DefaultLanguageCode = "en";

        private static readonly IDictionary<string, IDictionary<string, string>> Catalog = CreateCatalog();
        private static readonly ReadOnlyCollection<AppLanguageOption> Options = CreateLanguageOptions();
        private static readonly ReadOnlyCollection<string> CatalogKeys = CreateKeyList();
        private static readonly Regex FormatItemPattern = new Regex(
            @"\{(?<index>\d+)(?:,[^}:]+)?(?:\:[^}]+)?\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static string _currentLanguageCode = DefaultLanguageCode;

        static AppText()
        {
            ValidateCatalog();
        }

        public static event EventHandler LanguageChanged;

        public static string CurrentLanguageCode
        {
            get { return _currentLanguageCode; }
        }

        public static CultureInfo CurrentCulture
        {
            get
            {
                try { return CultureInfo.GetCultureInfo(GetSpecificCultureName(_currentLanguageCode)); }
                catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("en-US"); }
            }
        }

        public static IList<AppLanguageOption> LanguageOptions
        {
            get { return Options; }
        }

        public static IList<string> Keys
        {
            get { return CatalogKeys; }
        }

        public static bool SetLanguage(string languageCode)
        {
            string normalized = NormalizeLanguageCode(languageCode);
            if (string.Equals(_currentLanguageCode, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _currentLanguageCode = normalized;
            EventHandler handler = LanguageChanged;
            if (handler != null) handler(null, EventArgs.Empty);
            return true;
        }

        public static string Get(string key)
        {
            return Get(_currentLanguageCode, key);
        }

        public static string T(string key)
        {
            return Get(key);
        }

        public static string Format(string key, params object[] arguments)
        {
            return string.Format(CurrentCulture, Get(key), arguments ?? new object[0]);
        }

        public static string F(string key, params object[] arguments)
        {
            return Format(key, arguments);
        }

        public static string Get(string languageCode, string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            IDictionary<string, string> language;
            string value;
            string normalized = NormalizeLanguageCode(languageCode);
            if (Catalog.TryGetValue(normalized, out language) && language.TryGetValue(key, out value))
            {
                return value;
            }

            IDictionary<string, string> english = Catalog[DefaultLanguageCode];
            return english.TryGetValue(key, out value) ? value : key;
        }

        public static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return DefaultLanguageCode;

            string code = languageCode.Trim().Replace('_', '-');
            if (code.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("en-", StringComparison.OrdinalIgnoreCase)) return "en";
            if (code.Equals("vi", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("vi-", StringComparison.OrdinalIgnoreCase)) return "vi";
            if (code.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("ja-", StringComparison.OrdinalIgnoreCase)) return "ja";
            if (code.Equals("fr", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("fr-", StringComparison.OrdinalIgnoreCase)) return "fr";
            if (code.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
                code.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ||
                code.Equals("zh-SG", StringComparison.OrdinalIgnoreCase) ||
                code.Equals("zh-CHS", StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
            return DefaultLanguageCode;
        }

        public static bool IsSupportedLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return false;
            string normalized = NormalizeLanguageCode(languageCode);
            if (normalized != DefaultLanguageCode) return true;
            string code = languageCode.Trim().Replace('_', '-');
            return code.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                   code.StartsWith("en-", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetFontFamilyName(bool emphasized)
        {
            string[] candidates = GetFontCandidates(_currentLanguageCode, emphasized);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (IsFontInstalled(candidates[index])) return candidates[index];
            }

            string systemFamily = SystemFonts.MessageBoxFont.FontFamily.Name;
            if (!string.IsNullOrEmpty(systemFamily)) return systemFamily;
            return FontFamily.GenericSansSerif.Name;
        }

        public static Font CreateFont(float size, FontStyle style)
        {
            bool emphasized = (style & FontStyle.Bold) == FontStyle.Bold;
            return new Font(GetFontFamilyName(emphasized), size, style, GraphicsUnit.Point);
        }

        public static void ValidateCatalog()
        {
            IDictionary<string, string> english;
            if (!Catalog.TryGetValue(DefaultLanguageCode, out english) || english.Count == 0)
            {
                throw new InvalidOperationException("The English language pack is missing or empty.");
            }

            foreach (AppLanguageOption option in Options)
            {
                IDictionary<string, string> pack;
                if (!Catalog.TryGetValue(option.Code, out pack))
                {
                    throw new InvalidOperationException("Missing language pack: " + option.Code);
                }

                foreach (KeyValuePair<string, string> source in english)
                {
                    string translated;
                    if (!pack.TryGetValue(source.Key, out translated) || string.IsNullOrWhiteSpace(translated))
                    {
                        throw new InvalidOperationException(
                            "Missing text key '" + source.Key + "' in language pack " + option.Code + ".");
                    }

                    string expectedItems = GetFormatItemSignature(source.Value);
                    string translatedItems = GetFormatItemSignature(translated);
                    if (!string.Equals(expectedItems, translatedItems, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Format items differ for key '" + source.Key + "' in language pack " + option.Code + ".");
                    }
                }

                foreach (string key in pack.Keys)
                {
                    if (!english.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            "Unknown text key '" + key + "' in language pack " + option.Code + ".");
                    }
                }
            }
        }

        private static IDictionary<string, IDictionary<string, string>> CreateCatalog()
        {
            Dictionary<string, IDictionary<string, string>> catalog =
                new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            catalog["en"] = CreateEnglish();
            catalog["vi"] = CreateVietnamese();
            catalog["ja"] = CreateJapanese();
            catalog["zh-CN"] = CreateSimplifiedChinese();
            catalog["fr"] = CreateFrench();
            return catalog;
        }

        private static ReadOnlyCollection<AppLanguageOption> CreateLanguageOptions()
        {
            return new List<AppLanguageOption>
            {
                new AppLanguageOption("en", "English", "English"),
                new AppLanguageOption("vi", "Tiếng Việt", "Vietnamese"),
                new AppLanguageOption("ja", "日本語", "Japanese"),
                new AppLanguageOption("zh-CN", "简体中文", "Simplified Chinese"),
                new AppLanguageOption("fr", "Français", "French")
            }.AsReadOnly();
        }

        private static ReadOnlyCollection<string> CreateKeyList()
        {
            List<string> keys = new List<string>(Catalog[DefaultLanguageCode].Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys.AsReadOnly();
        }

        private static string[] GetFontCandidates(string languageCode, bool emphasized)
        {
            switch (NormalizeLanguageCode(languageCode))
            {
                case "ja":
                    return new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Segoe UI" };
                case "zh-CN":
                    return new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
                default:
                    return emphasized
                        ? new[] { "Segoe UI Semibold", "Segoe UI" }
                        : new[] { "Segoe UI" };
            }
        }

        private static string GetSpecificCultureName(string languageCode)
        {
            switch (NormalizeLanguageCode(languageCode))
            {
                case "vi": return "vi-VN";
                case "ja": return "ja-JP";
                case "zh-CN": return "zh-CN";
                case "fr": return "fr-FR";
                default: return "en-US";
            }
        }

        private static bool IsFontInstalled(string familyName)
        {
            if (string.IsNullOrEmpty(familyName)) return false;
            try
            {
                FontFamily[] families = FontFamily.Families;
                for (int index = 0; index < families.Length; index++)
                {
                    if (string.Equals(families[index].Name, familyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static string GetFormatItemSignature(string value)
        {
            List<int> indexes = new List<int>();
            MatchCollection matches = FormatItemPattern.Matches(value ?? string.Empty);
            for (int index = 0; index < matches.Count; index++)
            {
                int itemIndex;
                if (int.TryParse(matches[index].Groups["index"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out itemIndex))
                {
                    indexes.Add(itemIndex);
                }
            }
            indexes.Sort();
            return string.Join(",", indexes.ConvertAll(delegate(int item) { return item.ToString(CultureInfo.InvariantCulture); }).ToArray());
        }

        private static Dictionary<string, string> CreateEnglish()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "App.Name", "Quick Controls" },
                { "Common.Cancel", "Cancel" },
                { "Common.Close", "Close" },
                { "Common.Settings", "Settings" },
                { "Common.About", "About" },
                { "Common.Exit", "Exit" },
                { "Common.TryAgain", "Try again" },

                { "Panel.Title", "Quick Controls" },
                { "Panel.Volume", "Volume" },
                { "Panel.Brightness", "Brightness" },
                { "Panel.Mute", "Mute" },
                { "Panel.Unmute", "Unmute" },
                { "Panel.Quieter", "Quieter" },
                { "Panel.Louder", "Louder" },
                { "Panel.Dimmer", "Dimmer" },
                { "Panel.Brighter", "Brighter" },
                { "Panel.RefreshDisplays", "Refresh displays" },
                { "Panel.DisplaySettings", "Display settings" },
                { "Panel.CompactVolume", "VOLUME" },
                { "Panel.CompactBrightness", "BRIGHTNESS" },
                { "Panel.AudioUnavailable", "No audio output device was found." },
                { "Panel.VolumeMuted", "Muted" },
                { "Panel.VolumePercent.Accessible", "Volume {0} percent" },
                { "Panel.BrightnessPercent.Accessible", "Brightness {0} percent" },
                { "Panel.BrightnessUnavailable", "Brightness unavailable" },
                { "Panel.DisplaySearching", "Looking for displays with adjustable brightness..." },
                { "Panel.DisplayUnsupported", "The app can't adjust this display's brightness." },
                { "Panel.NoDisplays", "No displays found. Try again." },
                { "Panel.Collapse.Accessible", "Switch to a mini panel" },
                { "Panel.Expand.Accessible", "Expand the panel" },
                { "Panel.HideToTray.Accessible", "Hide to the system tray" },
                { "Panel.Drag.Accessible", "Move the control panel" },
                { "Panel.DecreaseVolume.Accessible", "Decrease volume" },
                { "Panel.IncreaseVolume.Accessible", "Increase volume" },
                { "Panel.Mute.Accessible", "Mute or unmute audio" },
                { "Panel.DecreaseBrightness.Accessible", "Decrease brightness" },
                { "Panel.IncreaseBrightness.Accessible", "Increase brightness" },
                { "Panel.HorizontalMini.Accessible", "Horizontal mini control panel" },
                { "Panel.VerticalMini.Accessible", "Vertical mini control panel" },
                { "Panel.EdgeTab.Accessible", "Quick Controls edge tab" },
                { "Panel.EdgeTab.Description", "Open volume and brightness controls from the screen edge" },
                { "Panel.ReturnToEdgeTab.Accessible", "Return to the edge tab" },
                { "Panel.OpenFull.Accessible", "Open the full control panel" },

                { "Settings.WindowTitle", "Settings — Quick Controls" },
                { "Settings.Title", "Settings" },
                { "Settings.Intro", "Choose a shortcut field, then press the key combination you want." },
                { "Settings.KeyboardShortcuts", "Keyboard shortcuts" },
                { "Settings.General", "General" },
                { "Settings.Interface", "Interface" },
                { "Settings.Language", "Language" },
                { "Settings.LanguageHelp", "The app changes language after you save." },
                { "Settings.InterfaceIntro", "Choose the language and panel shape that fit your workflow." },
                { "Settings.PanelLayoutHelp", "Choose a panel shape. You can switch layouts anytime from the tray menu." },
                { "Settings.ActionColumn", "Action" },
                { "Settings.ShortcutColumn", "Shortcut" },
                { "Settings.GeneralIntro", "Control startup, panel behavior, and adjustment size." },
                { "Settings.ChangeAmount", "Change amount" },
                { "Settings.StartWithWindows", "Open Quick Controls when I sign in to Windows" },
                { "Settings.AlwaysOnTop", "Keep the panel above other windows" },
                { "Settings.AutoCollapse", "Switch to a mini view when not in use" },
                { "Settings.RestoreDefaults", "Restore defaults" },
                { "Settings.SaveChanges", "Save changes" },
                { "Settings.DefaultsRestored", "Default settings restored. Save changes to apply them." },
                { "Settings.ShortcutFor", "Shortcut for {0}" },
                { "Settings.ShortcutModifierRequired", "Each shortcut must include Ctrl, Alt, Shift, or the Windows key." },
                { "Settings.ShortcutInvalid", "One shortcut is invalid." },
                { "Settings.ShortcutDuplicate", "Two actions use the same shortcut." },
                { "Action.IncreaseVolume", "Increase volume" },
                { "Action.DecreaseVolume", "Decrease volume" },
                { "Action.IncreaseBrightness", "Increase brightness" },
                { "Action.DecreaseBrightness", "Decrease brightness" },
                { "Action.ToggleMute", "Mute or unmute" },
                { "Action.TogglePanel", "Show or hide panel" },
                { "Hotkey.HoldModifier", "Hold this key and press another key" },
                { "Hotkey.RequiresModifier", "Requires Ctrl, Alt, Shift, or Windows" },
                { "Hotkey.NotSet", "Not set" },
                { "Hotkey.Space", "Space" },
                { "Hotkey.PageUp", "Page Up" },
                { "Hotkey.PageDown", "Page Down" },
                { "Hotkey.Windows", "Windows" },
                { "Accessibility.OpenChoices", "Open choices" },

                { "Settings.PanelLayout", "Panel layout" },
                { "Settings.OpenPanelAs", "Open panel as" },
                { "Settings.WhenNotInUse", "When not in use" },
                { "Settings.After", "After" },
                { "Settings.Seconds", "{0} seconds" },
                { "Settings.DockEdge", "Dock edge" },
                { "Settings.Screen", "Screen" },
                { "Settings.OpenEdgeOnHover", "Open the edge tab when I point to it" },
                { "Settings.ShowEdgeAtStartup", "Show the edge tab when Quick Controls starts" },
                { "Settings.ResetPanelPosition", "Reset panel position" },
                { "Settings.PanelPositionReset", "The panel position has been reset." },
                { "Settings.PanelPositionWillReset", "The panel position will reset after you save changes." },
                { "Settings.EdgeOptions", "Edge dock options" },
                { "Settings.EdgeOptionsHelp", "Edge options are available when an edge layout or idle action is selected." },
                { "Layout.Full", "Full panel" },
                { "Layout.HorizontalMini", "Horizontal mini" },
                { "Layout.VerticalMini", "Vertical mini" },
                { "Layout.EdgeDock", "Edge dock" },
                { "Idle.KeepCurrent", "Keep current layout" },
                { "Idle.HorizontalMini", "Switch to horizontal mini" },
                { "Idle.VerticalMini", "Switch to vertical mini" },
                { "Idle.EdgeTab", "Return to edge tab" },
                { "Idle.HideToTray", "Hide to system tray" },
                { "Edge.Auto", "Automatic" },
                { "Edge.Left", "Left" },
                { "Edge.Right", "Right" },
                { "Edge.Top", "Top" },
                { "Edge.Bottom", "Bottom" },
                { "Screen.RememberLast", "Remember last screen" },
                { "Screen.Primary", "Primary display" },
                { "EdgeMenu.OpenFullPanel", "Open full panel" },
                { "EdgeMenu.Settings", "Settings" },
                { "EdgeMenu.MoveToEdge", "Move to edge" },
                { "EdgeMenu.HideEdgeTab", "Hide edge tab to tray" },
                { "EdgeMenu.Exit", "Exit" },
                { "EdgeTab.ClickToOpen", "Click to open controls" },
                { "EdgeTab.ReturnAfterIdle", "Returns to the edge after {0} seconds" },

                { "Tray.Tooltip", "Quick Controls" },
                { "Tray.OpenPanel", "Open control panel" },
                { "Tray.MuteUnmute", "Mute / unmute" },
                { "Tray.Settings", "Settings" },
                { "Tray.StartWithWindows", "Start with Windows" },
                { "Tray.About", "About" },
                { "Tray.Exit", "Exit" },
                { "Tray.Layout", "Panel layout" },
                { "Tray.ShowEdgeTab", "Show edge tab" },
                { "Tray.HideEdgeTab", "Hide edge tab" },

                { "About.WindowTitle", "About — Quick Controls" },
                { "About.Version", "Version {0}" },
                { "About.Description", "A lightweight utility for adjusting volume and brightness quickly on Windows.\r\n\r\nIt runs quietly in the system tray and doesn't require administrator access." },
                { "About.OpenDisplaySettings", "Open Windows display settings" },
                { "About.Close", "Close" },

                { "Osd.Volume", "Volume" },
                { "Osd.Brightness", "Brightness" },
                { "Osd.SoundMuted", "Sound muted" },
                { "Osd.SoundUnmuted", "Sound unmuted" },

                { "Display.BuiltIn", "Built-in display" },
                { "Display.Laptop", "Laptop display" },
                { "Display.LaptopNumber", "Laptop display {0}" },
                { "Display.External", "External display" },
                { "Display.ExternalNumber", "External display {0}" },

                { "Error.ShortcutInUse", "The shortcut for {0} is already in use by another app." },
                { "Error.StartupNotAllowed", "Windows didn't allow changes to the startup entry." },
                { "Error.SaveSettings", "Couldn't save settings. Check your available disk space, then try again." },
                { "Error.LanguageUnsupported", "That language isn't supported. English will be used instead." },
                { "Notification.ShortcutsUnavailable.Title", "Some shortcuts aren't available" },
                { "Notification.ShortcutsUnavailable.Message", "A shortcut is already in use by another app. Open Settings to change it." },
                { "Notification.StartupFailed.Title", "Couldn't enable Start with Windows" },
                { "Notification.StartupFailed.Message", "The app will still work normally. You can try again in Settings." },
                { "Notification.Ready.Title", "Ready" },
                { "Notification.Ready.Message", "Use a shortcut or click the icon near the clock to open the control panel." },
                { "Notification.SaveFailed.Title", "Couldn't save" },
                { "Notification.SaveFailed.Message", "The Start with Windows setting wasn't changed." },
                { "Notification.StartupChangeFailed.Title", "Couldn't make the change" },
                { "Notification.StartupChangeFailed.Message", "Windows didn't allow changes to the startup entry." },
                { "Exit.Title", "Quick Controls" },
                { "Exit.Confirmation", "Exit the app?\r\n\r\nVolume and brightness shortcuts will stop working." }
            };
        }

        private static Dictionary<string, string> CreateVietnamese()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "App.Name", "Quick Controls" },
                { "Common.Cancel", "Hủy" },
                { "Common.Close", "Đóng" },
                { "Common.Settings", "Cài đặt" },
                { "Common.About", "Giới thiệu" },
                { "Common.Exit", "Thoát" },
                { "Common.TryAgain", "Thử lại" },

                { "Panel.Title", "Quick Controls" },
                { "Panel.Volume", "Âm lượng" },
                { "Panel.Brightness", "Độ sáng" },
                { "Panel.Mute", "Tắt tiếng" },
                { "Panel.Unmute", "Bật tiếng" },
                { "Panel.Quieter", "Nhỏ tiếng" },
                { "Panel.Louder", "To tiếng" },
                { "Panel.Dimmer", "Tối hơn" },
                { "Panel.Brighter", "Sáng hơn" },
                { "Panel.RefreshDisplays", "Quét lại màn hình" },
                { "Panel.DisplaySettings", "Cài đặt màn hình" },
                { "Panel.CompactVolume", "ÂM LƯỢNG" },
                { "Panel.CompactBrightness", "ĐỘ SÁNG" },
                { "Panel.AudioUnavailable", "Không tìm thấy thiết bị đầu ra âm thanh." },
                { "Panel.VolumeMuted", "Đã tắt tiếng" },
                { "Panel.VolumePercent.Accessible", "Âm lượng {0} phần trăm" },
                { "Panel.BrightnessPercent.Accessible", "Độ sáng {0} phần trăm" },
                { "Panel.BrightnessUnavailable", "Không thể điều chỉnh độ sáng" },
                { "Panel.DisplaySearching", "Đang tìm màn hình có thể điều chỉnh độ sáng..." },
                { "Panel.DisplayUnsupported", "Ứng dụng không thể điều chỉnh độ sáng của màn hình này." },
                { "Panel.NoDisplays", "Không tìm thấy màn hình. Hãy thử lại." },
                { "Panel.Collapse.Accessible", "Chuyển sang bảng điều khiển thu gọn" },
                { "Panel.Expand.Accessible", "Mở rộng bảng điều khiển" },
                { "Panel.HideToTray.Accessible", "Ẩn vào khay hệ thống" },
                { "Panel.Drag.Accessible", "Di chuyển bảng điều khiển" },
                { "Panel.DecreaseVolume.Accessible", "Giảm âm lượng" },
                { "Panel.IncreaseVolume.Accessible", "Tăng âm lượng" },
                { "Panel.Mute.Accessible", "Tắt hoặc bật tiếng" },
                { "Panel.DecreaseBrightness.Accessible", "Giảm độ sáng" },
                { "Panel.IncreaseBrightness.Accessible", "Tăng độ sáng" },
                { "Panel.HorizontalMini.Accessible", "Bảng điều khiển thu gọn nằm ngang" },
                { "Panel.VerticalMini.Accessible", "Bảng điều khiển thu gọn nằm dọc" },
                { "Panel.EdgeTab.Accessible", "Thẻ cạnh Quick Controls" },
                { "Panel.EdgeTab.Description", "Mở điều khiển âm lượng và độ sáng từ cạnh màn hình" },
                { "Panel.ReturnToEdgeTab.Accessible", "Thu về thẻ cạnh" },
                { "Panel.OpenFull.Accessible", "Mở bảng điều khiển đầy đủ" },

                { "Settings.WindowTitle", "Cài đặt — Quick Controls" },
                { "Settings.Title", "Cài đặt" },
                { "Settings.Intro", "Chọn một ô phím tắt, sau đó nhấn tổ hợp phím bạn muốn." },
                { "Settings.KeyboardShortcuts", "Phím tắt" },
                { "Settings.General", "Chung" },
                { "Settings.Interface", "Giao diện" },
                { "Settings.Language", "Ngôn ngữ" },
                { "Settings.LanguageHelp", "Ứng dụng sẽ đổi ngôn ngữ sau khi bạn lưu." },
                { "Settings.InterfaceIntro", "Chọn ngôn ngữ và kiểu bảng điều khiển phù hợp với cách bạn sử dụng." },
                { "Settings.PanelLayoutHelp", "Chọn một kiểu bảng điều khiển. Bạn có thể đổi bất kỳ lúc nào từ menu khay hệ thống." },
                { "Settings.ActionColumn", "Thao tác" },
                { "Settings.ShortcutColumn", "Phím tắt" },
                { "Settings.GeneralIntro", "Kiểm soát khởi động, hành vi bảng điều khiển và mức điều chỉnh." },
                { "Settings.ChangeAmount", "Mức thay đổi" },
                { "Settings.StartWithWindows", "Mở Quick Controls khi tôi đăng nhập vào Windows" },
                { "Settings.AlwaysOnTop", "Giữ bảng điều khiển phía trên các cửa sổ khác" },
                { "Settings.AutoCollapse", "Chuyển sang chế độ thu gọn khi không sử dụng" },
                { "Settings.RestoreDefaults", "Khôi phục mặc định" },
                { "Settings.SaveChanges", "Lưu thay đổi" },
                { "Settings.DefaultsRestored", "Đã khôi phục cài đặt mặc định. Hãy lưu thay đổi để áp dụng." },
                { "Settings.ShortcutFor", "Phím tắt cho {0}" },
                { "Settings.ShortcutModifierRequired", "Mỗi phím tắt phải có Ctrl, Alt, Shift hoặc phím Windows." },
                { "Settings.ShortcutInvalid", "Có một phím tắt không hợp lệ." },
                { "Settings.ShortcutDuplicate", "Hai thao tác đang dùng cùng một phím tắt." },
                { "Action.IncreaseVolume", "Tăng âm lượng" },
                { "Action.DecreaseVolume", "Giảm âm lượng" },
                { "Action.IncreaseBrightness", "Tăng độ sáng" },
                { "Action.DecreaseBrightness", "Giảm độ sáng" },
                { "Action.ToggleMute", "Tắt hoặc bật tiếng" },
                { "Action.TogglePanel", "Hiện hoặc ẩn bảng điều khiển" },
                { "Hotkey.HoldModifier", "Giữ phím này rồi nhấn thêm một phím khác" },
                { "Hotkey.RequiresModifier", "Cần có Ctrl, Alt, Shift hoặc phím Windows" },
                { "Hotkey.NotSet", "Chưa đặt" },
                { "Hotkey.Space", "Phím cách" },
                { "Hotkey.PageUp", "Page Up" },
                { "Hotkey.PageDown", "Page Down" },
                { "Hotkey.Windows", "Windows" },
                { "Accessibility.OpenChoices", "Mở danh sách lựa chọn" },

                { "Settings.PanelLayout", "Bố cục bảng điều khiển" },
                { "Settings.OpenPanelAs", "Mở bảng điều khiển dưới dạng" },
                { "Settings.WhenNotInUse", "Khi không sử dụng" },
                { "Settings.After", "Sau" },
                { "Settings.Seconds", "{0} giây" },
                { "Settings.DockEdge", "Ghim vào cạnh" },
                { "Settings.Screen", "Màn hình" },
                { "Settings.OpenEdgeOnHover", "Mở thẻ cạnh khi tôi trỏ chuột vào" },
                { "Settings.ShowEdgeAtStartup", "Hiện thẻ cạnh khi Quick Controls khởi động" },
                { "Settings.ResetPanelPosition", "Đặt lại vị trí bảng điều khiển" },
                { "Settings.PanelPositionReset", "Đã đặt lại vị trí bảng điều khiển." },
                { "Settings.PanelPositionWillReset", "Vị trí bảng điều khiển sẽ được đặt lại sau khi bạn lưu thay đổi." },
                { "Settings.EdgeOptions", "Tùy chọn neo cạnh" },
                { "Settings.EdgeOptionsHelp", "Tùy chọn cạnh khả dụng khi chọn bố cục cạnh hoặc thao tác chờ liên quan đến cạnh." },
                { "Layout.Full", "Bảng điều khiển đầy đủ" },
                { "Layout.HorizontalMini", "Bảng thu gọn ngang" },
                { "Layout.VerticalMini", "Bảng thu gọn dọc" },
                { "Layout.EdgeDock", "Ghim cạnh màn hình" },
                { "Idle.KeepCurrent", "Giữ bố cục hiện tại" },
                { "Idle.HorizontalMini", "Chuyển sang bảng thu gọn ngang" },
                { "Idle.VerticalMini", "Chuyển sang bảng thu gọn dọc" },
                { "Idle.EdgeTab", "Thu về thẻ cạnh" },
                { "Idle.HideToTray", "Ẩn vào khay hệ thống" },
                { "Edge.Auto", "Tự động" },
                { "Edge.Left", "Trái" },
                { "Edge.Right", "Phải" },
                { "Edge.Top", "Trên" },
                { "Edge.Bottom", "Dưới" },
                { "Screen.RememberLast", "Nhớ màn hình dùng lần trước" },
                { "Screen.Primary", "Màn hình chính" },
                { "EdgeMenu.OpenFullPanel", "Mở bảng điều khiển đầy đủ" },
                { "EdgeMenu.Settings", "Cài đặt" },
                { "EdgeMenu.MoveToEdge", "Chuyển sang cạnh" },
                { "EdgeMenu.HideEdgeTab", "Ẩn thẻ cạnh vào khay" },
                { "EdgeMenu.Exit", "Thoát" },
                { "EdgeTab.ClickToOpen", "Bấm để mở bảng điều khiển" },
                { "EdgeTab.ReturnAfterIdle", "Thu về cạnh sau {0} giây" },

                { "Tray.Tooltip", "Quick Controls" },
                { "Tray.OpenPanel", "Mở bảng điều khiển" },
                { "Tray.MuteUnmute", "Tắt / bật tiếng" },
                { "Tray.Settings", "Cài đặt" },
                { "Tray.StartWithWindows", "Khởi động cùng Windows" },
                { "Tray.About", "Giới thiệu" },
                { "Tray.Exit", "Thoát" },
                { "Tray.Layout", "Bố cục bảng điều khiển" },
                { "Tray.ShowEdgeTab", "Hiện thẻ cạnh" },
                { "Tray.HideEdgeTab", "Ẩn thẻ cạnh" },

                { "About.WindowTitle", "Giới thiệu — Quick Controls" },
                { "About.Version", "Phiên bản {0}" },
                { "About.Description", "Một tiện ích gọn nhẹ giúp điều chỉnh nhanh âm lượng và độ sáng trên Windows.\r\n\r\nỨng dụng chạy nền trong khay hệ thống và không cần quyền quản trị viên." },
                { "About.OpenDisplaySettings", "Mở cài đặt màn hình Windows" },
                { "About.Close", "Đóng" },

                { "Osd.Volume", "Âm lượng" },
                { "Osd.Brightness", "Độ sáng" },
                { "Osd.SoundMuted", "Đã tắt tiếng" },
                { "Osd.SoundUnmuted", "Đã bật tiếng" },

                { "Display.BuiltIn", "Màn hình tích hợp" },
                { "Display.Laptop", "Màn hình máy tính xách tay" },
                { "Display.LaptopNumber", "Màn hình máy tính xách tay {0}" },
                { "Display.External", "Màn hình ngoài" },
                { "Display.ExternalNumber", "Màn hình ngoài {0}" },

                { "Error.ShortcutInUse", "Phím tắt cho thao tác {0} đã được ứng dụng khác sử dụng." },
                { "Error.StartupNotAllowed", "Windows không cho phép thay đổi mục khởi động." },
                { "Error.SaveSettings", "Không thể lưu cài đặt. Hãy kiểm tra dung lượng đĩa còn trống rồi thử lại." },
                { "Error.LanguageUnsupported", "Ngôn ngữ này không được hỗ trợ. Ứng dụng sẽ dùng tiếng Anh." },
                { "Notification.ShortcutsUnavailable.Title", "Một số phím tắt không khả dụng" },
                { "Notification.ShortcutsUnavailable.Message", "Một phím tắt đã được ứng dụng khác sử dụng. Hãy mở Cài đặt để thay đổi." },
                { "Notification.StartupFailed.Title", "Không thể bật khởi động cùng Windows" },
                { "Notification.StartupFailed.Message", "Ứng dụng vẫn hoạt động bình thường. Bạn có thể thử lại trong Cài đặt." },
                { "Notification.Ready.Title", "Sẵn sàng" },
                { "Notification.Ready.Message", "Dùng phím tắt hoặc bấm biểu tượng gần đồng hồ để mở bảng điều khiển." },
                { "Notification.SaveFailed.Title", "Không thể lưu" },
                { "Notification.SaveFailed.Message", "Tùy chọn khởi động cùng Windows chưa được thay đổi." },
                { "Notification.StartupChangeFailed.Title", "Không thể thực hiện thay đổi" },
                { "Notification.StartupChangeFailed.Message", "Windows không cho phép thay đổi mục khởi động." },
                { "Exit.Title", "Quick Controls" },
                { "Exit.Confirmation", "Thoát ứng dụng?\r\n\r\nCác phím tắt âm lượng và độ sáng sẽ ngừng hoạt động." }
            };
        }

        private static Dictionary<string, string> CreateJapanese()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "App.Name", "Quick Controls" },
                { "Common.Cancel", "キャンセル" },
                { "Common.Close", "閉じる" },
                { "Common.Settings", "設定" },
                { "Common.About", "このアプリについて" },
                { "Common.Exit", "終了" },
                { "Common.TryAgain", "再試行" },

                { "Panel.Title", "Quick Controls" },
                { "Panel.Volume", "音量" },
                { "Panel.Brightness", "明るさ" },
                { "Panel.Mute", "ミュート" },
                { "Panel.Unmute", "ミュート解除" },
                { "Panel.Quieter", "小さく" },
                { "Panel.Louder", "大きく" },
                { "Panel.Dimmer", "暗く" },
                { "Panel.Brighter", "明るく" },
                { "Panel.RefreshDisplays", "ディスプレイを再検出" },
                { "Panel.DisplaySettings", "ディスプレイ設定" },
                { "Panel.CompactVolume", "音量" },
                { "Panel.CompactBrightness", "明るさ" },
                { "Panel.AudioUnavailable", "オーディオ出力デバイスが見つかりません。" },
                { "Panel.VolumeMuted", "ミュート中" },
                { "Panel.VolumePercent.Accessible", "音量 {0} パーセント" },
                { "Panel.BrightnessPercent.Accessible", "明るさ {0} パーセント" },
                { "Panel.BrightnessUnavailable", "明るさを調整できません" },
                { "Panel.DisplaySearching", "明るさを調整できるディスプレイを検索しています..." },
                { "Panel.DisplayUnsupported", "このディスプレイの明るさは調整できません。" },
                { "Panel.NoDisplays", "ディスプレイが見つかりません。再試行してください。" },
                { "Panel.Collapse.Accessible", "ミニ パネルに切り替える" },
                { "Panel.Expand.Accessible", "パネルを展開する" },
                { "Panel.HideToTray.Accessible", "システム トレイに格納する" },
                { "Panel.Drag.Accessible", "コントロール パネルを移動する" },
                { "Panel.DecreaseVolume.Accessible", "音量を下げる" },
                { "Panel.IncreaseVolume.Accessible", "音量を上げる" },
                { "Panel.Mute.Accessible", "音声をミュートまたはミュート解除する" },
                { "Panel.DecreaseBrightness.Accessible", "明るさを下げる" },
                { "Panel.IncreaseBrightness.Accessible", "明るさを上げる" },
                { "Panel.HorizontalMini.Accessible", "横型ミニ コントロール パネル" },
                { "Panel.VerticalMini.Accessible", "縦型ミニ コントロール パネル" },
                { "Panel.EdgeTab.Accessible", "Quick Controls の画面端タブ" },
                { "Panel.EdgeTab.Description", "画面端から音量と明るさのコントロールを開きます" },
                { "Panel.ReturnToEdgeTab.Accessible", "画面端タブに戻す" },
                { "Panel.OpenFull.Accessible", "フル コントロール パネルを開く" },

                { "Settings.WindowTitle", "設定 — Quick Controls" },
                { "Settings.Title", "設定" },
                { "Settings.Intro", "ショートカット欄を選び、使用するキーの組み合わせを押してください。" },
                { "Settings.KeyboardShortcuts", "キーボード ショートカット" },
                { "Settings.General", "全般" },
                { "Settings.Interface", "インターフェイス" },
                { "Settings.Language", "言語" },
                { "Settings.LanguageHelp", "保存するとアプリの表示言語が変わります。" },
                { "Settings.InterfaceIntro", "言語と作業スタイルに合うパネル形状を選択します。" },
                { "Settings.PanelLayoutHelp", "パネル形状を選択します。レイアウトはトレイ メニューからいつでも変更できます。" },
                { "Settings.ActionColumn", "操作" },
                { "Settings.ShortcutColumn", "ショートカット" },
                { "Settings.GeneralIntro", "起動、パネルの動作、調整幅を設定します。" },
                { "Settings.ChangeAmount", "調整幅" },
                { "Settings.StartWithWindows", "Windows へのサインイン時に Quick Controls を開く" },
                { "Settings.AlwaysOnTop", "パネルをほかのウィンドウより手前に表示する" },
                { "Settings.AutoCollapse", "未使用時にミニ表示へ切り替える" },
                { "Settings.RestoreDefaults", "既定値に戻す" },
                { "Settings.SaveChanges", "変更を保存" },
                { "Settings.DefaultsRestored", "既定の設定に戻しました。適用するには変更を保存してください。" },
                { "Settings.ShortcutFor", "{0} のショートカット" },
                { "Settings.ShortcutModifierRequired", "各ショートカットには Ctrl、Alt、Shift、または Windows キーが必要です。" },
                { "Settings.ShortcutInvalid", "無効なショートカットがあります。" },
                { "Settings.ShortcutDuplicate", "2 つの操作に同じショートカットが割り当てられています。" },
                { "Action.IncreaseVolume", "音量を上げる" },
                { "Action.DecreaseVolume", "音量を下げる" },
                { "Action.IncreaseBrightness", "明るさを上げる" },
                { "Action.DecreaseBrightness", "明るさを下げる" },
                { "Action.ToggleMute", "ミュートを切り替える" },
                { "Action.TogglePanel", "パネルの表示を切り替える" },
                { "Hotkey.HoldModifier", "このキーを押したまま、別のキーを押してください" },
                { "Hotkey.RequiresModifier", "Ctrl、Alt、Shift、または Windows キーが必要です" },
                { "Hotkey.NotSet", "未設定" },
                { "Hotkey.Space", "スペース" },
                { "Hotkey.PageUp", "Page Up" },
                { "Hotkey.PageDown", "Page Down" },
                { "Hotkey.Windows", "Windows" },
                { "Accessibility.OpenChoices", "選択肢を開く" },

                { "Settings.PanelLayout", "パネル レイアウト" },
                { "Settings.OpenPanelAs", "パネルの表示形式" },
                { "Settings.WhenNotInUse", "未使用時" },
                { "Settings.After", "切り替えまで" },
                { "Settings.Seconds", "{0} 秒" },
                { "Settings.DockEdge", "固定する端" },
                { "Settings.Screen", "ディスプレイ" },
                { "Settings.OpenEdgeOnHover", "画面端タブをポイントしたときに開く" },
                { "Settings.ShowEdgeAtStartup", "Quick Controls の起動時に画面端タブを表示する" },
                { "Settings.ResetPanelPosition", "パネルの位置をリセット" },
                { "Settings.PanelPositionReset", "パネルの位置をリセットしました。" },
                { "Settings.PanelPositionWillReset", "変更を保存すると、パネルの位置がリセットされます。" },
                { "Settings.EdgeOptions", "画面端固定のオプション" },
                { "Settings.EdgeOptionsHelp", "画面端のレイアウトまたは待機時の動作を選ぶと、画面端のオプションを使用できます。" },
                { "Layout.Full", "フル パネル" },
                { "Layout.HorizontalMini", "横型ミニ" },
                { "Layout.VerticalMini", "縦型ミニ" },
                { "Layout.EdgeDock", "画面端に固定" },
                { "Idle.KeepCurrent", "現在のレイアウトを維持" },
                { "Idle.HorizontalMini", "横型ミニに切り替える" },
                { "Idle.VerticalMini", "縦型ミニに切り替える" },
                { "Idle.EdgeTab", "画面端タブに戻す" },
                { "Idle.HideToTray", "システム トレイに格納する" },
                { "Edge.Auto", "自動" },
                { "Edge.Left", "左" },
                { "Edge.Right", "右" },
                { "Edge.Top", "上" },
                { "Edge.Bottom", "下" },
                { "Screen.RememberLast", "最後に使用したディスプレイを記憶" },
                { "Screen.Primary", "メイン ディスプレイ" },
                { "EdgeMenu.OpenFullPanel", "フル パネルを開く" },
                { "EdgeMenu.Settings", "設定" },
                { "EdgeMenu.MoveToEdge", "画面端へ移動" },
                { "EdgeMenu.HideEdgeTab", "画面端タブをシステム トレイに格納する" },
                { "EdgeMenu.Exit", "終了" },
                { "EdgeTab.ClickToOpen", "クリックしてコントロール パネルを開く" },
                { "EdgeTab.ReturnAfterIdle", "{0} 秒後に画面端へ戻ります" },

                { "Tray.Tooltip", "Quick Controls" },
                { "Tray.OpenPanel", "コントロール パネルを開く" },
                { "Tray.MuteUnmute", "ミュート / ミュート解除" },
                { "Tray.Settings", "設定" },
                { "Tray.StartWithWindows", "Windows と同時に起動" },
                { "Tray.About", "このアプリについて" },
                { "Tray.Exit", "終了" },
                { "Tray.Layout", "パネル レイアウト" },
                { "Tray.ShowEdgeTab", "画面端タブを表示" },
                { "Tray.HideEdgeTab", "画面端タブを隠す" },

                { "About.WindowTitle", "Quick Controls について" },
                { "About.Version", "バージョン {0}" },
                { "About.Description", "Windows で音量と明るさをすばやく調整できる軽量ユーティリティです。\r\n\r\nシステム トレイに常駐し、管理者権限は必要ありません。" },
                { "About.OpenDisplaySettings", "Windows のディスプレイ設定を開く" },
                { "About.Close", "閉じる" },

                { "Osd.Volume", "音量" },
                { "Osd.Brightness", "明るさ" },
                { "Osd.SoundMuted", "ミュートしました" },
                { "Osd.SoundUnmuted", "ミュートを解除しました" },

                { "Display.BuiltIn", "内蔵ディスプレイ" },
                { "Display.Laptop", "ノート PC のディスプレイ" },
                { "Display.LaptopNumber", "ノート PC のディスプレイ {0}" },
                { "Display.External", "外部ディスプレイ" },
                { "Display.ExternalNumber", "外部ディスプレイ {0}" },

                { "Error.ShortcutInUse", "「{0}」のショートカットは別のアプリで使用されています。" },
                { "Error.StartupNotAllowed", "Windows によりスタートアップ項目の変更が許可されませんでした。" },
                { "Error.SaveSettings", "設定を保存できませんでした。ディスクの空き容量を確認して、もう一度お試しください。" },
                { "Error.LanguageUnsupported", "この言語はサポートされていません。代わりに英語を使用します。" },
                { "Notification.ShortcutsUnavailable.Title", "一部のショートカットを使用できません" },
                { "Notification.ShortcutsUnavailable.Message", "ショートカットが別のアプリで使用されています。設定を開いて変更してください。" },
                { "Notification.StartupFailed.Title", "Windows と同時に起動する設定を有効にできませんでした" },
                { "Notification.StartupFailed.Message", "アプリは通常どおり使用できます。設定からもう一度お試しください。" },
                { "Notification.Ready.Title", "準備完了" },
                { "Notification.Ready.Message", "ショートカットを使うか、時計の近くにあるアイコンをクリックしてコントロール パネルを開きます。" },
                { "Notification.SaveFailed.Title", "保存できませんでした" },
                { "Notification.SaveFailed.Message", "Windows と同時に起動する設定は変更されませんでした。" },
                { "Notification.StartupChangeFailed.Title", "変更できませんでした" },
                { "Notification.StartupChangeFailed.Message", "Windows によりスタートアップ項目の変更が許可されませんでした。" },
                { "Exit.Title", "Quick Controls" },
                { "Exit.Confirmation", "アプリを終了しますか？\r\n\r\n音量と明るさのショートカットは動作しなくなります。" }
            };
        }

        private static Dictionary<string, string> CreateSimplifiedChinese()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "App.Name", "Quick Controls" },
                { "Common.Cancel", "取消" },
                { "Common.Close", "关闭" },
                { "Common.Settings", "设置" },
                { "Common.About", "关于" },
                { "Common.Exit", "退出" },
                { "Common.TryAgain", "重试" },

                { "Panel.Title", "Quick Controls" },
                { "Panel.Volume", "音量" },
                { "Panel.Brightness", "亮度" },
                { "Panel.Mute", "静音" },
                { "Panel.Unmute", "取消静音" },
                { "Panel.Quieter", "调低" },
                { "Panel.Louder", "调高" },
                { "Panel.Dimmer", "调暗" },
                { "Panel.Brighter", "调亮" },
                { "Panel.RefreshDisplays", "重新检测显示器" },
                { "Panel.DisplaySettings", "显示设置" },
                { "Panel.CompactVolume", "音量" },
                { "Panel.CompactBrightness", "亮度" },
                { "Panel.AudioUnavailable", "未找到音频输出设备。" },
                { "Panel.VolumeMuted", "已静音" },
                { "Panel.VolumePercent.Accessible", "音量百分之{0}" },
                { "Panel.BrightnessPercent.Accessible", "亮度百分之{0}" },
                { "Panel.BrightnessUnavailable", "无法调节亮度" },
                { "Panel.DisplaySearching", "正在查找可调节亮度的显示器..." },
                { "Panel.DisplayUnsupported", "应用无法调节此显示器的亮度。" },
                { "Panel.NoDisplays", "未找到显示器。请重试。" },
                { "Panel.Collapse.Accessible", "切换到迷你面板" },
                { "Panel.Expand.Accessible", "展开面板" },
                { "Panel.HideToTray.Accessible", "隐藏到系统托盘" },
                { "Panel.Drag.Accessible", "移动控制面板" },
                { "Panel.DecreaseVolume.Accessible", "降低音量" },
                { "Panel.IncreaseVolume.Accessible", "提高音量" },
                { "Panel.Mute.Accessible", "静音或取消静音" },
                { "Panel.DecreaseBrightness.Accessible", "降低亮度" },
                { "Panel.IncreaseBrightness.Accessible", "提高亮度" },
                { "Panel.HorizontalMini.Accessible", "横向迷你控制面板" },
                { "Panel.VerticalMini.Accessible", "纵向迷你控制面板" },
                { "Panel.EdgeTab.Accessible", "Quick Controls 屏幕边缘标签" },
                { "Panel.EdgeTab.Description", "从屏幕边缘打开音量和亮度控制" },
                { "Panel.ReturnToEdgeTab.Accessible", "返回屏幕边缘标签" },
                { "Panel.OpenFull.Accessible", "打开完整控制面板" },

                { "Settings.WindowTitle", "设置 — Quick Controls" },
                { "Settings.Title", "设置" },
                { "Settings.Intro", "选择一个快捷键输入框，然后按下所需的组合键。" },
                { "Settings.KeyboardShortcuts", "键盘快捷键" },
                { "Settings.General", "常规" },
                { "Settings.Interface", "界面" },
                { "Settings.Language", "语言" },
                { "Settings.LanguageHelp", "保存后，应用将切换显示语言。" },
                { "Settings.InterfaceIntro", "选择适合您使用方式的语言和面板形状。" },
                { "Settings.PanelLayoutHelp", "选择面板形状。您可以随时从系统托盘菜单切换布局。" },
                { "Settings.ActionColumn", "操作" },
                { "Settings.ShortcutColumn", "快捷键" },
                { "Settings.GeneralIntro", "控制启动方式、面板行为和每次调整幅度。" },
                { "Settings.ChangeAmount", "每次调整幅度" },
                { "Settings.StartWithWindows", "登录 Windows 时打开 Quick Controls" },
                { "Settings.AlwaysOnTop", "让面板保持在其他窗口上方" },
                { "Settings.AutoCollapse", "不使用时切换到迷你视图" },
                { "Settings.RestoreDefaults", "恢复默认设置" },
                { "Settings.SaveChanges", "保存更改" },
                { "Settings.DefaultsRestored", "已恢复默认设置。请保存更改以应用。" },
                { "Settings.ShortcutFor", "“{0}”的快捷键" },
                { "Settings.ShortcutModifierRequired", "每个快捷键必须包含 Ctrl、Alt、Shift 或 Windows 键。" },
                { "Settings.ShortcutInvalid", "有一个快捷键无效。" },
                { "Settings.ShortcutDuplicate", "两个操作使用了相同的快捷键。" },
                { "Action.IncreaseVolume", "提高音量" },
                { "Action.DecreaseVolume", "降低音量" },
                { "Action.IncreaseBrightness", "提高亮度" },
                { "Action.DecreaseBrightness", "降低亮度" },
                { "Action.ToggleMute", "静音或取消静音" },
                { "Action.TogglePanel", "显示或隐藏面板" },
                { "Hotkey.HoldModifier", "按住此键，再按另一个键" },
                { "Hotkey.RequiresModifier", "需要 Ctrl、Alt、Shift 或 Windows 键" },
                { "Hotkey.NotSet", "未设置" },
                { "Hotkey.Space", "空格" },
                { "Hotkey.PageUp", "Page Up" },
                { "Hotkey.PageDown", "Page Down" },
                { "Hotkey.Windows", "Windows" },
                { "Accessibility.OpenChoices", "打开选项" },

                { "Settings.PanelLayout", "面板布局" },
                { "Settings.OpenPanelAs", "面板打开方式" },
                { "Settings.WhenNotInUse", "不使用时" },
                { "Settings.After", "等待" },
                { "Settings.Seconds", "{0} 秒" },
                { "Settings.DockEdge", "停靠边缘" },
                { "Settings.Screen", "屏幕" },
                { "Settings.OpenEdgeOnHover", "指向屏幕边缘标签时将其打开" },
                { "Settings.ShowEdgeAtStartup", "Quick Controls 启动时显示屏幕边缘标签" },
                { "Settings.ResetPanelPosition", "重置面板位置" },
                { "Settings.PanelPositionReset", "面板位置已重置。" },
                { "Settings.PanelPositionWillReset", "保存更改后，面板位置将重置。" },
                { "Settings.EdgeOptions", "屏幕边缘停靠选项" },
                { "Settings.EdgeOptionsHelp", "选择屏幕边缘布局或相关空闲操作后，即可使用屏幕边缘选项。" },
                { "Layout.Full", "完整面板" },
                { "Layout.HorizontalMini", "横向迷你面板" },
                { "Layout.VerticalMini", "纵向迷你面板" },
                { "Layout.EdgeDock", "停靠在屏幕边缘" },
                { "Idle.KeepCurrent", "保持当前布局" },
                { "Idle.HorizontalMini", "切换到横向迷你面板" },
                { "Idle.VerticalMini", "切换到纵向迷你面板" },
                { "Idle.EdgeTab", "返回屏幕边缘标签" },
                { "Idle.HideToTray", "隐藏到系统托盘" },
                { "Edge.Auto", "自动" },
                { "Edge.Left", "左侧" },
                { "Edge.Right", "右侧" },
                { "Edge.Top", "顶部" },
                { "Edge.Bottom", "底部" },
                { "Screen.RememberLast", "记住上次使用的显示器" },
                { "Screen.Primary", "主显示器" },
                { "EdgeMenu.OpenFullPanel", "打开完整面板" },
                { "EdgeMenu.Settings", "设置" },
                { "EdgeMenu.MoveToEdge", "移动到屏幕边缘" },
                { "EdgeMenu.HideEdgeTab", "将屏幕边缘标签隐藏到系统托盘" },
                { "EdgeMenu.Exit", "退出" },
                { "EdgeTab.ClickToOpen", "单击打开控制面板" },
                { "EdgeTab.ReturnAfterIdle", "{0} 秒后返回屏幕边缘" },

                { "Tray.Tooltip", "Quick Controls" },
                { "Tray.OpenPanel", "打开控制面板" },
                { "Tray.MuteUnmute", "静音 / 取消静音" },
                { "Tray.Settings", "设置" },
                { "Tray.StartWithWindows", "随 Windows 启动" },
                { "Tray.About", "关于" },
                { "Tray.Exit", "退出" },
                { "Tray.Layout", "面板布局" },
                { "Tray.ShowEdgeTab", "显示屏幕边缘标签" },
                { "Tray.HideEdgeTab", "隐藏屏幕边缘标签" },

                { "About.WindowTitle", "关于 Quick Controls" },
                { "About.Version", "版本 {0}" },
                { "About.Description", "一款轻量级 Windows 工具，可快速调节音量和亮度。\r\n\r\n它在系统托盘中静默运行，且无需管理员权限。" },
                { "About.OpenDisplaySettings", "打开 Windows 显示设置" },
                { "About.Close", "关闭" },

                { "Osd.Volume", "音量" },
                { "Osd.Brightness", "亮度" },
                { "Osd.SoundMuted", "已静音" },
                { "Osd.SoundUnmuted", "已取消静音" },

                { "Display.BuiltIn", "内置显示器" },
                { "Display.Laptop", "笔记本电脑显示器" },
                { "Display.LaptopNumber", "笔记本电脑显示器 {0}" },
                { "Display.External", "外接显示器" },
                { "Display.ExternalNumber", "外接显示器 {0}" },

                { "Error.ShortcutInUse", "“{0}”的快捷键已被其他应用使用。" },
                { "Error.StartupNotAllowed", "Windows 不允许更改启动项。" },
                { "Error.SaveSettings", "无法保存设置。请检查可用磁盘空间，然后重试。" },
                { "Error.LanguageUnsupported", "不支持该语言，将改用英语。" },
                { "Notification.ShortcutsUnavailable.Title", "部分快捷键不可用" },
                { "Notification.ShortcutsUnavailable.Message", "某个快捷键已被其他应用使用。请打开“设置”进行更改。" },
                { "Notification.StartupFailed.Title", "无法启用随 Windows 启动" },
                { "Notification.StartupFailed.Message", "应用仍可正常使用。您可以在“设置”中重试。" },
                { "Notification.Ready.Title", "已就绪" },
                { "Notification.Ready.Message", "使用快捷键，或单击时钟附近的图标打开控制面板。" },
                { "Notification.SaveFailed.Title", "无法保存" },
                { "Notification.SaveFailed.Message", "“随 Windows 启动”设置未更改。" },
                { "Notification.StartupChangeFailed.Title", "无法进行更改" },
                { "Notification.StartupChangeFailed.Message", "Windows 不允许更改启动项。" },
                { "Exit.Title", "Quick Controls" },
                { "Exit.Confirmation", "要退出应用吗？\r\n\r\n音量和亮度快捷键将停止工作。" }
            };
        }

        private static Dictionary<string, string> CreateFrench()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "App.Name", "Quick Controls" },
                { "Common.Cancel", "Annuler" },
                { "Common.Close", "Fermer" },
                { "Common.Settings", "Paramètres" },
                { "Common.About", "À propos" },
                { "Common.Exit", "Quitter" },
                { "Common.TryAgain", "Réessayer" },

                { "Panel.Title", "Quick Controls" },
                { "Panel.Volume", "Volume" },
                { "Panel.Brightness", "Luminosité" },
                { "Panel.Mute", "Couper le son" },
                { "Panel.Unmute", "Rétablir le son" },
                { "Panel.Quieter", "Moins fort" },
                { "Panel.Louder", "Plus fort" },
                { "Panel.Dimmer", "Assombrir" },
                { "Panel.Brighter", "Éclaircir" },
                { "Panel.RefreshDisplays", "Actualiser les écrans" },
                { "Panel.DisplaySettings", "Paramètres d’affichage" },
                { "Panel.CompactVolume", "VOLUME" },
                { "Panel.CompactBrightness", "LUMINOSITÉ" },
                { "Panel.AudioUnavailable", "Aucun périphérique de sortie audio n’a été trouvé." },
                { "Panel.VolumeMuted", "Son coupé" },
                { "Panel.VolumePercent.Accessible", "Volume à {0} pour cent" },
                { "Panel.BrightnessPercent.Accessible", "Luminosité à {0} pour cent" },
                { "Panel.BrightnessUnavailable", "Luminosité indisponible" },
                { "Panel.DisplaySearching", "Recherche d’écrans dont la luminosité est réglable..." },
                { "Panel.DisplayUnsupported", "L’application ne peut pas régler la luminosité de cet écran." },
                { "Panel.NoDisplays", "Aucun écran trouvé. Réessayez." },
                { "Panel.Collapse.Accessible", "Passer à un panneau miniature" },
                { "Panel.Expand.Accessible", "Développer le panneau" },
                { "Panel.HideToTray.Accessible", "Masquer dans la zone de notification" },
                { "Panel.Drag.Accessible", "Déplacer le panneau de contrôle" },
                { "Panel.DecreaseVolume.Accessible", "Baisser le volume" },
                { "Panel.IncreaseVolume.Accessible", "Augmenter le volume" },
                { "Panel.Mute.Accessible", "Couper ou rétablir le son" },
                { "Panel.DecreaseBrightness.Accessible", "Baisser la luminosité" },
                { "Panel.IncreaseBrightness.Accessible", "Augmenter la luminosité" },
                { "Panel.HorizontalMini.Accessible", "Mini-panneau de contrôle horizontal" },
                { "Panel.VerticalMini.Accessible", "Mini-panneau de contrôle vertical" },
                { "Panel.EdgeTab.Accessible", "Onglet de bord Quick Controls" },
                { "Panel.EdgeTab.Description", "Ouvrir les commandes de volume et de luminosité depuis le bord de l’écran" },
                { "Panel.ReturnToEdgeTab.Accessible", "Revenir à l’onglet de bord" },
                { "Panel.OpenFull.Accessible", "Ouvrir le panneau de contrôle complet" },

                { "Settings.WindowTitle", "Paramètres — Quick Controls" },
                { "Settings.Title", "Paramètres" },
                { "Settings.Intro", "Sélectionnez un champ de raccourci, puis appuyez sur la combinaison de touches souhaitée." },
                { "Settings.KeyboardShortcuts", "Raccourcis clavier" },
                { "Settings.General", "Général" },
                { "Settings.Interface", "Interface" },
                { "Settings.Language", "Langue" },
                { "Settings.LanguageHelp", "La langue de l’application change après l’enregistrement." },
                { "Settings.InterfaceIntro", "Choisissez la langue et la forme de panneau adaptées à votre utilisation." },
                { "Settings.PanelLayoutHelp", "Choisissez une forme de panneau. Vous pourrez changer de disposition depuis la zone de notification." },
                { "Settings.ActionColumn", "Action" },
                { "Settings.ShortcutColumn", "Raccourci" },
                { "Settings.GeneralIntro", "Configurez le démarrage, le comportement du panneau et le niveau d’ajustement." },
                { "Settings.ChangeAmount", "Incrément de réglage" },
                { "Settings.StartWithWindows", "Ouvrir Quick Controls lorsque j’ouvre une session Windows" },
                { "Settings.AlwaysOnTop", "Garder le panneau au-dessus des autres fenêtres" },
                { "Settings.AutoCollapse", "Passer à la vue miniature en cas d’inactivité" },
                { "Settings.RestoreDefaults", "Rétablir les valeurs par défaut" },
                { "Settings.SaveChanges", "Enregistrer" },
                { "Settings.DefaultsRestored", "Les paramètres par défaut ont été rétablis. Enregistrez les modifications pour les appliquer." },
                { "Settings.ShortcutFor", "Raccourci pour {0}" },
                { "Settings.ShortcutModifierRequired", "Chaque raccourci doit inclure Ctrl, Alt, Maj ou la touche Windows." },
                { "Settings.ShortcutInvalid", "Un raccourci n’est pas valide." },
                { "Settings.ShortcutDuplicate", "Deux actions utilisent le même raccourci." },
                { "Action.IncreaseVolume", "Augmenter le volume" },
                { "Action.DecreaseVolume", "Baisser le volume" },
                { "Action.IncreaseBrightness", "Augmenter la luminosité" },
                { "Action.DecreaseBrightness", "Baisser la luminosité" },
                { "Action.ToggleMute", "Couper ou rétablir le son" },
                { "Action.TogglePanel", "Afficher ou masquer le panneau" },
                { "Hotkey.HoldModifier", "Maintenez cette touche et appuyez sur une autre touche" },
                { "Hotkey.RequiresModifier", "Nécessite Ctrl, Alt, Maj ou la touche Windows" },
                { "Hotkey.NotSet", "Non défini" },
                { "Hotkey.Space", "Espace" },
                { "Hotkey.PageUp", "Page précédente" },
                { "Hotkey.PageDown", "Page suivante" },
                { "Hotkey.Windows", "Windows" },
                { "Accessibility.OpenChoices", "Ouvrir les choix" },

                { "Settings.PanelLayout", "Disposition du panneau" },
                { "Settings.OpenPanelAs", "Ouvrir le panneau sous forme de" },
                { "Settings.WhenNotInUse", "En cas d’inactivité" },
                { "Settings.After", "Après" },
                { "Settings.Seconds", "{0} secondes" },
                { "Settings.DockEdge", "Bord d’ancrage" },
                { "Settings.Screen", "Écran" },
                { "Settings.OpenEdgeOnHover", "Ouvrir l’onglet de bord au passage du pointeur" },
                { "Settings.ShowEdgeAtStartup", "Afficher l’onglet de bord au démarrage de Quick Controls" },
                { "Settings.ResetPanelPosition", "Réinitialiser la position du panneau" },
                { "Settings.PanelPositionReset", "La position du panneau a été réinitialisée." },
                { "Settings.PanelPositionWillReset", "La position du panneau sera réinitialisée après l’enregistrement." },
                { "Settings.EdgeOptions", "Options d’ancrage au bord" },
                { "Settings.EdgeOptionsHelp", "Les options de bord sont disponibles lorsqu’une disposition ou une action d’inactivité liée au bord est sélectionnée." },
                { "Layout.Full", "Panneau complet" },
                { "Layout.HorizontalMini", "Mini-panneau horizontal" },
                { "Layout.VerticalMini", "Mini-panneau vertical" },
                { "Layout.EdgeDock", "Ancré au bord" },
                { "Idle.KeepCurrent", "Conserver la disposition actuelle" },
                { "Idle.HorizontalMini", "Passer au mini-panneau horizontal" },
                { "Idle.VerticalMini", "Passer au mini-panneau vertical" },
                { "Idle.EdgeTab", "Revenir à l’onglet de bord" },
                { "Idle.HideToTray", "Masquer dans la zone de notification" },
                { "Edge.Auto", "Automatique" },
                { "Edge.Left", "Gauche" },
                { "Edge.Right", "Droite" },
                { "Edge.Top", "Haut" },
                { "Edge.Bottom", "Bas" },
                { "Screen.RememberLast", "Mémoriser le dernier écran utilisé" },
                { "Screen.Primary", "Écran principal" },
                { "EdgeMenu.OpenFullPanel", "Ouvrir le panneau complet" },
                { "EdgeMenu.Settings", "Paramètres" },
                { "EdgeMenu.MoveToEdge", "Déplacer vers un bord" },
                { "EdgeMenu.HideEdgeTab", "Masquer l’onglet de bord dans la zone de notification" },
                { "EdgeMenu.Exit", "Quitter" },
                { "EdgeTab.ClickToOpen", "Cliquez pour ouvrir les commandes" },
                { "EdgeTab.ReturnAfterIdle", "Retour au bord après {0} secondes" },

                { "Tray.Tooltip", "Quick Controls" },
                { "Tray.OpenPanel", "Ouvrir le panneau de contrôle" },
                { "Tray.MuteUnmute", "Couper / rétablir le son" },
                { "Tray.Settings", "Paramètres" },
                { "Tray.StartWithWindows", "Démarrer avec Windows" },
                { "Tray.About", "À propos" },
                { "Tray.Exit", "Quitter" },
                { "Tray.Layout", "Disposition du panneau" },
                { "Tray.ShowEdgeTab", "Afficher l’onglet de bord" },
                { "Tray.HideEdgeTab", "Masquer l’onglet de bord" },

                { "About.WindowTitle", "À propos — Quick Controls" },
                { "About.Version", "Version {0}" },
                { "About.Description", "Un utilitaire léger pour régler rapidement le volume et la luminosité sous Windows.\r\n\r\nIl fonctionne discrètement dans la zone de notification et ne nécessite pas de droits d’administrateur." },
                { "About.OpenDisplaySettings", "Ouvrir les paramètres d’affichage de Windows" },
                { "About.Close", "Fermer" },

                { "Osd.Volume", "Volume" },
                { "Osd.Brightness", "Luminosité" },
                { "Osd.SoundMuted", "Son coupé" },
                { "Osd.SoundUnmuted", "Son rétabli" },

                { "Display.BuiltIn", "Écran intégré" },
                { "Display.Laptop", "Écran de l’ordinateur portable" },
                { "Display.LaptopNumber", "Écran de l’ordinateur portable {0}" },
                { "Display.External", "Écran externe" },
                { "Display.ExternalNumber", "Écran externe {0}" },

                { "Error.ShortcutInUse", "Le raccourci pour « {0} » est déjà utilisé par une autre application." },
                { "Error.StartupNotAllowed", "Windows n’a pas autorisé la modification de l’élément de démarrage." },
                { "Error.SaveSettings", "Impossible d’enregistrer les paramètres. Vérifiez l’espace disque disponible, puis réessayez." },
                { "Error.LanguageUnsupported", "Cette langue n’est pas prise en charge. L’anglais sera utilisé à la place." },
                { "Notification.ShortcutsUnavailable.Title", "Certains raccourcis ne sont pas disponibles" },
                { "Notification.ShortcutsUnavailable.Message", "Un raccourci est déjà utilisé par une autre application. Ouvrez les paramètres pour le modifier." },
                { "Notification.StartupFailed.Title", "Impossible d’activer le démarrage avec Windows" },
                { "Notification.StartupFailed.Message", "L’application continuera de fonctionner normalement. Vous pouvez réessayer dans les paramètres." },
                { "Notification.Ready.Title", "Prêt" },
                { "Notification.Ready.Message", "Utilisez un raccourci ou cliquez sur l’icône près de l’horloge pour ouvrir le panneau de contrôle." },
                { "Notification.SaveFailed.Title", "Impossible d’enregistrer" },
                { "Notification.SaveFailed.Message", "Le paramètre de démarrage avec Windows n’a pas été modifié." },
                { "Notification.StartupChangeFailed.Title", "Impossible d’effectuer la modification" },
                { "Notification.StartupChangeFailed.Message", "Windows n’a pas autorisé la modification de l’élément de démarrage." },
                { "Exit.Title", "Quick Controls" },
                { "Exit.Confirmation", "Quitter l’application ?\r\n\r\nLes raccourcis de volume et de luminosité cesseront de fonctionner." }
            };
        }
    }
}
