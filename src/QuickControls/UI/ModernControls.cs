using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

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
                Accent2 = SystemColors.HotTrack;
                Border = SystemColors.ControlDark;
                Danger = Color.Red;
                IsLight = true;
            }
            else if (light)
            {
                Window = Color.FromArgb(245, 247, 252);
                Card = Color.White;
                CardHover = Color.FromArgb(238, 241, 249);
                Text = Color.FromArgb(28, 31, 42);
                MutedText = Color.FromArgb(104, 111, 132);
                Accent = Color.FromArgb(104, 82, 235);
                Accent2 = Color.FromArgb(25, 162, 190);
                Border = Color.FromArgb(222, 226, 238);
                Danger = Color.FromArgb(216, 67, 86);
                IsLight = true;
            }
            else
            {
                Window = Color.FromArgb(18, 20, 28);
                Card = Color.FromArgb(29, 33, 45);
                CardHover = Color.FromArgb(39, 44, 59);
                Text = Color.FromArgb(246, 248, 252);
                MutedText = Color.FromArgb(164, 173, 196);
                Accent = Color.FromArgb(128, 100, 246);
                Accent2 = Color.FromArgb(48, 195, 218);
                Border = Color.FromArgb(52, 57, 74);
                Danger = Color.FromArgb(244, 105, 126);
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
        public static Color Accent2 { get; private set; }
        public static Color Border { get; private set; }
        public static Color Danger { get; private set; }

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
            IntPtr region = CreateRoundRectRgn(
                0, 0, control.Width + 1, control.Height + 1, scaledRadius, scaledRadius);
            control.Region = Region.FromHrgn(region);
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
            CornerRadius = 16;
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
            CornerRadius = 10;
            FillColor = AppColors.CardHover;
            HoverColor = Blend(AppColors.CardHover, AppColors.Accent, 0.22F);
            PressedColor = Blend(AppColors.CardHover, AppColors.Accent, 0.38F);
        }

        public int CornerRadius { get; set; }
        public Color FillColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }

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
            float strokeWidth = Math.Max(1F, scale);
            float inset = strokeWidth / 2F;
            RectangleF bounds = new RectangleF(
                inset, inset, Math.Max(1F, Width - strokeWidth), Math.Max(1F, Height - strokeWidth));
            Color color = !Enabled ? Blend(AppColors.Card, AppColors.MutedText, 0.15F) :
                (_pressed ? PressedColor : (_hovered ? HoverColor : FillColor));
            Color outline = Enabled
                ? Blend(color, AppColors.Text, SystemInformation.HighContrast ? 0.35F : 0.12F)
                : AppColors.Border;
            using (GraphicsPath path = UiHelpers.RoundedRectangle(bounds, CornerRadius * scale))
            using (SolidBrush brush = new SolidBrush(color))
            using (Pen outlinePen = new Pen(outline, strokeWidth))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(outlinePen, path);
            }

            if (Focused && ShowFocusCues)
            {
                float focusInset = Math.Max(2F, 2F * scale);
                RectangleF focusBounds = RectangleF.Inflate(bounds, -focusInset, -focusInset);
                if (focusBounds.Width > 2F && focusBounds.Height > 2F)
                {
                    using (GraphicsPath focusPath = UiHelpers.RoundedRectangle(
                        focusBounds, Math.Max(1F, CornerRadius * scale - focusInset)))
                    using (Pen focusPen = new Pen(Color.FromArgb(180, AppColors.Accent), strokeWidth))
                    {
                        eventArgs.Graphics.DrawPath(focusPen, focusPath);
                    }
                }
            }

            Rectangle textBounds = ClientRectangle;
            if (_pressed) textBounds.Offset(0, Math.Max(1, (int)Math.Round(scale)));
            TextRenderer.DrawText(eventArgs.Graphics, Text, Font, textBounds,
                Enabled ? ForeColor : AppColors.MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
            float trackHeight = Math.Max(4F, 6F * _dpiScale);
            RectangleF track = new RectangleF(
                horizontalPadding, centerY - trackHeight / 2F, trackWidth, trackHeight);
            float fillWidth = trackWidth * Value / 100F;
            float thumbX = horizontalPadding + fillWidth;

            using (SolidBrush trackBrush = new SolidBrush(Enabled ? AppColors.Border : AppColors.CardHover))
            using (SolidBrush fillBrush = new SolidBrush(Enabled ? AccentColor : AppColors.MutedText))
            using (GraphicsPath trackPath = UiHelpers.RoundedRectangle(track, trackHeight / 2F))
            {
                eventArgs.Graphics.FillPath(trackBrush, trackPath);
                if (fillWidth > 0F)
                {
                    RectangleF fill = new RectangleF(
                        horizontalPadding, centerY - trackHeight / 2F, fillWidth, trackHeight);
                    using (GraphicsPath fillPath = UiHelpers.RoundedRectangle(fill, trackHeight / 2F))
                    {
                        eventArgs.Graphics.FillPath(fillBrush, fillPath);
                    }
                }
            }

            bool hot = Enabled && (_hovered || _dragging || Focused);
            float thumbRadius = (hot ? 7.5F : 7F) * _dpiScale;
            float shadowRadius = thumbRadius + _dpiScale;
            Color thumbColor = Enabled ? Color.FromArgb(250, 251, 254) : AppColors.MutedText;
            Color thumbOutline = hot ? AccentColor : AppColors.Border;
            Color shadowColor = SystemInformation.HighContrast
                ? Color.Transparent
                : Color.FromArgb(AppColors.IsLight ? 42 : 82, Color.Black);
            using (SolidBrush shadowBrush = new SolidBrush(shadowColor))
            using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
            using (Pen thumbPen = new Pen(thumbOutline, Math.Max(1F, _dpiScale)))
            {
                eventArgs.Graphics.FillEllipse(shadowBrush,
                    thumbX - shadowRadius, centerY - shadowRadius + _dpiScale,
                    shadowRadius * 2F, shadowRadius * 2F);
                eventArgs.Graphics.FillEllipse(thumbBrush,
                    thumbX - thumbRadius, centerY - thumbRadius,
                    thumbRadius * 2F, thumbRadius * 2F);
                eventArgs.Graphics.DrawEllipse(thumbPen,
                    thumbX - thumbRadius, centerY - thumbRadius,
                    thumbRadius * 2F, thumbRadius * 2F);
            }

            if (Focused && Enabled)
            {
                float focusRadius = thumbRadius + 2F * _dpiScale;
                using (Pen focusPen = new Pen(
                    Color.FromArgb(SystemInformation.HighContrast ? 255 : 175, AccentColor),
                    Math.Max(1F, _dpiScale)))
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
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    logoBounds, AppColors.Accent, AppColors.Accent2, 45F))
                using (Pen pen = new Pen(Color.White, 2.4F))
                {
                    eventArgs.Graphics.FillEllipse(brush, logoBounds);
                    const float centerY = logicalSize / 2F;
                    eventArgs.Graphics.DrawLine(pen, 12F, centerY - 3F, 17F, centerY - 3F);
                    eventArgs.Graphics.DrawLine(pen, 17F, centerY - 3F, 23F, centerY - 8F);
                    eventArgs.Graphics.DrawLine(pen, 23F, centerY - 8F, 23F, centerY + 8F);
                    eventArgs.Graphics.DrawLine(pen, 23F, centerY + 8F, 17F, centerY + 3F);
                    eventArgs.Graphics.DrawLine(pen, 17F, centerY + 3F, 12F, centerY + 3F);
                    eventArgs.Graphics.DrawArc(pen, 22F, centerY - 7F, 11F, 14F, -55F, 110F);
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
            float trackHeight = Math.Max(2F, Height - 3F * scale);
            float top = (Height - trackHeight) / 2F;
            float trackWidth = Math.Max(1F, Width - scale);
            RectangleF all = new RectangleF(horizontalInset, top, trackWidth, trackHeight);
            using (GraphicsPath allPath = UiHelpers.RoundedRectangle(all, trackHeight / 2F))
            using (SolidBrush backBrush = new SolidBrush(AppColors.Border))
            using (SolidBrush fillBrush = new SolidBrush(AccentColor))
            {
                eventArgs.Graphics.FillPath(backBrush, allPath);
                float fillWidth = trackWidth * Value / 100F;
                if (fillWidth > 0F)
                {
                    RectangleF fill = new RectangleF(horizontalInset, top, fillWidth, trackHeight);
                    using (GraphicsPath fillPath = UiHelpers.RoundedRectangle(fill, trackHeight / 2F))
                    {
                        eventArgs.Graphics.FillPath(fillBrush, fillPath);
                    }
                }
            }
        }
    }
}
