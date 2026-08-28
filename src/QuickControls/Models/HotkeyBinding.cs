using System;
using System.Windows.Forms;

namespace QuickControls.Models
{
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Ctrl = 2,
        Shift = 4,
        Win = 8
    }

    public enum HotkeyAction
    {
        VolumeUp = 1,
        VolumeDown = 2,
        BrightnessUp = 3,
        BrightnessDown = 4,
        ToggleMute = 5,
        TogglePanel = 6
    }

    [Serializable]
    public class HotkeyBinding
    {
        public HotkeyBinding()
        {
        }

        public HotkeyBinding(HotkeyModifiers modifiers, Keys key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public HotkeyModifiers Modifiers { get; set; }
        public Keys Key { get; set; }

        public HotkeyBinding Clone()
        {
            return new HotkeyBinding(Modifiers, Key);
        }

        public bool IsValid()
        {
            return Modifiers != HotkeyModifiers.None && Key != Keys.None && !IsModifierKey(Key);
        }

        public bool SameAs(HotkeyBinding other)
        {
            return other != null && other.Modifiers == Modifiers && other.Key == Key;
        }

        public string ToDisplayString()
        {
            if (!IsValid())
            {
                return "Not set";
            }

            string text = string.Empty;
            if ((Modifiers & HotkeyModifiers.Ctrl) != 0) text += "Ctrl + ";
            if ((Modifiers & HotkeyModifiers.Alt) != 0) text += "Alt + ";
            if ((Modifiers & HotkeyModifiers.Shift) != 0) text += "Shift + ";
            if ((Modifiers & HotkeyModifiers.Win) != 0) text += "Windows + ";
            return text + FriendlyKeyName(Key);
        }

        public static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        private static string FriendlyKeyName(Keys key)
        {
            switch (key)
            {
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                case Keys.Space: return "Space";
                case Keys.PageUp: return "Page Up";
                case Keys.PageDown: return "Page Down";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                default: return key.ToString();
            }
        }
    }
}
