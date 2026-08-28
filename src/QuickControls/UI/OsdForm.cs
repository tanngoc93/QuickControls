using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Services;

namespace QuickControls.UI
{
    public sealed class OsdForm : Form
    {
        private readonly Label _titleLabel;
        private readonly Label _valueLabel;
        private readonly ModernProgress _progress;
        private readonly Timer _hideTimer;
        private readonly Timer _fadeTimer;

        public OsdForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = AppColors.Card;
            ClientSize = new Size(370, 104);
            Font = AppText.CreateFont(10F, FontStyle.Regular);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            _titleLabel = new Label();
            _titleLabel.Font = AppText.CreateFont(11F, FontStyle.Bold);
            _titleLabel.ForeColor = AppColors.Text;
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.SetBounds(24, 18, 245, 26);
            Controls.Add(_titleLabel);

            _valueLabel = new Label();
            _valueLabel.Font = AppText.CreateFont(14F, FontStyle.Bold);
            _valueLabel.ForeColor = AppColors.Text;
            _valueLabel.TextAlign = ContentAlignment.MiddleRight;
            _valueLabel.BackColor = Color.Transparent;
            _valueLabel.SetBounds(270, 14, 76, 34);
            Controls.Add(_valueLabel);

            _progress = new ModernProgress();
            _progress.SetBounds(24, 61, 322, 12);
            Controls.Add(_progress);

            _hideTimer = new Timer();
            _hideTimer.Interval = 1350;
            _hideTimer.Tick += StartFade;

            _fadeTimer = new Timer();
            _fadeTimer.Interval = 35;
            _fadeTimer.Tick += FadeTick;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x00000080;
                return parameters;
            }
        }

        public void ShowValue(string title, int value, bool alternateAccent)
        {
            _titleLabel.Text = title;
            _valueLabel.Text = Math.Max(0, Math.Min(100, value)) + "%";
            _progress.Value = value;
            _progress.AccentColor = alternateAccent ? AppColors.Accent2 : AppColors.Accent;
            PositionNearTaskbar();
            _hideTimer.Stop();
            _fadeTimer.Stop();
            Opacity = 0.97D;
            if (!Visible) Show();
            else Invalidate(true);
            SetWindowPos(Handle, new IntPtr(-1), Left, Top, Width, Height, 0x0010 | 0x0040);
            _hideTimer.Start();
        }

        public void ApplyLanguage()
        {
            Font oldTitleFont = _titleLabel.Font;
            Font oldValueFont = _valueLabel.Font;
            _titleLabel.Font = AppText.CreateFont(11F, FontStyle.Bold);
            _valueLabel.Font = AppText.CreateFont(14F, FontStyle.Bold);
            if (oldTitleFont != null) oldTitleFont.Dispose();
            if (oldValueFont != null) oldValueFont.Dispose();
        }

        public void ShowMuted(bool muted, int volume)
        {
            ShowValue(muted ? AppText.Get("Osd.SoundMuted") : AppText.Get("Osd.SoundUnmuted"),
                muted ? 0 : volume, false);
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (Width > 0 && Height > 0) UiHelpers.ApplyRoundedRegion(this, 4);
        }

        private void PositionNearTaskbar()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle working = screen.WorkingArea;
            Location = new Point(working.Left + (working.Width - Width) / 2, working.Bottom - Height - 42);
        }

        private void StartFade(object sender, EventArgs eventArgs)
        {
            _hideTimer.Stop();
            if (SystemInformation.HighContrast)
            {
                Hide();
                return;
            }
            _fadeTimer.Start();
        }

        private void FadeTick(object sender, EventArgs eventArgs)
        {
            Opacity -= 0.12D;
            if (Opacity <= 0.08D)
            {
                _fadeTimer.Stop();
                Hide();
                Opacity = 0.97D;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
