using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Models;
using QuickControls.Services;

namespace QuickControls.UI
{
    public sealed class PanelForm : Form
    {
        private const int ExpandedWidth = 420;
        private const int ExpandedHeight = 452;
        private const int CompactWidth = 336;
        private const int CompactHeight = 64;

        private readonly Panel _expandedView;
        private readonly Panel _compactView;
        private readonly Label _volumePercent;
        private readonly Label _compactVolume;
        private readonly Label _compactBrightness;
        private readonly ModernSlider _volumeSlider;
        private readonly ModernButton _muteButton;
        private readonly ModernButton _volumeDownButton;
        private readonly ModernButton _volumeUpButton;
        private readonly Label _audioStatus;
        private readonly Label _brightnessPercent;
        private readonly ModernSlider _brightnessSlider;
        private readonly ModernButton _brightnessDownButton;
        private readonly ModernButton _brightnessUpButton;
        private readonly ComboBox _displayCombo;
        private readonly Label _displayName;
        private readonly Label _brightnessStatus;
        private readonly ModernButton _brightnessRetry;
        private readonly Timer _collapseTimer;
        private readonly bool _initialCompact;
        private bool _compact;
        private bool _allowClose;
        private bool _updatingDisplaySelection;
        private Size _expandedSize;
        private Size _compactSize;
        private AppSettings _settings;

        public PanelForm(AppSettings settings)
        {
            _settings = settings;
            _initialCompact = settings.PanelCompact;
            _expandedSize = new Size(ExpandedWidth, ExpandedHeight);
            _compactSize = new Size(CompactWidth, CompactHeight);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = AppColors.Window;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI", 10F);
            TopMost = settings.AlwaysOnTop;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(ExpandedWidth, ExpandedHeight);
            MinimumSize = new Size(CompactWidth, CompactHeight);

            _expandedView = new Panel();
            _expandedView.Dock = DockStyle.Fill;
            _expandedView.BackColor = Color.Transparent;
            Controls.Add(_expandedView);

            Panel header = new Panel();
            header.BackColor = Color.Transparent;
            header.SetBounds(0, 0, ExpandedWidth, 64);
            header.MouseDown += DragWindow;
            _expandedView.Controls.Add(header);

            LogoControl logo = new LogoControl();
            logo.SetBounds(16, 14, 36, 36);
            logo.MouseDown += DragWindow;
            header.Controls.Add(logo);

            Label title = CreateLabel("Quick Controls", 18F, FontStyle.Bold, AppColors.Text);
            title.SetBounds(64, 13, 250, 32);
            title.MouseDown += DragWindow;
            header.Controls.Add(title);

            ModernButton collapse = CreateSquareButton("–", "Switch to mini panel");
            collapse.SetBounds(332, 16, 32, 32);
            collapse.Click += delegate { SetCompact(true); };
            header.Controls.Add(collapse);

            ModernButton hide = CreateSquareButton("×", "Hide to the system tray");
            hide.SetBounds(372, 16, 32, 32);
            hide.Click += delegate { Hide(); };
            header.Controls.Add(hide);

            RoundedPanel volumeCard = new RoundedPanel();
            volumeCard.SetBounds(16, 68, 388, 136);
            volumeCard.CornerRadius = 14;
            _expandedView.Controls.Add(volumeCard);

            Label volumeTitle = CreateLabel("Volume", 11.5F, FontStyle.Bold, AppColors.Text);
            volumeTitle.SetBounds(16, 10, 220, 27);
            volumeCard.Controls.Add(volumeTitle);

            _volumePercent = CreateLabel("--%", 18F, FontStyle.Bold, AppColors.Text);
            _volumePercent.TextAlign = ContentAlignment.MiddleRight;
            _volumePercent.SetBounds(300, 7, 72, 31);
            volumeCard.Controls.Add(_volumePercent);

            _volumeSlider = new ModernSlider();
            _volumeSlider.AccessibleName = "Volume";
            _volumeSlider.SetBounds(16, 42, 356, 32);
            _volumeSlider.UserValueChanged += delegate { ResetCollapseTimer(); RaiseInt(VolumeChanged, _volumeSlider.Value); };
            volumeCard.Controls.Add(_volumeSlider);

            _volumeDownButton = CreateActionButton("Quieter", "Decrease volume");
            _volumeDownButton.SetBounds(16, 84, 80, 40);
            _volumeDownButton.Click += delegate { ResetCollapseTimer(); RaiseInt(VolumeStepRequested, -1); };
            volumeCard.Controls.Add(_volumeDownButton);

            _muteButton = CreateActionButton("Mute", "Mute or unmute audio");
            _muteButton.SetBounds(104, 84, 180, 40);
            _muteButton.Click += delegate { ResetCollapseTimer(); Raise(MuteRequested); };
            volumeCard.Controls.Add(_muteButton);

            _volumeUpButton = CreateActionButton("Louder", "Increase volume");
            _volumeUpButton.SetBounds(292, 84, 80, 40);
            _volumeUpButton.Click += delegate { ResetCollapseTimer(); RaiseInt(VolumeStepRequested, 1); };
            volumeCard.Controls.Add(_volumeUpButton);

            _audioStatus = CreateLabel(string.Empty, 8.5F, FontStyle.Regular, AppColors.Danger);
            _audioStatus.SetBounds(16, 43, 356, 34);
            volumeCard.Controls.Add(_audioStatus);

            RoundedPanel brightnessCard = new RoundedPanel();
            brightnessCard.SetBounds(16, 216, 388, 164);
            brightnessCard.CornerRadius = 14;
            _expandedView.Controls.Add(brightnessCard);

            Label brightnessTitle = CreateLabel("Brightness", 11.5F, FontStyle.Bold, AppColors.Text);
            brightnessTitle.SetBounds(16, 8, 220, 27);
            brightnessCard.Controls.Add(brightnessTitle);

            _brightnessPercent = CreateLabel("--%", 18F, FontStyle.Bold, AppColors.Text);
            _brightnessPercent.TextAlign = ContentAlignment.MiddleRight;
            _brightnessPercent.SetBounds(300, 5, 72, 31);
            brightnessCard.Controls.Add(_brightnessPercent);

            _displayCombo = new ComboBox();
            _displayCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _displayCombo.FlatStyle = FlatStyle.Flat;
            _displayCombo.BackColor = AppColors.CardHover;
            _displayCombo.ForeColor = AppColors.Text;
            _displayCombo.Font = new Font("Segoe UI", 9.5F);
            _displayCombo.SetBounds(16, 37, 356, 28);
            _displayCombo.SelectedIndexChanged += DisplaySelectionChanged;
            brightnessCard.Controls.Add(_displayCombo);

            _displayName = CreateLabel(string.Empty, 9F, FontStyle.Regular, AppColors.MutedText);
            _displayName.SetBounds(16, 38, 356, 24);
            brightnessCard.Controls.Add(_displayName);

            _brightnessSlider = new ModernSlider();
            _brightnessSlider.AccentColor = AppColors.Accent2;
            _brightnessSlider.AccessibleName = "Brightness";
            _brightnessSlider.SetBounds(16, 68, 356, 32);
            _brightnessSlider.UserValueChanged += delegate { ResetCollapseTimer(); RaiseInt(BrightnessChanged, _brightnessSlider.Value); };
            brightnessCard.Controls.Add(_brightnessSlider);

            _brightnessDownButton = CreateActionButton("Dimmer", "Decrease brightness");
            _brightnessDownButton.SetBounds(16, 108, 174, 40);
            _brightnessDownButton.Click += delegate { ResetCollapseTimer(); RaiseInt(BrightnessStepRequested, -1); };
            brightnessCard.Controls.Add(_brightnessDownButton);

            _brightnessRetry = CreateActionButton("Refresh displays", "Detect displays again");
            _brightnessRetry.SetBounds(16, 108, 356, 40);
            _brightnessRetry.Visible = false;
            _brightnessRetry.Click += delegate { ResetCollapseTimer(); Raise(BrightnessRetryRequested); };
            brightnessCard.Controls.Add(_brightnessRetry);

            _brightnessUpButton = CreateActionButton("Brighter", "Increase brightness");
            _brightnessUpButton.SetBounds(198, 108, 174, 40);
            _brightnessUpButton.Click += delegate { ResetCollapseTimer(); RaiseInt(BrightnessStepRequested, 1); };
            brightnessCard.Controls.Add(_brightnessUpButton);

            _brightnessStatus = CreateLabel(string.Empty, 8.5F, FontStyle.Regular, AppColors.Danger);
            _brightnessStatus.SetBounds(16, 66, 356, 38);
            brightnessCard.Controls.Add(_brightnessStatus);

            ModernButton settingsButton = new ModernButton();
            settingsButton.Text = "Settings";
            settingsButton.AccessibleName = "Open shortcuts and settings";
            settingsButton.SetBounds(16, 392, 190, 40);
            settingsButton.Click += delegate { ResetCollapseTimer(); Raise(SettingsRequested); };
            _expandedView.Controls.Add(settingsButton);

            ModernButton windowsDisplayButton = new ModernButton();
            windowsDisplayButton.Text = "Display settings";
            windowsDisplayButton.AccessibleName = "Open Windows display settings";
            windowsDisplayButton.SetBounds(214, 392, 190, 40);
            windowsDisplayButton.Click += delegate { Raise(OpenWindowsDisplaySettingsRequested); };
            _expandedView.Controls.Add(windowsDisplayButton);

            _compactView = new Panel();
            _compactView.Dock = DockStyle.Fill;
            _compactView.BackColor = Color.Transparent;
            _compactView.Visible = false;
            Controls.Add(_compactView);

            Label grip = CreateLabel("⋮", 13F, FontStyle.Bold, AppColors.MutedText);
            grip.TextAlign = ContentAlignment.MiddleCenter;
            grip.SetBounds(8, 12, 16, 40);
            grip.Cursor = Cursors.SizeAll;
            grip.MouseDown += DragWindow;
            _compactView.Controls.Add(grip);

            LogoControl compactLogo = new LogoControl();
            compactLogo.SetBounds(32, 14, 36, 36);
            _compactView.Controls.Add(compactLogo);

            Label compactVolumeTitle = CreateLabel("VOLUME", 7F, FontStyle.Bold, AppColors.MutedText);
            compactVolumeTitle.TextAlign = ContentAlignment.MiddleCenter;
            compactVolumeTitle.SetBounds(72, 10, 58, 18);
            _compactView.Controls.Add(compactVolumeTitle);

            _compactVolume = CreateLabel("--%", 11.5F, FontStyle.Bold, AppColors.Text);
            _compactVolume.TextAlign = ContentAlignment.MiddleCenter;
            _compactVolume.SetBounds(72, 27, 58, 27);
            _compactVolume.AccessibleName = "Volume";
            _compactView.Controls.Add(_compactVolume);

            Label compactBrightnessIcon = CreateLabel("☀", 16F, FontStyle.Regular, AppColors.Accent2);
            compactBrightnessIcon.TextAlign = ContentAlignment.MiddleCenter;
            compactBrightnessIcon.SetBounds(138, 16, 30, 32);
            _compactView.Controls.Add(compactBrightnessIcon);

            Label compactBrightnessTitle = CreateLabel("BRIGHTNESS", 7F, FontStyle.Bold, AppColors.MutedText);
            compactBrightnessTitle.TextAlign = ContentAlignment.MiddleCenter;
            compactBrightnessTitle.SetBounds(169, 10, 78, 18);
            _compactView.Controls.Add(compactBrightnessTitle);

            _compactBrightness = CreateLabel("--%", 11.5F, FontStyle.Bold, AppColors.Text);
            _compactBrightness.TextAlign = ContentAlignment.MiddleCenter;
            _compactBrightness.SetBounds(169, 27, 78, 27);
            _compactBrightness.AccessibleName = "Brightness";
            _compactView.Controls.Add(_compactBrightness);

            ModernButton expand = CreateSquareButton("›", "Expand panel");
            expand.SetBounds(256, 16, 32, 32);
            _compactView.Controls.Add(expand);

            ModernButton compactHide = CreateSquareButton("×", "Hide to the system tray");
            compactHide.SetBounds(296, 16, 32, 32);
            compactHide.Tag = "NoExpand";
            compactHide.Click += delegate { Hide(); };
            _compactView.Controls.Add(compactHide);

            AttachCompactInteraction(_compactView);

            _collapseTimer = new Timer();
            _collapseTimer.Interval = 6000;
            _collapseTimer.Tick += CollapseTimerTick;

            Resize += delegate { if (Width > 0 && Height > 0) UiHelpers.ApplyRoundedRegion(this, 30); };
            LocationChanged += delegate { ClampToVisibleArea(); Raise(PanelPositionChanged); };
            FormClosing += PanelFormClosing;
        }

        public event EventHandler<IntValueEventArgs> VolumeChanged;
        public event EventHandler<IntValueEventArgs> VolumeStepRequested;
        public event EventHandler MuteRequested;
        public event EventHandler<IntValueEventArgs> BrightnessChanged;
        public event EventHandler<IntValueEventArgs> BrightnessStepRequested;
        public event EventHandler<IntValueEventArgs> DisplaySelectionRequested;
        public event EventHandler BrightnessRetryRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler OpenWindowsDisplaySettingsRequested;
        public event EventHandler PanelPositionChanged;
        public event EventHandler CompactStateChanged;

        public bool IsCompact { get { return _compact; } }
        public int SelectedDisplayIndex { get { return _displayCombo.SelectedIndex; } }

        public void EnsureMessageHandle()
        {
            if (!IsHandleCreated) CreateHandle();
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

        public void SetAudioState(AudioState state)
        {
            if (state == null || !state.Available)
            {
                _volumeSlider.Enabled = false;
                _muteButton.Enabled = false;
                _volumeDownButton.Enabled = false;
                _volumeUpButton.Enabled = false;
                _volumeSlider.Visible = false;
                _volumePercent.Text = "--%";
                _compactVolume.Text = "--%";
                _audioStatus.Text = "No audio output device was found.";
                _audioStatus.SetBounds(16, 43, 356, 34);
                _audioStatus.Visible = true;
                return;
            }
            _volumeSlider.Enabled = true;
            _volumeSlider.Visible = true;
            _muteButton.Enabled = true;
            _volumeDownButton.Enabled = true;
            _volumeUpButton.Enabled = true;
            _volumeSlider.Value = state.Volume;
            _volumePercent.Text = state.Muted ? "Muted" : state.Volume + "%";
            _compactVolume.Text = state.Muted ? "Muted" : state.Volume + "%";
            _compactVolume.AccessibleName = state.Muted ? "Volume muted" : "Volume " + state.Volume + " percent";
            _muteButton.Text = state.Muted ? "Unmute" : "Mute";
            _audioStatus.Text = string.Empty;
            _audioStatus.Visible = false;
        }

        public void SetBrightnessDevices(IList<BrightnessDevice> devices, string selectedId)
        {
            _updatingDisplaySelection = true;
            _displayCombo.Items.Clear();
            int selectedIndex = -1;
            for (int index = 0; index < devices.Count; index++)
            {
                _displayCombo.Items.Add(devices[index]);
                if (string.Equals(devices[index].Id, selectedId, StringComparison.Ordinal)) selectedIndex = index;
            }
            if (_displayCombo.Items.Count > 0)
            {
                _displayCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            }
            _displayCombo.Visible = _displayCombo.Items.Count > 1;
            _displayName.Visible = _displayCombo.Items.Count <= 1;
            _displayName.Text = _displayCombo.Items.Count == 1 ? Convert.ToString(_displayCombo.Items[0]) : string.Empty;
            _updatingDisplaySelection = false;
        }

        public void SetBrightnessState(bool available, int value, string status)
        {
            _brightnessSlider.Enabled = available;
            _brightnessSlider.Visible = available;
            _brightnessDownButton.Enabled = available;
            _brightnessUpButton.Enabled = available;
            _brightnessDownButton.Visible = available;
            _brightnessUpButton.Visible = available;
            _brightnessSlider.Value = available ? value : 0;
            _brightnessPercent.Text = available ? value + "%" : "--%";
            _compactBrightness.Text = available ? value + "%" : "--%";
            _compactBrightness.AccessibleName = available
                ? "Brightness " + value + " percent"
                : "Brightness unavailable";
            _brightnessStatus.Text = available ? string.Empty : status;
            _brightnessStatus.SetBounds(16, 66, 356, 38);
            _brightnessStatus.Visible = !available;
            _brightnessRetry.Text = "Try again";
            _brightnessRetry.Visible = !available;
            _brightnessRetry.Enabled = !available;
        }

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            TopMost = settings.AlwaysOnTop;
            if (settings.AutoCollapse) ResetCollapseTimer();
            else _collapseTimer.Stop();
        }

        public void ShowExpanded()
        {
            if (!Visible) Show();
            SetCompact(false);
            ClampToVisibleArea();
            BringToFront();
            Activate();
            ResetCollapseTimer();
        }

        public void TogglePanel()
        {
            if (!Visible)
            {
                ShowExpanded();
            }
            else
            {
                Hide();
            }
        }

        public void PrepareForExit()
        {
            _allowClose = true;
            _collapseTimer.Stop();
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);
            if (_settings.AutoCollapse) ResetCollapseTimer();
        }

        protected override void OnLoad(EventArgs eventArgs)
        {
            base.OnLoad(eventArgs);
            float scale = 1F;
            using (Graphics graphics = CreateGraphics())
            {
                scale = Math.Max(1F, graphics.DpiX / 96F);
            }
            _expandedSize = Size;
            _compactSize = new Size(
                Math.Max(CompactWidth, (int)Math.Round(CompactWidth * scale)),
                Math.Max(CompactHeight, (int)Math.Round(CompactHeight * scale)));
            PositionFromSettings();
            SetCompact(_initialCompact);
        }

        private void SetCompact(bool compact)
        {
            Size targetSize = compact ? _compactSize : _expandedSize;
            if (_compact == compact && Size == targetSize) return;
            int right = Right;
            int centerY = Top + Height / 2;
            _compact = compact;
            _expandedView.Visible = !compact;
            _compactView.Visible = compact;
            Size = targetSize;
            Location = new Point(right - Width, centerY - Height / 2);
            ClampToVisibleArea();
            if (compact) _collapseTimer.Stop();
            else ResetCollapseTimer();
            Raise(CompactStateChanged);
        }

        private void PositionFromSettings()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            bool hasSavedPosition = _settings.PanelLeft >= 0 && _settings.PanelTop >= 0;
            int left = hasSavedPosition ? (int)_settings.PanelLeft : area.Right - _expandedSize.Width - 18;
            int top = hasSavedPosition ? (int)_settings.PanelTop : area.Top + (area.Height - _expandedSize.Height) / 2;
            if (hasSavedPosition && _initialCompact)
            {
                left -= _expandedSize.Width - _compactSize.Width;
                top -= (_expandedSize.Height - _compactSize.Height) / 2;
            }
            Location = new Point(left, top);
            ClampToVisibleArea();
        }

        private void ClampToVisibleArea()
        {
            Screen screen = Screen.FromRectangle(Bounds);
            Rectangle area = screen.WorkingArea;
            int left = Math.Max(area.Left + 4, Math.Min(Left, area.Right - Width - 4));
            int top = Math.Max(area.Top + 4, Math.Min(Top, area.Bottom - Height - 4));
            if (left != Left || top != Top) Location = new Point(left, top);
        }

        private void DragWindow(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left) return;
            _collapseTimer.Stop();
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            ClampToVisibleArea();
            ResetCollapseTimer();
        }

        private void CollapseTimerTick(object sender, EventArgs eventArgs)
        {
            if (!_settings.AutoCollapse || _compact || !Visible)
            {
                _collapseTimer.Stop();
                return;
            }
            if (Bounds.Contains(Cursor.Position) || ContainsFocus || _displayCombo.DroppedDown ||
                _volumeSlider.Capture || _brightnessSlider.Capture)
            {
                ResetCollapseTimer();
                return;
            }
            SetCompact(true);
        }

        private void ResetCollapseTimer()
        {
            _collapseTimer.Stop();
            if (_settings.AutoCollapse && !_compact && Visible) _collapseTimer.Start();
        }

        private void AttachCompactInteraction(Control parent)
        {
            parent.Cursor = Cursors.Hand;
            parent.Click += ExpandCompact;
            foreach (Control child in parent.Controls)
            {
                if (child.Cursor == Cursors.SizeAll || string.Equals(Convert.ToString(child.Tag), "NoExpand", StringComparison.Ordinal)) continue;
                child.Cursor = Cursors.Hand;
                child.Click += ExpandCompact;
            }
        }

        private void ExpandCompact(object sender, EventArgs eventArgs)
        {
            SetCompact(false);
            Activate();
        }

        private void DisplaySelectionChanged(object sender, EventArgs eventArgs)
        {
            if (_updatingDisplaySelection || _displayCombo.SelectedIndex < 0) return;
            ResetCollapseTimer();
            RaiseInt(DisplaySelectionRequested, _displayCombo.SelectedIndex);
        }

        private void PanelFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (!_allowClose && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                Hide();
            }
        }

        private static ModernButton CreateSquareButton(string text, string accessibleName)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            button.AccessibleName = accessibleName;
            button.CornerRadius = 8;
            return button;
        }

        private static ModernButton CreateActionButton(string text, string accessibleName)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.AccessibleName = accessibleName;
            return button;
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Segoe UI", size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            return label;
        }

        private static void Raise(EventHandler handler)
        {
            if (handler != null) handler(null, EventArgs.Empty);
        }

        private static void RaiseInt(EventHandler<IntValueEventArgs> handler, int value)
        {
            if (handler != null) handler(null, new IntValueEventArgs(value));
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }

    public sealed class IntValueEventArgs : EventArgs
    {
        public IntValueEventArgs(int value)
        {
            Value = value;
        }
        public int Value { get; private set; }
    }
}
