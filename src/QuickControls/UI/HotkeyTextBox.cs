using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Models;

namespace QuickControls.UI
{
    public sealed class HotkeyTextBox : TextBox
    {
        private HotkeyBinding _binding;

        public HotkeyTextBox()
        {
            _binding = new HotkeyBinding();
            ReadOnly = true;
            ShortcutsEnabled = false;
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = AppColors.CardHover;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            TextAlign = HorizontalAlignment.Center;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public event EventHandler InvalidCombination;

        public HotkeyBinding Binding
        {
            get { return _binding.Clone(); }
            set
            {
                _binding = value == null ? new HotkeyBinding() : value.Clone();
                UpdateDisplay();
            }
        }

        protected override void OnEnter(EventArgs eventArgs)
        {
            BackColor = AppColors.CardHover;
            SelectAll();
            base.OnEnter(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            Focus();
            SelectAll();
            base.OnMouseDown(eventArgs);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            if (eventArgs.KeyCode == Keys.Escape)
            {
                UpdateDisplay();
                Parent.Focus();
                return;
            }

            if (HotkeyBinding.IsModifierKey(eventArgs.KeyCode))
            {
                Text = "Hold this key and press another key";
                return;
            }

            HotkeyModifiers modifiers = HotkeyModifiers.None;
            if (eventArgs.Control) modifiers |= HotkeyModifiers.Ctrl;
            if (eventArgs.Alt) modifiers |= HotkeyModifiers.Alt;
            if (eventArgs.Shift) modifiers |= HotkeyModifiers.Shift;
            if (IsKeyPressed(0x5B) || IsKeyPressed(0x5C)) modifiers |= HotkeyModifiers.Win;

            HotkeyBinding candidate = new HotkeyBinding(modifiers, eventArgs.KeyCode);
            if (!candidate.IsValid())
            {
                BackColor = AppColors.IsLight
                    ? Color.FromArgb(255, 232, 236)
                    : Color.FromArgb(75, 36, 47);
                Text = "Requires Ctrl, Alt, Shift, or Windows";
                EventHandler handler = InvalidCombination;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }

            _binding = candidate;
            BackColor = AppColors.CardHover;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            Text = _binding.ToDisplayString();
            Select(0, 0);
        }

        private static bool IsKeyPressed(int virtualKey)
        {
            return (GetKeyState(virtualKey) & 0x8000) != 0;
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);
    }
}
