using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using QuickControls.Services;

namespace QuickControls.UI
{
    public static class AppColors
    {
        static AppColors()
        {
            bool light = ReadLightTheme();
            if (SystemInformation.HighContrast)
            {
                Window = SystemColors.Window;
                Card = SystemColors.Control;
                CardHover = SystemColors.ControlLight;
                Text = SystemColors.WindowText;
                MutedText = SystemColors.GrayText;
                Accent = SystemColors.Highlight;
                AccentHover = SystemColors.HotTrack;
                AccentPressed = SystemColors.Highlight;
                Accent2 = SystemColors.Highlight;
                Border = SystemColors.ControlDark;
                StrongBorder = SystemColors.WindowText;
                ControlHover = SystemColors.Highlight;
                ControlPressed = SystemColors.HotTrack;
                Focus = SystemColors.Highlight;
                Danger = Color.Red;
                Divider = SystemColors.ControlDark;
                AccentSoft = SystemColors.ControlLight;
                Sidebar = SystemColors.Control;
                SidebarHover = SystemColors.ControlLight;
                SidebarSelected = SystemColors.Highlight;
                SidebarText = SystemColors.HighlightText;
                SidebarMuted = SystemColors.GrayText;
                IsLight = true;
            }
            else if (light)
            {
                Window = Color.FromArgb(244, 246, 248);
                Card = Color.White;
                CardHover = Color.FromArgb(248, 250, 252);
                Text = Color.FromArgb(16, 24, 40);
                MutedText = Color.FromArgb(71, 84, 103);
                Accent = Color.FromArgb(21, 94, 239);
                AccentHover = Color.FromArgb(0, 78, 235);
                AccentPressed = Color.FromArgb(0, 53, 158);
                Accent2 = Color.FromArgb(220, 104, 3);
                Border = Color.FromArgb(208, 213, 221);
                StrongBorder = Color.FromArgb(122, 134, 153);
                ControlHover = Color.FromArgb(239, 244, 255);
                ControlPressed = Color.FromArgb(209, 224, 255);
                Focus = Color.FromArgb(46, 144, 250);
                Danger = Color.FromArgb(217, 45, 32);
                Divider = Color.FromArgb(234, 236, 240);
                AccentSoft = Color.FromArgb(234, 240, 255);
                Sidebar = Color.FromArgb(16, 24, 40);
                SidebarHover = Color.FromArgb(29, 41, 57);
                SidebarSelected = Color.FromArgb(37, 54, 82);
                SidebarText = Color.FromArgb(249, 250, 251);
                SidebarMuted = Color.FromArgb(152, 162, 179);
                IsLight = true;
            }
            else
            {
                Window = Color.FromArgb(11, 16, 24);
                Card = Color.FromArgb(17, 24, 39);
                CardHover = Color.FromArgb(24, 34, 48);
                Text = Color.FromArgb(248, 250, 252);
                MutedText = Color.FromArgb(180, 192, 208);
                Accent = Color.FromArgb(37, 99, 235);
                AccentHover = Color.FromArgb(29, 78, 216);
                AccentPressed = Color.FromArgb(30, 64, 175);
                Accent2 = Color.FromArgb(247, 144, 9);
                Border = Color.FromArgb(122, 134, 153);
                StrongBorder = Color.FromArgb(102, 112, 133);
                ControlHover = Color.FromArgb(36, 52, 77);
                ControlPressed = Color.FromArgb(45, 63, 89);
                Focus = Color.FromArgb(83, 177, 253);
                Danger = Color.FromArgb(249, 112, 102);
                Divider = Color.FromArgb(52, 64, 84);
                AccentSoft = Color.FromArgb(25, 45, 78);
                Sidebar = Color.FromArgb(6, 12, 22);
                SidebarHover = Color.FromArgb(17, 28, 45);
                SidebarSelected = Color.FromArgb(27, 48, 78);
                SidebarText = Color.FromArgb(248, 250, 252);
                SidebarMuted = Color.FromArgb(152, 162, 179);
                IsLight = false;
            }
        }

        public static bool IsLight { get; private set; }
        public static Color Window { get; private set; }
        public static Color Card { get; private set; }
        public static Color CardHover { get; private set; }
        public static Color Text { get; private set; }
        public static Color MutedText { get; private set; }
        public static Color Accent { get; private set; }
        public static Color AccentHover { get; private set; }
        public static Color AccentPressed { get; private set; }
        public static Color Accent2 { get; private set; }
        public static Color Border { get; private set; }
        public static Color StrongBorder { get; private set; }
        public static Color ControlHover { get; private set; }
        public static Color ControlPressed { get; private set; }
        public static Color Focus { get; private set; }
        public static Color Danger { get; private set; }
        public static Color Divider { get; private set; }
        public static Color AccentSoft { get; private set; }
        public static Color Sidebar { get; private set; }
        public static Color SidebarHover { get; private set; }
        public static Color SidebarSelected { get; private set; }
        public static Color SidebarText { get; private set; }
        public static Color SidebarMuted { get; private set; }

        private static bool ReadLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value == null || Convert.ToInt32(value) != 0;
                }
            }
            catch
            {
                return true;
            }
        }
    }

    public static class UiHelpers
    {
        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            return RoundedRectangle(
                new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);
        }

        public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 1F || bounds.Height <= 1F)
            {
                path.AddRectangle(bounds);
                return path;
            }

            radius = Math.Max(0F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2F));
            if (radius < 0.5F)
            {
                path.AddRectangle(bounds);
                return path;
            }

            float diameter = radius * 2F;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static float DpiScale(Graphics graphics)
        {
            if (graphics == null || graphics.DpiX <= 0F) return 1F;
            return Math.Max(1F, graphics.DpiX / 96F);
        }

        public static void PrepareGraphics(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
        }

        public static void ApplyRoundedRegion(Control control, int radius)
        {
            float scale = 1F;
            using (Graphics graphics = control.CreateGraphics())
            {
                scale = DpiScale(graphics);
            }
            int scaledRadius = Math.Max(1, (int)Math.Round(radius * scale));
            int scaledDiameter = scaledRadius * 2;
            IntPtr region = CreateRoundRectRgn(
                0, 0, control.Width + 1, control.Height + 1, scaledDiameter, scaledDiameter);
            Region previousRegion = control.Region;
            control.Region = Region.FromHrgn(region);
            if (previousRegion != null) previousRegion.Dispose();
            DeleteObject(region);
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);
    }

    public class RoundedPanel : Panel
    {
        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            CornerRadius = 3;
            FillColor = AppColors.Card;
            BorderColor = AppColors.Border;
        }

        public int CornerRadius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            // The pixels outside the rounded path still belong to the parent. Paint them
            // first so anti-aliased edge pixels blend with the real surface, not an empty
            // (black) double-buffer.
            base.OnPaintBackground(eventArgs);
            if (Width <= 1 || Height <= 1) return;

            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float scale = UiHelpers.DpiScale(eventArgs.Graphics);
            float strokeWidth = Math.Max(1F, scale);
            float inset = strokeWidth / 2F;
            RectangleF rectangle = new RectangleF(
                inset, inset, Math.Max(1F, Width - strokeWidth), Math.Max(1F, Height - strokeWidth));
            using (GraphicsPath path = UiHelpers.RoundedRectangle(rectangle, CornerRadius * scale))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor, strokeWidth))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(pen, path);
            }
        }
    }

    public class ModernButton : Button
    {
        private bool _hovered;
        private bool _pressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            // ButtonBase is opaque by default. That suppresses OnPaintBackground and leaves
            // the corners outside our custom rounded path uninitialised in the back-buffer.
            SetStyle(ControlStyles.Opaque, false);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Color.Transparent;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            CornerRadius = 3;
            FillColor = AppColors.Card;
            HoverColor = AppColors.ControlHover;
            PressedColor = AppColors.ControlPressed;
            BorderColor = AppColors.Border;
            BorderThickness = 1;
        }

        public int CornerRadius { get; set; }
        public Color FillColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color BorderColor { get; set; }
        public int BorderThickness { get; set; }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            if (Enabled && eventArgs.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(eventArgs);
        }

        protected override void OnMouseCaptureChanged(EventArgs eventArgs)
        {
            if (!Capture && _pressed)
            {
                _pressed = false;
                Invalidate();
            }
            base.OnMouseCaptureChanged(eventArgs);
        }

        protected override void OnGotFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnGotFocus(eventArgs);
        }

        protected override void OnLostFocus(EventArgs eventArgs)
        {
            _pressed = false;
            Invalidate();
            base.OnLostFocus(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;

            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float scale = UiHelpers.DpiScale(eventArgs.Graphics);
            float strokeWidth = BorderThickness <= 0 ? 0F : Math.Max(1F, BorderThickness * scale);
            float inset = strokeWidth / 2F;
            RectangleF bounds = new RectangleF(
                inset, inset, Math.Max(1F, Width - strokeWidth), Math.Max(1F, Height - strokeWidth));
            bool accentSurface = FillColor.ToArgb() == AppColors.Accent.ToArgb();
            Color color;
            if (!Enabled)
            {
                color = Blend(AppColors.Card, AppColors.MutedText, 0.14F);
            }
            else if (accentSurface)
            {
                color = _pressed ? AppColors.AccentPressed :
                    (_hovered ? AppColors.AccentHover : AppColors.Accent);
            }
            else
            {
                color = _pressed ? PressedColor : (_hovered ? HoverColor : FillColor);
            }
            Color outline = !Enabled ? AppColors.Border :
                (accentSurface ? AppColors.AccentPressed : BorderColor);
            using (GraphicsPath path = UiHelpers.RoundedRectangle(bounds, CornerRadius * scale))
            using (SolidBrush brush = new SolidBrush(color))
            {
                eventArgs.Graphics.FillPath(brush, path);
                if (strokeWidth > 0F)
                {
                    using (Pen outlinePen = new Pen(outline, strokeWidth))
                        eventArgs.Graphics.DrawPath(outlinePen, path);
                }
            }

            if (Focused && ShowFocusCues)
            {
                float focusInset = Math.Max(2F, 2F * scale);
                RectangleF focusBounds = RectangleF.Inflate(bounds, -focusInset, -focusInset);
                if (focusBounds.Width > 2F && focusBounds.Height > 2F)
                {
                    using (GraphicsPath focusPath = UiHelpers.RoundedRectangle(
                        focusBounds, Math.Max(1F, CornerRadius * scale - focusInset)))
                    using (Pen focusPen = new Pen(AppColors.Focus, Math.Max(2F, 2F * scale)))
                    {
                        eventArgs.Graphics.DrawPath(focusPen, focusPath);
                    }
                }
            }

            Rectangle textBounds = new Rectangle(
                Padding.Left, Padding.Top,
                Math.Max(1, Width - Padding.Horizontal), Math.Max(1, Height - Padding.Vertical));
            Color textColor = Enabled ? ForeColor : AppColors.MutedText;
            if (SystemInformation.HighContrast && Enabled && (accentSurface || _hovered || _pressed))
            {
                textColor = SystemColors.HighlightText;
            }
            TextFormatFlags textFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine | HorizontalTextFlags(TextAlign);
            TextRenderer.DrawText(eventArgs.Graphics, Text, Font, textBounds, textColor, textFlags);
        }

        private static TextFormatFlags HorizontalTextFlags(ContentAlignment alignment)
        {
            switch (alignment)
            {
                case ContentAlignment.BottomLeft:
                case ContentAlignment.MiddleLeft:
                case ContentAlignment.TopLeft:
                    return TextFormatFlags.Left;
                case ContentAlignment.BottomRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.TopRight:
                    return TextFormatFlags.Right;
                default:
                    return TextFormatFlags.HorizontalCenter;
            }
        }

        private static Color Blend(Color first, Color second, float amount)
        {
            float inverse = 1F - amount;
            return Color.FromArgb(
                (int)(first.A * inverse + second.A * amount),
                (int)(first.R * inverse + second.R * amount),
                (int)(first.G * inverse + second.G * amount),
                (int)(first.B * inverse + second.B * amount));
        }
    }

    public sealed class ModernChoiceBox : Control
    {
        private readonly List<object> _items = new List<object>();
        private int _selectedIndex = -1;
        private bool _hovered;
        private bool _menuOpen;

        public ModernChoiceBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = 38;
        }

        public event EventHandler SelectedIndexChanged;
        public IList<object> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int next = value;
                if (next < -1) next = -1;
                if (next >= _items.Count) next = _items.Count - 1;
                if (_selectedIndex == next) return;
                _selectedIndex = next;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null) handler(this, EventArgs.Empty);
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        public object SelectedItem
        {
            get { return _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null; }
            set { SelectedIndex = value == null ? -1 : _items.IndexOf(value); }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ChoiceAccessibleObject(this);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right ||
                key == Keys.Home || key == Keys.End) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            if (!Enabled) return;
            if (eventArgs.KeyCode == Keys.Down && eventArgs.Alt)
            {
                ShowDropDown();
                eventArgs.Handled = true;
                return;
            }
            if (eventArgs.KeyCode == Keys.Down || eventArgs.KeyCode == Keys.Right)
            {
                if (_items.Count > 0) SelectedIndex = Math.Min(_items.Count - 1, Math.Max(0, SelectedIndex + 1));
                eventArgs.Handled = true;
                return;
            }
            if (eventArgs.KeyCode == Keys.Up || eventArgs.KeyCode == Keys.Left)
            {
                if (_items.Count > 0) SelectedIndex = Math.Max(0, SelectedIndex < 0 ? 0 : SelectedIndex - 1);
                eventArgs.Handled = true;
                return;
            }
            if (eventArgs.KeyCode == Keys.Home)
            {
                if (_items.Count > 0) SelectedIndex = 0;
                eventArgs.Handled = true;
                return;
            }
            if (eventArgs.KeyCode == Keys.End)
            {
                if (_items.Count > 0) SelectedIndex = _items.Count - 1;
                eventArgs.Handled = true;
                return;
            }
            if (eventArgs.KeyCode == Keys.Enter || eventArgs.KeyCode == Keys.Space)
            {
                ShowDropDown();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(eventArgs);
        }

        protected override void OnClick(EventArgs eventArgs)
        {
            Focus();
            ShowDropDown();
            base.OnClick(eventArgs);
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

        protected override void OnGotFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnGotFocus(eventArgs);
        }

        protected override void OnLostFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnLostFocus(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;
            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float scale = UiHelpers.DpiScale(eventArgs.Graphics);
            float stroke = Focused ? Math.Max(2F, 2F * scale) : Math.Max(1F, scale);
            RectangleF bounds = new RectangleF(stroke / 2F, stroke / 2F,
                Math.Max(1F, Width - stroke), Math.Max(1F, Height - stroke));
            Color fill = !Enabled ? AppColors.CardHover :
                ((_hovered || _menuOpen || Focused) ? AppColors.CardHover : AppColors.Card);
            Color border = !Enabled ? AppColors.Border : (Focused ? AppColors.Focus : AppColors.StrongBorder);
            using (GraphicsPath path = UiHelpers.RoundedRectangle(bounds, 3F * scale))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, stroke))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(pen, path);
            }

            string display = SelectedItem == null ? string.Empty : Convert.ToString(SelectedItem);
            int textLeft = (int)Math.Round(12F * scale);
            int rightReserve = (int)Math.Round(48F * scale);
            Rectangle textBounds = new Rectangle(textLeft, 1,
                Math.Max(1, Width - textLeft - rightReserve), Height - 2);
            TextRenderer.DrawText(eventArgs.Graphics, display, Font, textBounds,
                Enabled ? ForeColor : AppColors.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);

            int centerX = Width - (int)Math.Round(20F * scale);
            int centerY = Height / 2;
            using (Pen chevron = new Pen(Enabled ? AppColors.MutedText : AppColors.Border, Math.Max(1.4F, 1.4F * scale)))
            {
                chevron.StartCap = LineCap.Round;
                chevron.EndCap = LineCap.Round;
                eventArgs.Graphics.DrawLine(chevron,
                    centerX - 4F * scale, centerY - 2F * scale,
                    centerX, centerY + 2F * scale);
                eventArgs.Graphics.DrawLine(chevron,
                    centerX, centerY + 2F * scale,
                    centerX + 4F * scale, centerY - 2F * scale);
            }
        }

        private void ShowDropDown()
        {
            if (!Enabled || _items.Count == 0 || _menuOpen) return;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.BackColor = AppColors.Card;
            menu.ForeColor = AppColors.Text;
            menu.Font = Font;
            menu.Padding = new Padding(2);
            for (int index = 0; index < _items.Count; index++)
            {
                int itemIndex = index;
                ToolStripMenuItem item = new ToolStripMenuItem(Convert.ToString(_items[index]));
                item.Checked = index == _selectedIndex;
                item.AutoSize = false;
                item.Size = new Size(Math.Max(Width - 4, 180), 34);
                item.Click += delegate { SelectedIndex = itemIndex; };
                menu.Items.Add(item);
            }
            menu.Closed += delegate
            {
                _menuOpen = false;
                Invalidate();
                menu.Dispose();
            };
            _menuOpen = true;
            Invalidate();
            menu.Show(this, new Point(0, Height + 2));
        }

        private sealed class ChoiceAccessibleObject : ControlAccessibleObject
        {
            private readonly ModernChoiceBox _owner;
            public ChoiceAccessibleObject(ModernChoiceBox owner) : base(owner) { _owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.ComboBox; } }
            public override string DefaultAction { get { return AppText.Get("Accessibility.OpenChoices"); } }
            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates states = base.State;
                    states |= _owner._menuOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                    return states;
                }
            }
            public override string Value
            {
                get { return _owner.SelectedItem == null ? string.Empty : Convert.ToString(_owner.SelectedItem); }
                set { }
            }
            public override void DoDefaultAction() { _owner.ShowDropDown(); }
        }
    }

    public sealed class ModernToggle : Control
    {
        private bool _checked;
        private bool _hovered;

        public ModernToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = 42;
        }

        public event EventHandler CheckedChanged;
        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null) handler(this, EventArgs.Empty);
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ToggleAccessibleObject(this);
        }

        protected override void OnClick(EventArgs eventArgs)
        {
            if (Enabled)
            {
                Focus();
                Checked = !Checked;
            }
            base.OnClick(eventArgs);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            if (Enabled && eventArgs.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(eventArgs);
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

        protected override void OnGotFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnGotFocus(eventArgs);
        }

        protected override void OnLostFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnLostFocus(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;
            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float scale = UiHelpers.DpiScale(eventArgs.Graphics);
            int textReserve = (int)Math.Round(58F * scale);
            Rectangle textBounds = new Rectangle(0, 0, Math.Max(1, Width - textReserve), Height);
            TextRenderer.DrawText(eventArgs.Graphics, Text, Font, textBounds,
                Enabled ? ForeColor : AppColors.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);

            int switchWidth = Math.Max(32, (int)Math.Round(42F * scale));
            int switchHeight = Math.Max(18, (int)Math.Round(22F * scale));
            RectangleF switchBounds = new RectangleF(Width - switchWidth - 2F * scale,
                (Height - switchHeight) / 2F, switchWidth, switchHeight);
            Color trackColor = Checked ? AppColors.Accent : (_hovered ? AppColors.StrongBorder : AppColors.Border);
            if (!Enabled) trackColor = AppColors.Border;
            using (GraphicsPath trackPath = UiHelpers.RoundedRectangle(switchBounds, switchHeight / 2F))
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
                eventArgs.Graphics.FillPath(trackBrush, trackPath);

            float thumbSize = 16F * scale;
            float thumbInset = 3F * scale;
            float thumbX = Checked ? switchBounds.Right - thumbSize - thumbInset : switchBounds.Left + thumbInset;
            float thumbY = switchBounds.Top + (switchHeight - thumbSize) / 2F;
            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
                eventArgs.Graphics.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);

            if (Focused && ShowFocusCues)
            {
                RectangleF focusBounds = RectangleF.Inflate(switchBounds, 3F * scale, 3F * scale);
                using (GraphicsPath focusPath = UiHelpers.RoundedRectangle(
                    focusBounds, switchHeight / 2F + 3F * scale))
                using (Pen focusPen = new Pen(AppColors.Focus, Math.Max(2F, 2F * scale)))
                    eventArgs.Graphics.DrawPath(focusPen, focusPath);
            }
        }

        private sealed class ToggleAccessibleObject : ControlAccessibleObject
        {
            private readonly ModernToggle _owner;
            public ToggleAccessibleObject(ModernToggle owner) : base(owner) { _owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.CheckButton; } }
            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates states = base.State;
                    if (_owner.Checked) states |= AccessibleStates.Checked;
                    return states;
                }
            }
            public override void DoDefaultAction() { _owner.Checked = !_owner.Checked; }
        }
    }

    public class ModernSlider : Control
    {
        private int _value;
        private bool _dragging;
        private bool _hovered;
        private float _dpiScale = 1F;

        public ModernSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 34;
            TabStop = true;
            AccentColor = AppColors.Accent;
        }

        public event EventHandler UserValueChanged;
        public Color AccentColor { get; set; }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (_value == clamped) return;
                _value = clamped;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new SliderAccessibleObject(this);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down ||
                key == Keys.Home || key == Keys.End || key == Keys.PageUp || key == Keys.PageDown)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            int next = Value;
            switch (eventArgs.KeyCode)
            {
                case Keys.Left:
                case Keys.Down: next -= 2; break;
                case Keys.Right:
                case Keys.Up: next += 2; break;
                case Keys.PageDown: next -= 10; break;
                case Keys.PageUp: next += 10; break;
                case Keys.Home: next = 0; break;
                case Keys.End: next = 100; break;
                default: base.OnKeyDown(eventArgs); return;
            }
            SetFromUser(next);
            eventArgs.Handled = true;
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            if (!Enabled || eventArgs.Button != MouseButtons.Left) return;
            Focus();
            _dragging = true;
            Capture = true;
            SetFromMouse(eventArgs.X);
            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            if (_dragging) SetFromMouse(eventArgs.X);
            base.OnMouseMove(eventArgs);
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                SetFromMouse(eventArgs.X);
            }
            base.OnMouseUp(eventArgs);
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

        protected override void OnMouseCaptureChanged(EventArgs eventArgs)
        {
            if (!Capture && _dragging)
            {
                _dragging = false;
                Invalidate();
            }
            base.OnMouseCaptureChanged(eventArgs);
        }

        protected override void OnGotFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnGotFocus(eventArgs);
        }

        protected override void OnLostFocus(EventArgs eventArgs)
        {
            Invalidate();
            base.OnLostFocus(eventArgs);
        }

        protected override void OnMouseWheel(MouseEventArgs eventArgs)
        {
            if (Enabled) SetFromUser(Value + (eventArgs.Delta > 0 ? 5 : -5));
            base.OnMouseWheel(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;

            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            _dpiScale = UiHelpers.DpiScale(eventArgs.Graphics);
            float horizontalPadding = 9F * _dpiScale;
            float trackWidth = Math.Max(1F, Width - horizontalPadding * 2F);
            float centerY = Height / 2F;
            float trackHeight = Math.Max(3F, 4F * _dpiScale);
            float trackRadius = Math.Min(trackHeight / 2F, Math.Max(1F, 1.5F * _dpiScale));
            RectangleF track = new RectangleF(
                horizontalPadding, centerY - trackHeight / 2F, trackWidth, trackHeight);
            float fillWidth = trackWidth * Value / 100F;
            float thumbX = horizontalPadding + fillWidth;

            using (SolidBrush trackBrush = new SolidBrush(Enabled ? AppColors.Border : AppColors.CardHover))
            using (SolidBrush fillBrush = new SolidBrush(Enabled ? AccentColor : AppColors.MutedText))
            using (GraphicsPath trackPath = UiHelpers.RoundedRectangle(track, trackRadius))
            {
                eventArgs.Graphics.FillPath(trackBrush, trackPath);
                if (fillWidth > 0F)
                {
                    RectangleF fill = new RectangleF(
                        horizontalPadding, centerY - trackHeight / 2F, fillWidth, trackHeight);
                    using (GraphicsPath fillPath = UiHelpers.RoundedRectangle(fill, trackRadius))
                    {
                        eventArgs.Graphics.FillPath(fillBrush, fillPath);
                    }
                }
            }

            bool hot = Enabled && (_hovered || _dragging || Focused);
            float thumbRadius = (hot ? 6.5F : 6F) * _dpiScale;
            Color thumbColor = !Enabled ? AppColors.CardHover :
                (AppColors.IsLight ? AppColors.Card : AppColors.Text);
            Color thumbOutline = hot ? AccentColor : AppColors.StrongBorder;
            using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
            using (Pen thumbPen = new Pen(thumbOutline, Math.Max(1F, 2F * _dpiScale)))
            {
                eventArgs.Graphics.FillEllipse(thumbBrush,
                    thumbX - thumbRadius, centerY - thumbRadius,
                    thumbRadius * 2F, thumbRadius * 2F);
                eventArgs.Graphics.DrawEllipse(thumbPen,
                    thumbX - thumbRadius, centerY - thumbRadius,
                    thumbRadius * 2F, thumbRadius * 2F);
            }

            if (Focused && Enabled)
            {
                float focusRadius = thumbRadius + 3F * _dpiScale;
                using (Pen focusPen = new Pen(AppColors.Focus, Math.Max(1F, 2F * _dpiScale)))
                {
                    eventArgs.Graphics.DrawEllipse(focusPen,
                        thumbX - focusRadius, centerY - focusRadius,
                        focusRadius * 2F, focusRadius * 2F);
                }
            }
        }

        private void SetFromMouse(int x)
        {
            float horizontalPadding = 9F * Math.Max(1F, _dpiScale);
            float usableWidth = Math.Max(1F, Width - horizontalPadding * 2F);
            int percent = (int)Math.Round((x - horizontalPadding) * 100D / usableWidth);
            SetFromUser(percent);
        }

        private void SetFromUser(int value)
        {
            Value = value;
            EventHandler handler = UserValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private sealed class SliderAccessibleObject : ControlAccessibleObject
        {
            private readonly ModernSlider _owner;

            public SliderAccessibleObject(ModernSlider owner) : base(owner)
            {
                _owner = owner;
            }

            public override AccessibleRole Role { get { return AccessibleRole.Slider; } }
            public override string Value
            {
                get { return _owner.Value + "%"; }
                set { }
            }
        }
    }

    public sealed class LogoControl : Control
    {
        public LogoControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(44, 44);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;

            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            const float logicalSize = 44F;
            float scale = Math.Min(Width / logicalSize, Height / logicalSize);
            float offsetX = (Width - logicalSize * scale) / 2F;
            float offsetY = (Height - logicalSize * scale) / 2F;
            GraphicsState state = eventArgs.Graphics.Save();
            try
            {
                eventArgs.Graphics.TranslateTransform(offsetX, offsetY);
                eventArgs.Graphics.ScaleTransform(scale, scale);
                RectangleF logoBounds = new RectangleF(1F, 1F, 42F, 42F);
                Color glyphColor = SystemInformation.HighContrast
                    ? SystemColors.HighlightText
                    : Color.White;
                using (GraphicsPath logoPath = UiHelpers.RoundedRectangle(logoBounds, 6F))
                using (SolidBrush backgroundBrush = new SolidBrush(AppColors.Accent))
                using (SolidBrush glyphBrush = new SolidBrush(glyphColor))
                using (Pen wavePen = new Pen(glyphColor, 2.4F))
                {
                    eventArgs.Graphics.FillPath(backgroundBrush, logoPath);
                    PointF[] speaker = new PointF[]
                    {
                        new PointF(11F, 19F),
                        new PointF(16F, 19F),
                        new PointF(22F, 14F),
                        new PointF(24F, 14F),
                        new PointF(24F, 30F),
                        new PointF(22F, 30F),
                        new PointF(16F, 25F),
                        new PointF(11F, 25F)
                    };
                    eventArgs.Graphics.FillPolygon(glyphBrush, speaker);
                    wavePen.StartCap = LineCap.Round;
                    wavePen.EndCap = LineCap.Round;
                    eventArgs.Graphics.DrawArc(wavePen, 21F, 15F, 13F, 14F, -48F, 96F);
                }
            }
            finally
            {
                eventArgs.Graphics.Restore(state);
            }
        }
    }

    public sealed class ModernProgress : Control
    {
        private int _value;
        public ModernProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 10;
            AccentColor = AppColors.Accent;
        }
        public Color AccentColor { get; set; }
        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width <= 1 || Height <= 1) return;

            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float scale = UiHelpers.DpiScale(eventArgs.Graphics);
            float horizontalInset = scale / 2F;
            float trackHeight = Math.Max(3F, Math.Min(4F * scale, Height - 2F * scale));
            float top = (Height - trackHeight) / 2F;
            float trackWidth = Math.Max(1F, Width - scale);
            RectangleF all = new RectangleF(horizontalInset, top, trackWidth, trackHeight);
            float trackRadius = Math.Min(trackHeight / 2F, Math.Max(1F, scale));
            using (GraphicsPath allPath = UiHelpers.RoundedRectangle(all, trackRadius))
            using (SolidBrush backBrush = new SolidBrush(AppColors.Border))
            using (SolidBrush fillBrush = new SolidBrush(AccentColor))
            {
                eventArgs.Graphics.FillPath(backBrush, allPath);
                float fillWidth = trackWidth * Value / 100F;
                if (fillWidth > 0F)
                {
                    RectangleF fill = new RectangleF(horizontalInset, top, fillWidth, trackHeight);
                    using (GraphicsPath fillPath = UiHelpers.RoundedRectangle(fill, trackRadius))
                    {
                        eventArgs.Graphics.FillPath(fillBrush, fillPath);
                    }
                }
            }
        }
    }
}
