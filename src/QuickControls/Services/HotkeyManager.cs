using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Models;

namespace QuickControls.Services
{
    public sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;
        private readonly List<int> _registeredIds;
        private bool _disposed;

        public HotkeyManager()
        {
            _registeredIds = new List<int>();
            CreateParams parameters = new CreateParams();
            parameters.Caption = "QuickControls.Hotkeys";
            parameters.Parent = new IntPtr(-3);
            CreateHandle(parameters);
        }

        public event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;

        public IList<HotkeyAction> RegisterAll(AppSettings settings)
        {
            UnregisterAll();
            List<HotkeyAction> failures = new List<HotkeyAction>();
            Dictionary<HotkeyAction, HotkeyBinding> bindings = settings.GetHotkeys();
            foreach (KeyValuePair<HotkeyAction, HotkeyBinding> pair in bindings)
            {
                uint nativeModifiers = ToNativeModifiers(pair.Value == null ? HotkeyModifiers.None : pair.Value.Modifiers);
                if (pair.Key == HotkeyAction.ToggleMute || pair.Key == HotkeyAction.TogglePanel)
                {
                    nativeModifiers |= ModNoRepeat;
                }

                if (pair.Value == null || !pair.Value.IsValid() ||
                    !RegisterHotKey(Handle, (int)pair.Key, nativeModifiers, (uint)pair.Value.Key))
                {
                    failures.Add(pair.Key);
                }
                else
                {
                    _registeredIds.Add((int)pair.Key);
                }
            }

            return failures;
        }

        public void UnregisterAll()
        {
            for (int index = 0; index < _registeredIds.Count; index++)
            {
                UnregisterHotKey(Handle, _registeredIds[index]);
            }
            _registeredIds.Clear();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey)
            {
                EventHandler<HotkeyPressedEventArgs> handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(this, new HotkeyPressedEventArgs((HotkeyAction)message.WParam.ToInt32()));
                }
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnregisterAll();
            DestroyHandle();
        }

        private static uint ToNativeModifiers(HotkeyModifiers modifiers)
        {
            uint value = 0;
            if ((modifiers & HotkeyModifiers.Alt) != 0) value |= 0x0001;
            if ((modifiers & HotkeyModifiers.Ctrl) != 0) value |= 0x0002;
            if ((modifiers & HotkeyModifiers.Shift) != 0) value |= 0x0004;
            if ((modifiers & HotkeyModifiers.Win) != 0) value |= 0x0008;
            return value;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
    }

    public sealed class HotkeyPressedEventArgs : EventArgs
    {
        public HotkeyPressedEventArgs(HotkeyAction action)
        {
            Action = action;
        }

        public HotkeyAction Action { get; private set; }
    }
}
