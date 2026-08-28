using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using QuickControls.Models;
using QuickControls.Services;

namespace QuickControls.UI
{
    public sealed class HardwareMonitorForm : Form
    {
        public const int LogicalWidth = 920;
        public const int LogicalHeight = 620;

        private readonly IHardwareMonitorService _service;
        private readonly bool _ownsService;
        private readonly bool _sampleAutomatically;
        private readonly System.Windows.Forms.Timer _sampleTimer;
        private readonly Label _titleLabel;
        private readonly Label _introLabel;
        private readonly Label _liveLabel;
        private readonly Label _statusLabel;
        private readonly ModernButton _minimizeButton;
        private readonly ModernButton _closeButton;
        private readonly Panel _rootPanel;
        private readonly HardwareMetricCard _cpuCard;
        private readonly HardwareMetricCard _gpuCard;
        private readonly HardwareMetricCard _memoryCard;
        private readonly HardwareMetricCard _storageCard;
        private int _reading;
        private int _monitorGeneration;
        private int _serviceDisposeQueued;
        private bool _monitoring;
        private bool _allowClose;
        private bool _closed;
        private float _fitFontScale = 1F;
        private float _lastFitDpi;
        private Rectangle _lastFitArea = Rectangle.Empty;
        private HardwareSnapshot _lastSnapshot;

        public HardwareMonitorForm()
            : this(new HardwareMonitorService(), true, true)
        {
        }

        internal HardwareMonitorForm(IHardwareMonitorService service, bool ownsService, bool sampleAutomatically)
        {
            _service = service;
            _ownsService = ownsService;
            _sampleAutomatically = sampleAutomatically;
            Text = AppText.Get("Hardware.WindowTitle");
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            ClientSize = new Size(LogicalWidth, LogicalHeight);
            MinimumSize = new Size(760, 540);
            BackColor = AppColors.Window;
            ForeColor = AppColors.Text;
            Font = AppText.CreateFont(10F, FontStyle.Regular);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Escape) Close();
            };
            _rootPanel = new Panel();
            Panel root = _rootPanel;
            root.Dock = DockStyle.Fill;
            root.BackColor = AppColors.Window;
            root.Paint += PaintRootBorder;
            Controls.Add(root);

            Panel accent = new Panel();
            accent.BackColor = AppColors.Accent;
            accent.Dock = DockStyle.Top;
            accent.Height = 4;
            root.Controls.Add(accent);

            Panel titleBar = new Panel();
            titleBar.SetBounds(0, 4, LogicalWidth, 68);
            titleBar.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            titleBar.BackColor = AppColors.Card;
            titleBar.MouseDown += DragWindow;
            root.Controls.Add(titleBar);

            ActivityMark activity = new ActivityMark();
            activity.SetBounds(24, 16, 36, 36);
            activity.MouseDown += DragWindow;
            titleBar.Controls.Add(activity);

            _titleLabel = CreateLabel(17F, FontStyle.Bold, AppColors.Text);
            _titleLabel.SetBounds(74, 9, 390, 30);
            _titleLabel.MouseDown += DragWindow;
            titleBar.Controls.Add(_titleLabel);
            _introLabel = CreateLabel(8.8F, FontStyle.Regular, AppColors.MutedText);
            _introLabel.SetBounds(75, 38, 570, 23);
            _introLabel.MouseDown += DragWindow;
            titleBar.Controls.Add(_introLabel);

            _liveLabel = CreateLabel(8.5F, FontStyle.Bold, Color.FromArgb(3, 152, 85));
            _liveLabel.TextAlign = ContentAlignment.MiddleRight;
            _liveLabel.SetBounds(LogicalWidth - 250, 19, 146, 30);
            _liveLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            titleBar.Controls.Add(_liveLabel);

            _minimizeButton = CreateWindowButton("−");
            _minimizeButton.SetBounds(LogicalWidth - 94, 17, 34, 34);
            _minimizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(_minimizeButton);
            _closeButton = CreateWindowButton("×");
            _closeButton.SetBounds(LogicalWidth - 52, 17, 34, 34);
            _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _closeButton.HoverColor = AppColors.IsLight
                ? Color.FromArgb(254, 228, 226)
                : Color.FromArgb(80, 24, 30);
            _closeButton.PressedColor = AppColors.IsLight
                ? Color.FromArgb(254, 205, 202)
                : Color.FromArgb(110, 28, 36);
            _closeButton.Click += delegate { Close(); };
            titleBar.Controls.Add(_closeButton);

            _cpuCard = new HardwareMetricCard(Color.FromArgb(21, 94, 239), "CPU");
            _gpuCard = new HardwareMetricCard(Color.FromArgb(124, 58, 237), "GPU");
            _memoryCard = new HardwareMetricCard(Color.FromArgb(8, 145, 178), "RAM");
            _storageCard = new HardwareMetricCard(Color.FromArgb(220, 104, 3), "SSD");
            AddCard(root, _cpuCard, 24, 92, 428, 216);
            AddCard(root, _gpuCard, 468, 92, 428, 216);
            AddCard(root, _memoryCard, 24, 324, 428, 216);
            AddCard(root, _storageCard, 468, 324, 428, 216);

            Panel footer = new Panel();
            footer.SetBounds(24, 556, LogicalWidth - 48, 42);
            footer.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            footer.BackColor = AppColors.Card;
            root.Controls.Add(footer);
            Panel footerRule = new Panel();
            footerRule.BackColor = AppColors.Divider;
            footerRule.Dock = DockStyle.Top;
            footerRule.Height = 1;
            footer.Controls.Add(footerRule);
            _statusLabel = CreateLabel(8.5F, FontStyle.Regular, AppColors.MutedText);
            _statusLabel.SetBounds(10, 8, footer.Width - 20, 27);
            _statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            footer.Controls.Add(_statusLabel);

            ApplyLanguage();
            ApplySnapshot(HardwareSnapshot.Empty());

            _sampleTimer = new System.Windows.Forms.Timer();
            _sampleTimer.Interval = 1000;
            _sampleTimer.Tick += delegate { RequestSample(); };
            if (sampleAutomatically)
            {
                Resize += delegate
                {
                    if (WindowState == FormWindowState.Minimized)
                    {
                        StopMonitoring(false);
                    }
                    else if (!_closed && Visible)
                    {
                        StartMonitoring();
                    }
                };
            }
            FormClosing += HardwareMonitorClosing;
        }

        public void ShowMonitor()
        {
            if (_closed || IsDisposed) return;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            bool opening = !Visible;
            Screen target = opening ? Screen.FromPoint(Cursor.Position) : Screen.FromControl(this);
            if (opening)
            {
                StartPosition = FormStartPosition.Manual;
                Rectangle area = target.WorkingArea;
                Location = new Point(
                    area.Left + Math.Max(0, (area.Width - Width) / 2),
                    area.Top + Math.Max(0, (area.Height - Height) / 2));
                Show();
            }
            FitToWorkingArea(target.WorkingArea, opening);
            StartMonitoring();
            BringToFront();
            Activate();
        }

        public void PrepareForExit()
        {
            _allowClose = true;
            StopMonitoring(false);
        }

        public void HandleDisplayConfigurationChanged()
        {
            if (_closed || IsDisposed) return;
            if (!Visible)
            {
                _lastFitArea = Rectangle.Empty;
                _lastFitDpi = 0F;
                return;
            }
            FitToWorkingArea(Screen.FromControl(this).WorkingArea, false);
        }

        internal Control ApplyPreviewScale(float scale)
        {
            if (scale <= 0F) throw new ArgumentOutOfRangeException("scale");
            if (Math.Abs(scale - 1F) < 0.001F) return _rootPanel;
            SuspendLayout();
            _rootPanel.SuspendLayout();
            Rectangle originalBounds = _rootPanel.Bounds;
            Size originalSize = _rootPanel.ClientSize;
            _rootPanel.Dock = DockStyle.None;
            // Removing Dock restores the panel's pre-dock design-time bounds.
            // Put back the actual laid-out surface before applying preview scale.
            _rootPanel.Bounds = originalBounds;
            _rootPanel.Scale(new SizeF(scale, scale));
            _rootPanel.ClientSize = new Size(
                Math.Max(1, (int)Math.Round(originalSize.Width * scale)),
                Math.Max(1, (int)Math.Round(originalSize.Height * scale)));
            _fitFontScale *= scale;
            ApplyLanguage();
            _rootPanel.ResumeLayout(true);
            ResumeLayout(true);
            return _rootPanel;
        }

        public void ApplyLanguage()
        {
            ReplaceFont(_titleLabel, 17F * _fitFontScale, FontStyle.Bold);
            ReplaceFont(_introLabel, 8.8F * _fitFontScale, FontStyle.Regular);
            ReplaceFont(_liveLabel, 8.5F * _fitFontScale, FontStyle.Bold);
            ReplaceFont(_statusLabel, 8.5F * _fitFontScale, FontStyle.Regular);
            ReplaceFont(_minimizeButton, 12F * _fitFontScale, FontStyle.Regular);
            ReplaceFont(_closeButton, 12F * _fitFontScale, FontStyle.Regular);
            Text = AppText.Get("Hardware.WindowTitle");
            _titleLabel.Text = AppText.Get("Hardware.Title");
            _introLabel.Text = AppText.Get("Hardware.Intro");
            _liveLabel.Text = "●  " + AppText.Get("Hardware.Live");
            _minimizeButton.AccessibleName = AppText.Get("Hardware.Minimize.Accessible");
            _closeButton.AccessibleName = AppText.Get("Hardware.Close.Accessible");
            _cpuCard.ApplyLanguage(AppText.Get("Hardware.Cpu"));
            _gpuCard.ApplyLanguage(AppText.Get("Hardware.Gpu"));
            _memoryCard.ApplyLanguage(AppText.Get("Hardware.Memory"));
            _storageCard.ApplyLanguage(AppText.Get("Hardware.Storage"));
            if (_lastSnapshot != null) ApplySnapshotCore(_lastSnapshot, false);
            else UpdateStatus(null);
        }

        public void ApplySnapshot(HardwareSnapshot snapshot)
        {
            ApplySnapshotCore(snapshot, true);
        }

        private void ApplySnapshotCore(HardwareSnapshot snapshot, bool addHistory)
        {
            if (snapshot == null) snapshot = HardwareSnapshot.Empty();
            _lastSnapshot = snapshot;
            _cpuCard.SetReading(snapshot.Cpu, null, addHistory);
            _gpuCard.SetReading(snapshot.Gpu, null, addHistory);
            string memoryDetail = null;
            if (snapshot.Memory.UsedBytes.HasValue && snapshot.Memory.TotalBytes.HasValue)
            {
                memoryDetail = AppText.Format(
                    "Hardware.MemoryUsed",
                    FormatBytes(snapshot.Memory.UsedBytes.Value),
                    FormatBytes(snapshot.Memory.TotalBytes.Value));
            }
            _memoryCard.SetReading(snapshot.Memory, memoryDetail, addHistory);
            _storageCard.SetReading(snapshot.Storage, null, addHistory);
            UpdateStatus(snapshot);
        }

        private void RequestSample()
        {
            if (_closed || !_monitoring || _service == null || Interlocked.Exchange(ref _reading, 1) != 0) return;
            int generation = _monitorGeneration;
            ThreadPool.QueueUserWorkItem(delegate
            {
                HardwareSnapshot snapshot;
                try { snapshot = _service.ReadSnapshot(); }
                catch { snapshot = HardwareSnapshot.Empty(); }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        Interlocked.Exchange(ref _reading, 0);
                        if (!_closed && _monitoring && generation == _monitorGeneration)
                            ApplySnapshot(snapshot);
                    }));
                }
                catch
                {
                    Interlocked.Exchange(ref _reading, 0);
                }
            });
        }

        private void UpdateStatus(HardwareSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SampledAt == DateTime.MinValue ||
                (!snapshot.Cpu.UsagePercent.HasValue && !snapshot.Memory.UsagePercent.HasValue))
            {
                _statusLabel.Text = AppText.Get("Hardware.WarmingUp");
                return;
            }

            string updated = AppText.Format(
                "Hardware.LastUpdated", snapshot.SampledAt.ToString("T", AppText.CurrentCulture));
            _statusLabel.Text = updated + "   •   " + AppText.Get("Hardware.History") +
                "   •   " + AppText.Get("Hardware.SensorNote");
        }

        private void StartMonitoring()
        {
            if (!_sampleAutomatically || _closed || !Visible || WindowState == FormWindowState.Minimized) return;
            if (_monitoring) return;
            _monitoring = true;
            _sampleTimer.Start();
            RequestSample();
        }

        private void StopMonitoring(bool clearHistory)
        {
            if (_closed) return;
            _monitoring = false;
            _monitorGeneration++;
            _sampleTimer.Stop();
            if (!clearHistory) return;
            _cpuCard.Clear();
            _gpuCard.Clear();
            _memoryCard.Clear();
            _storageCard.Clear();
            _lastSnapshot = HardwareSnapshot.Empty();
            UpdateStatus(null);
        }

        private void HardwareMonitorClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (_allowClose || eventArgs.CloseReason != CloseReason.UserClosing) return;
            eventArgs.Cancel = true;
            StopMonitoring(true);
            Hide();
        }

        private void FitToWorkingArea(Rectangle area, bool center)
        {
            if (_closed || IsDisposed || area.Width <= 0 || area.Height <= 0) return;
            float dpi = ReadCurrentDpi();
            bool configurationChanged = !_lastFitArea.Equals(area) ||
                Math.Abs(_lastFitDpi - dpi) > 0.5F;
            if (configurationChanged)
            {
                SuspendLayout();
                MinimumSize = Size.Empty;
                float dpiScale = Math.Max(0.5F, dpi / 96F);
                float naturalWidth = LogicalWidth * dpiScale;
                float naturalHeight = LogicalHeight * dpiScale;
                float widthScale = (area.Width - 24F) / Math.Max(1F, naturalWidth);
                float heightScale = (area.Height - 24F) / Math.Max(1F, naturalHeight);
                float fitScale = Math.Min(1F, Math.Min(widthScale, heightScale));
                float desiredLayoutScale = dpiScale * fitScale;
                int desiredWidth = Math.Max(1, (int)Math.Round(LogicalWidth * desiredLayoutScale));
                int desiredHeight = Math.Max(1, (int)Math.Round(LogicalHeight * desiredLayoutScale));
                float currentLayoutScale = Math.Max(
                    0.01F,
                    Math.Min(Width / (float)LogicalWidth, Height / (float)LogicalHeight));
                float scaleRatio = desiredLayoutScale / currentLayoutScale;
                if (Math.Abs(scaleRatio - 1F) > 0.001F)
                {
                    Scale(new SizeF(scaleRatio, scaleRatio));
                }
                ClientSize = new Size(desiredWidth, desiredHeight);
                _fitFontScale = fitScale;
                ApplyLanguage();
                ResumeLayout(true);
                _lastFitArea = area;
                _lastFitDpi = dpi;
            }

            int left = center
                ? area.Left + Math.Max(0, (area.Width - Width) / 2)
                : Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            int top = center
                ? area.Top + Math.Max(0, (area.Height - Height) / 2)
                : Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
            Location = new Point(left, top);
        }

        private float ReadCurrentDpi()
        {
            try
            {
                using (Graphics graphics = CreateGraphics()) return graphics.DpiX;
            }
            catch
            {
                return 96F;
            }
        }

        private static void AddCard(Control parent, Control card, int x, int y, int width, int height)
        {
            card.SetBounds(x, y, width, height);
            card.Anchor = (x < LogicalWidth / 2 ? AnchorStyles.Left : AnchorStyles.Right) |
                (y < LogicalHeight / 2 ? AnchorStyles.Top : AnchorStyles.Bottom);
            parent.Controls.Add(card);
        }

        private static Label CreateLabel(float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Font = AppText.CreateFont(size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.UseCompatibleTextRendering = true;
            return label;
        }

        private void ReplaceFont(Control control, float size, FontStyle style)
        {
            Font previous = control.Font;
            string family = AppText.GetFontFamilyName((style & FontStyle.Bold) == FontStyle.Bold);
            if (previous != null &&
                string.Equals(previous.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(previous.SizeInPoints - size) < 0.05F &&
                previous.Style == style)
                return;
            Font replacement = AppText.CreateFont(size, style);
            control.Font = replacement;
            if (object.ReferenceEquals(control.Font, previous))
            {
                replacement.Dispose();
                return;
            }
            if (previous != null) previous.Dispose();
        }

        private static ModernButton CreateWindowButton(string text)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.Font = AppText.CreateFont(12F, FontStyle.Regular);
            button.CornerRadius = 2;
            button.FillColor = AppColors.Card;
            button.HoverColor = AppColors.ControlHover;
            button.BorderThickness = 0;
            return button;
        }

        private static string FormatBytes(long bytes)
        {
            double value = bytes;
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;
            while (value >= 1024D && unit < units.Length - 1)
            {
                value /= 1024D;
                unit++;
            }
            string pattern = value >= 100D ? "0" : "0.0";
            return value.ToString(pattern, AppText.CurrentCulture) + " " + units[unit];
        }

        private static void PaintRootBorder(object sender, PaintEventArgs eventArgs)
        {
            Control control = sender as Control;
            if (control == null) return;
            using (Pen border = new Pen(AppColors.StrongBorder))
                eventArgs.Graphics.DrawRectangle(border, 0, 0, control.Width - 1, control.Height - 1);
        }

        private void DragWindow(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg != 0x02E0 || _closed || IsDisposed || !Visible) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (!_closed && !IsDisposed && Visible)
                        FitToWorkingArea(Screen.FromControl(this).WorkingArea, false);
                }));
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            List<Font> ownedFonts = null;
            if (disposing && !_closed)
            {
                _closed = true;
                _monitoring = false;
                _monitorGeneration++;
                if (_sampleTimer != null)
                {
                    _sampleTimer.Stop();
                    _sampleTimer.Dispose();
                }
                if (_ownsService && _service != null &&
                    Interlocked.Exchange(ref _serviceDisposeQueued, 1) == 0)
                    ThreadPool.QueueUserWorkItem(delegate { _service.Dispose(); });
                ownedFonts = CollectOwnedFonts();
            }
            base.Dispose(disposing);
            if (ownedFonts != null)
            {
                foreach (Font font in ownedFonts) font.Dispose();
            }
        }

        private List<Font> CollectOwnedFonts()
        {
            List<Font> fonts = new List<Font>();
            AddOwnedFont(fonts, Font);
            AddOwnedFont(fonts, _titleLabel.Font);
            AddOwnedFont(fonts, _introLabel.Font);
            AddOwnedFont(fonts, _liveLabel.Font);
            AddOwnedFont(fonts, _statusLabel.Font);
            AddOwnedFont(fonts, _minimizeButton.Font);
            AddOwnedFont(fonts, _closeButton.Font);
            return fonts;
        }

        private static void AddOwnedFont(List<Font> fonts, Font font)
        {
            if (font == null) return;
            foreach (Font existing in fonts)
            {
                if (object.ReferenceEquals(existing, font)) return;
            }
            fonts.Add(font);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class HardwareMetricCard : Control
    {
        private const int HistoryLength = 60;
        private readonly Queue<double?> _usageHistory = new Queue<double?>();
        private readonly Queue<double?> _temperatureHistory = new Queue<double?>();
        private readonly Color _accent;
        private readonly string _badge;
        private string _title = string.Empty;
        private string _deviceName = string.Empty;
        private string _detail = string.Empty;
        private double? _usage;
        private double? _temperature;
        private bool _present;

        public HardwareMetricCard(Color accent, string badge)
        {
            _accent = accent;
            _badge = badge;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = AppColors.Card;
            AccessibleRole = AccessibleRole.Graphic;
        }

        public void ApplyLanguage(string title)
        {
            _title = title;
            AccessibleName = title;
            Invalidate();
        }

        public void SetReading(HardwareMetricReading reading, string detail, bool addHistory)
        {
            if (reading == null) return;
            _deviceName = reading.Name ?? string.Empty;
            _detail = detail ?? string.Empty;
            if (string.IsNullOrEmpty(_deviceName) && !string.IsNullOrEmpty(_detail))
                _deviceName = _detail;
            _usage = reading.UsagePercent;
            _temperature = reading.TemperatureCelsius;
            _present = reading.Present;
            if (addHistory)
            {
                AddHistory(_usageHistory, _usage);
                AddHistory(_temperatureHistory, _temperature);
            }
            AccessibleDescription = BuildAccessibleDescription();
            Invalidate();
        }

        public void Clear()
        {
            _usageHistory.Clear();
            _temperatureHistory.Clear();
            _deviceName = string.Empty;
            _detail = string.Empty;
            _usage = null;
            _temperature = null;
            _present = false;
            AccessibleDescription = BuildAccessibleDescription();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (Width < 2 || Height < 2) return;
            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            float dpiScale = Math.Max(0.5F, UiHelpers.DpiScale(eventArgs.Graphics));
            float layoutScale = Math.Max(0.5F, Math.Min(Width / 428F, Height / 216F));
            float fontScale = Math.Max(0.5F, layoutScale / dpiScale);
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush background = new SolidBrush(AppColors.Card))
                eventArgs.Graphics.FillRectangle(background, bounds);
            using (Pen border = new Pen(AppColors.Border, Math.Max(1F, layoutScale)))
                eventArgs.Graphics.DrawRectangle(border, bounds);
            using (SolidBrush rail = new SolidBrush(_accent))
                eventArgs.Graphics.FillRectangle(rail, 0, 0, Scaled(4F, layoutScale), Height);

            int left = Scaled(20F, layoutScale);
            int top = Scaled(14F, layoutScale);
            int badgeWidth = Scaled(52F, layoutScale);
            Rectangle badgeBounds = new Rectangle(left, top, badgeWidth, Scaled(26F, layoutScale));
            using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(26, _accent)))
                eventArgs.Graphics.FillRectangle(badgeBrush, badgeBounds);
            using (Font badgeFont = AppText.CreateFont(8F * fontScale, FontStyle.Bold))
                TextRenderer.DrawText(eventArgs.Graphics, _badge, badgeFont, badgeBounds, _accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            Rectangle titleBounds = new Rectangle(
                badgeBounds.Right + Scaled(10F, layoutScale),
                top - Scaled(1F, layoutScale),
                Math.Max(Scaled(30F, layoutScale), Width - badgeBounds.Right - Scaled(30F, layoutScale)),
                badgeBounds.Height + Scaled(2F, layoutScale));
            using (Font titleFont = AppText.CreateFont(11F * fontScale, FontStyle.Bold))
                TextRenderer.DrawText(eventArgs.Graphics, _title, titleFont, titleBounds, AppColors.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);

            string device = !_present ? AppText.Get("Hardware.NotDetected") :
                (string.IsNullOrEmpty(_deviceName) ? _title : _deviceName);
            if (_present && !string.IsNullOrEmpty(_detail) && !string.Equals(device, _detail))
                device += "  •  " + _detail;
            Rectangle deviceBounds = new Rectangle(
                left,
                badgeBounds.Bottom + Scaled(6F, layoutScale),
                Width - left * 2,
                Scaled(20F, layoutScale));
            using (Font deviceFont = AppText.CreateFont(8F * fontScale, FontStyle.Regular))
                TextRenderer.DrawText(eventArgs.Graphics, device, deviceFont, deviceBounds, AppColors.MutedText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);

            int valueTop = badgeBounds.Bottom + Scaled(29F, layoutScale);
            string usageText = _usage.HasValue ? Math.Round(_usage.Value).ToString("0") + "%" : "--%";
            Rectangle usageBounds = new Rectangle(
                left, valueTop, Scaled(120F, layoutScale), Scaled(40F, layoutScale));
            using (Font valueFont = AppText.CreateFont(21F * fontScale, FontStyle.Bold))
                TextRenderer.DrawText(eventArgs.Graphics, usageText, valueFont, usageBounds, AppColors.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Rectangle usageLabelBounds = new Rectangle(
                left + Scaled(122F, layoutScale),
                valueTop + Scaled(8F, layoutScale),
                Scaled(84F, layoutScale),
                Scaled(26F, layoutScale));
            using (Font smallBold = AppText.CreateFont(8F * fontScale, FontStyle.Bold))
                TextRenderer.DrawText(eventArgs.Graphics, AppText.Get("Hardware.Usage"), smallBold,
                    usageLabelBounds, AppColors.MutedText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            Rectangle temperatureBounds = new Rectangle(
                Width - Scaled(154F, layoutScale),
                valueTop,
                Scaled(132F, layoutScale),
                Scaled(40F, layoutScale));
            using (SolidBrush temperatureBack = new SolidBrush(AppColors.CardHover))
                eventArgs.Graphics.FillRectangle(temperatureBack, temperatureBounds);
            string temperatureText = _temperature.HasValue
                ? Math.Round(_temperature.Value).ToString("0") + " °C"
                : AppText.Get("Hardware.TemperatureUnavailable");
            using (Font temperatureFont = AppText.CreateFont(
                (_temperature.HasValue ? 11F : 7.8F) * fontScale, FontStyle.Bold))
                TextRenderer.DrawText(eventArgs.Graphics, temperatureText, temperatureFont,
                    Rectangle.Inflate(temperatureBounds, -Scaled(8F, layoutScale), 0),
                    _temperature.HasValue ? Color.FromArgb(217, 45, 32) : AppColors.MutedText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);

            int chartTop = valueTop + Scaled(50F, layoutScale);
            Rectangle chartBounds = new Rectangle(
                left,
                chartTop,
                Math.Max(Scaled(20F, layoutScale), Width - left - Scaled(22F, layoutScale)),
                Math.Max(Scaled(30F, layoutScale), Height - chartTop - Scaled(18F, layoutScale)));
            DrawChart(eventArgs.Graphics, chartBounds, layoutScale);
        }

        private void DrawChart(Graphics graphics, Rectangle bounds, float scale)
        {
            using (SolidBrush chartBack = new SolidBrush(AppColors.Window))
                graphics.FillRectangle(chartBack, bounds);
            using (Pen gridPen = new Pen(AppColors.Divider, Math.Max(1F, scale)))
            {
                for (int row = 1; row < 4; row++)
                {
                    float y = bounds.Top + bounds.Height * row / 4F;
                    graphics.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
                }
                for (int column = 1; column < 6; column++)
                {
                    float x = bounds.Left + bounds.Width * column / 6F;
                    graphics.DrawLine(gridPen, x, bounds.Top, x, bounds.Bottom);
                }
            }
            DrawSeries(graphics, bounds, _usageHistory, _accent, Math.Max(1.8F, 2F * scale));
            DrawSeries(graphics, bounds, _temperatureHistory, Color.FromArgb(217, 45, 32),
                Math.Max(1.4F, 1.7F * scale));
        }

        private static void DrawSeries(
            Graphics graphics,
            Rectangle bounds,
            Queue<double?> samples,
            Color color,
            float thickness)
        {
            double?[] values = samples.ToArray();
            if (values.Length < 2) return;
            using (Pen pen = new Pen(color, thickness))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                PointF? previous = null;
                int missing = Math.Max(0, HistoryLength - values.Length);
                for (int index = 0; index < values.Length; index++)
                {
                    if (!values[index].HasValue)
                    {
                        previous = null;
                        continue;
                    }
                    double value = Math.Max(0D, Math.Min(100D, values[index].Value));
                    float x = bounds.Left + bounds.Width * (missing + index) / (float)(HistoryLength - 1);
                    float y = bounds.Bottom - 2F - (bounds.Height - 4F) * (float)(value / 100D);
                    PointF current = new PointF(x, y);
                    if (previous.HasValue) graphics.DrawLine(pen, previous.Value, current);
                    previous = current;
                }
            }
        }

        private static void AddHistory(Queue<double?> history, double? value)
        {
            history.Enqueue(value);
            while (history.Count > HistoryLength) history.Dequeue();
        }

        private static int Scaled(float logicalPixels, float scale)
        {
            return Math.Max(1, (int)Math.Round(logicalPixels * scale));
        }

        private string BuildAccessibleDescription()
        {
            string usage = _usage.HasValue ? Math.Round(_usage.Value).ToString("0") + "%" : "--";
            string temperature = _temperature.HasValue
                ? Math.Round(_temperature.Value).ToString("0") + " °C"
                : AppText.Get("Hardware.TemperatureUnavailable");
            string device = !_present ? AppText.Get("Hardware.NotDetected") : _deviceName;
            if (!string.IsNullOrEmpty(_detail) && !string.Equals(device, _detail))
                device = string.IsNullOrEmpty(device) ? _detail : device + ", " + _detail;
            string prefix = string.IsNullOrEmpty(device) ? _title : _title + ", " + device;
            return prefix + ", " + AppText.Get("Hardware.Usage") + " " + usage + ", " +
                AppText.Get("Hardware.Temperature") + " " + temperature;
        }
    }

    internal sealed class ActivityMark : Control
    {
        public ActivityMark()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            UiHelpers.PrepareGraphics(eventArgs.Graphics);
            using (SolidBrush background = new SolidBrush(AppColors.Accent))
                eventArgs.Graphics.FillRectangle(background, 0, 0, Width - 1, Height - 1);
            float iconScale = Math.Max(0.1F, Math.Min((Width - 2F) / 36F, (Height - 2F) / 36F));
            GraphicsState state = eventArgs.Graphics.Save();
            eventArgs.Graphics.TranslateTransform(
                (Width - 36F * iconScale) / 2F,
                (Height - 36F * iconScale) / 2F);
            eventArgs.Graphics.ScaleTransform(iconScale, iconScale);
            using (Pen line = new Pen(Color.White, 2.2F))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                PointF[] points = new PointF[]
                {
                    new PointF(6F, 20F), new PointF(11F, 20F), new PointF(14F, 12F),
                    new PointF(19F, 27F), new PointF(23F, 17F), new PointF(30F, 17F)
                };
                eventArgs.Graphics.DrawLines(line, points);
            }
            eventArgs.Graphics.Restore(state);
        }
    }
}
