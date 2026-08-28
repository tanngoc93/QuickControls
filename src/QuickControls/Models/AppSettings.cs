using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace QuickControls.Models
{
    public enum PanelLayoutMode
    {
        Full = 0,
        HorizontalMini = 1,
        VerticalMini = 2,
        EdgeDock = 3
    }

    public enum PanelDockEdge
    {
        Automatic = 0,
        Left = 1,
        Right = 2
    }

    [Serializable]
    public class AppSettings
    {
        public const int CurrentSettingsVersion = 2;

        public AppSettings()
        {
            SettingsVersion = CurrentSettingsVersion;
            LanguageCode = "en";
            StartWithWindows = true;
            AlwaysOnTop = true;
            AutoCollapse = true;
            StepPercent = 5;
            FirstRun = true;
            PanelLeft = -1;
            PanelTop = -1;
            PanelCompact = false;
            PanelLayoutMode = QuickControls.Models.PanelLayoutMode.Full;
            DockEdge = PanelDockEdge.Automatic;
            SelectedDisplayId = string.Empty;

            VolumeUp = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, Keys.Up);
            VolumeDown = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, Keys.Down);
            BrightnessUp = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, Keys.Right);
            BrightnessDown = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, Keys.Left);
            ToggleMute = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, Keys.M);
            TogglePanel = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, Keys.Space);
        }

        public int SettingsVersion { get; set; }
        public string LanguageCode { get; set; }
        public bool StartWithWindows { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool AutoCollapse { get; set; }
        public int StepPercent { get; set; }
        public bool FirstRun { get; set; }
        public double PanelLeft { get; set; }
        public double PanelTop { get; set; }
        public bool PanelCompact { get; set; }
        public PanelLayoutMode PanelLayoutMode { get; set; }
        public PanelDockEdge DockEdge { get; set; }
        public string SelectedDisplayId { get; set; }

        public HotkeyBinding VolumeUp { get; set; }
        public HotkeyBinding VolumeDown { get; set; }
        public HotkeyBinding BrightnessUp { get; set; }
        public HotkeyBinding BrightnessDown { get; set; }
        public HotkeyBinding ToggleMute { get; set; }
        public HotkeyBinding TogglePanel { get; set; }

        public AppSettings Clone()
        {
            AppSettings copy = new AppSettings();
            copy.SettingsVersion = SettingsVersion;
            copy.LanguageCode = LanguageCode;
            copy.StartWithWindows = StartWithWindows;
            copy.AlwaysOnTop = AlwaysOnTop;
            copy.AutoCollapse = AutoCollapse;
            copy.StepPercent = StepPercent;
            copy.FirstRun = FirstRun;
            copy.PanelLeft = PanelLeft;
            copy.PanelTop = PanelTop;
            copy.PanelCompact = PanelCompact;
            copy.PanelLayoutMode = PanelLayoutMode;
            copy.DockEdge = DockEdge;
            copy.SelectedDisplayId = SelectedDisplayId;
            copy.VolumeUp = SafeClone(VolumeUp);
            copy.VolumeDown = SafeClone(VolumeDown);
            copy.BrightnessUp = SafeClone(BrightnessUp);
            copy.BrightnessDown = SafeClone(BrightnessDown);
            copy.ToggleMute = SafeClone(ToggleMute);
            copy.TogglePanel = SafeClone(TogglePanel);
            return copy;
        }

        public Dictionary<HotkeyAction, HotkeyBinding> GetHotkeys()
        {
            Dictionary<HotkeyAction, HotkeyBinding> result = new Dictionary<HotkeyAction, HotkeyBinding>();
            result[HotkeyAction.VolumeUp] = VolumeUp;
            result[HotkeyAction.VolumeDown] = VolumeDown;
            result[HotkeyAction.BrightnessUp] = BrightnessUp;
            result[HotkeyAction.BrightnessDown] = BrightnessDown;
            result[HotkeyAction.ToggleMute] = ToggleMute;
            result[HotkeyAction.TogglePanel] = TogglePanel;
            return result;
        }

        public HotkeyBinding GetHotkey(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.VolumeUp: return VolumeUp;
                case HotkeyAction.VolumeDown: return VolumeDown;
                case HotkeyAction.BrightnessUp: return BrightnessUp;
                case HotkeyAction.BrightnessDown: return BrightnessDown;
                case HotkeyAction.ToggleMute: return ToggleMute;
                default: return TogglePanel;
            }
        }

        public void SetHotkey(HotkeyAction action, HotkeyBinding binding)
        {
            switch (action)
            {
                case HotkeyAction.VolumeUp: VolumeUp = binding; break;
                case HotkeyAction.VolumeDown: VolumeDown = binding; break;
                case HotkeyAction.BrightnessUp: BrightnessUp = binding; break;
                case HotkeyAction.BrightnessDown: BrightnessDown = binding; break;
                case HotkeyAction.ToggleMute: ToggleMute = binding; break;
                default: TogglePanel = binding; break;
            }
        }

        public static AppSettings CreateDefaults()
        {
            return new AppSettings();
        }

        private static HotkeyBinding SafeClone(HotkeyBinding binding)
        {
            return binding == null ? new HotkeyBinding() : binding.Clone();
        }
    }
}
