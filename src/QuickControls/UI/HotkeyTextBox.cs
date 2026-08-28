using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Models;
using QuickControls.Services;

namespace QuickControls.UI
{
    public sealed class HotkeyTextBox : Control
    {
        private HotkeyBinding _binding;
        private string _displayText;
        private bool _hovered;
        private bool _invalid;
        private bool _showCapturePrompt;

        public HotkeyTextBox()
        {
            _binding = new HotkeyBinding();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = AppColors.Text;
            Font = AppText.CreateFont(9F, FontStyle.Bold);
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = 36;
            UpdateDisplay();
        }

        public event EventHandler InvalidCombination;

        public HotkeyBinding Binding
        {
            get { return _binding.Clone(); }
            set
            {
                _binding = value == null ? new HotkeyBinding() : value.Clone();
                _invalid = false;
                _showCapturePrompt = false;
                UpdateDisplay();
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new HotkeyAccessibleObject(this);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Tab) return false;
            return true;
        }

        protected override void OnEnter(EventArgs eventArgs)
        {
            _invalid = false;
            _showCapturePrompt = false;
            Invalidate();
            base.OnEnter(eventArgs);
        }

        protected override void OnLeave(EventArgs eventArgs)
        {
            _invalid = false;
            _showCapturePrompt = false;
            UpdateDisplay();
            base.OnLeave(eventArgs);
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            Focus();
            _showCapturePrompt = true;
            _displayText = AppText.Get("Hotkey.HoldModifier");
            Invalidate();
            base.OnMouseDown(eventArgs);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            if (eventArgs.KeyCode == Keys.Escape)
            {
                _invalid = false;
                _showCapturePrompt = false;
                UpdateDisplay();
                if (Parent != null) Parent.Focus();
                return;
            }

            if (HotkeyBinding.IsModifierKey(eventArgs.KeyCode))
            {
                _displayText = AppText.Get("Hotkey.HoldModifier");
                _showCapturePrompt = true;
                Invalidate();
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
                _invalid = true;
                _showCapturePrompt = false;
                _displayText = AppText.Get("Hotkey.RequiresModifier");
                Invalidate();
                EventHandler handler = InvalidCombination;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }

            _binding = candidate;
            _invalid = false;
            _showCapturePrompt = false;
            UpdateDisplay();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;
            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float scale = UiHelpers.DpiScale(eventArgs.Graphics);
            float stroke = Focused ? Math.Max(2F, 2F * scale) : Math.Max(1F, scale);
            RectangleF bounds = new RectangleF(stroke / 2F, stroke / 2F,
                Math.Max(1F, Width - stroke), Math.Max(1F, Height - stroke));
            Color fill = _invalid
                ? (AppColors.IsLight ? Color.FromArgb(255, 244, 244) : Color.FromArgb(75, 36, 47))
                : ((Focused || _hovered) ? AppColors.AccentSoft : AppColors.Card);
            Color border = _invalid ? AppColors.Danger : (Focused ? AppColors.Focus : AppColors.StrongBorder);
            using (GraphicsPath path = UiHelpers.RoundedRectangle(bounds, 3F * scale))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border, stroke))
            {
                eventArgs.Graphics.FillPath(fillBrush, path);
                eventArgs.Graphics.DrawPath(borderPen, path);
            }

            if (_invalid || _showCapturePrompt || string.IsNullOrEmpty(_displayText))
            {
                int messagePadding = (int)Math.Round(10F * scale);
                using (Font messageFont = AppText.CreateFont(8.5F, FontStyle.Regular))
                {
                    TextRenderer.DrawText(eventArgs.Graphics, _displayText, messageFont,
                        new Rectangle(messagePadding, 1,
                            Math.Max(1, Width - messagePadding * 2), Height - 2),
                        _invalid ? AppColors.Danger : AppColors.MutedText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
                return;
            }

            DrawKeycaps(eventArgs.Graphics, _displayText, scale);
        }

        private void DrawKeycaps(Graphics graphics, string text, float scale)
        {
            string[] tokens = text.Split(new string[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length <= 1)
            {
                int textPadding = (int)Math.Round(10F * scale);
                TextRenderer.DrawText(graphics, text, Font,
                    new Rectangle(textPadding, 1,
                        Math.Max(1, Width - textPadding * 2), Height - 2), ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                return;
            }

            int[] widths = new int[tokens.Length];
            int totalWidth = 0;
            for (int index = 0; index < tokens.Length; index++)
            {
                widths[index] = TextRenderer.MeasureText(tokens[index], Font,
                    new Size(int.MaxValue, Height), TextFormatFlags.NoPadding).Width +
                    (int)Math.Round(14F * scale);
                totalWidth += widths[index];
                if (index > 0) totalWidth += (int)Math.Round(15F * scale);
            }
            int outerPadding = (int)Math.Round(8F * scale);
            if (totalWidth > Width - outerPadding * 2)
            {
                TextRenderer.DrawText(graphics, text, Font,
                    new Rectangle(outerPadding, 1,
                        Math.Max(1, Width - outerPadding * 2), Height - 2), ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                return;
            }

            int x = (Width - totalWidth) / 2;
            int keyHeight = Math.Max(20, (int)Math.Round(24F * scale));
            int y = (Height - keyHeight) / 2;
            int separatorWidth = Math.Max(12, (int)Math.Round(15F * scale));
            using (Font separatorFont = AppText.CreateFont(8.5F, FontStyle.Bold))
            {
                for (int index = 0; index < tokens.Length; index++)
                {
                    Rectangle keyBounds = new Rectangle(x, y, widths[index], keyHeight);
                    using (GraphicsPath keyPath = UiHelpers.RoundedRectangle(keyBounds, 3F * scale))
                    using (SolidBrush keyBrush = new SolidBrush(AppColors.CardHover))
                    using (Pen keyPen = new Pen(AppColors.Border, Math.Max(1F, scale)))
                    {
                        graphics.FillPath(keyBrush, keyPath);
                        graphics.DrawPath(keyPen, keyPath);
                    }
                    TextRenderer.DrawText(graphics, tokens[index], Font, keyBounds, ForeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    x += widths[index];
                    if (index < tokens.Length - 1)
                    {
                        TextRenderer.DrawText(graphics, "+", separatorFont,
                            new Rectangle(x, y, separatorWidth, keyHeight), AppColors.MutedText,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                        x += separatorWidth;
                    }
                }
            }
        }

        private void UpdateDisplay()
        {
            _displayText = _binding.ToDisplayString();
            Text = _displayText;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        private static bool IsKeyPressed(int virtualKey)
        {
            return (GetKeyState(virtualKey) & 0x8000) != 0;
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        private sealed class HotkeyAccessibleObject : ControlAccessibleObject
        {
            private readonly HotkeyTextBox _owner;
            public HotkeyAccessibleObject(HotkeyTextBox owner) : base(owner) { _owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.Text; } }
            public override string Value
            {
                get { return _owner._binding.ToDisplayString(); }
                set { }
            }
        }
    }
}
