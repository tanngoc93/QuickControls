using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Models;
using QuickControls.Services;

namespace QuickControls.UI
{
    public sealed class PanelForm : Form
    {
        public const int FullWidth = 440;
        public const int FullHeight = 456;
        public const int HorizontalWidth = 520;
        public const int HorizontalHeight = 72;
        public const int VerticalWidth = 136;
        public const int VerticalHeight = 360;
        public const int EdgeWidth = 48;
        public const int EdgeHeight = 232;

        private readonly Dictionary<PanelLayoutMode, Panel> _views = new Dictionary<PanelLayoutMode, Panel>();
        private readonly Dictionary<PanelLayoutMode, Size> _logicalSizes = new Dictionary<PanelLayoutMode, Size>();
        private readonly Dictionary<PanelLayoutMode, Size> _scaledSizes = new Dictionary<PanelLayoutMode, Size>();
        private readonly Dictionary<PanelLayoutMode, ToolStripMenuItem> _layoutItems = new Dictionary<PanelLayoutMode, ToolStripMenuItem>();
        private readonly ToolTip _toolTip;
        private readonly ContextMenuStrip _layoutMenu;
        private readonly Timer _collapseTimer;

        private Label _titleLabel;
        private Label _volumeTitle;
        private Label _brightnessTitle;
        private Label _volumePercent;
        private Label _brightnessPercent;
        private Label _horizontalVolumePercent;
        private Label _horizontalBrightnessPercent;
        private Label _verticalVolumeTitle;
        private Label _verticalBrightnessTitle;
        private Label _verticalVolumePercent;
        private Label _verticalBrightnessPercent;
        private Label _edgeVolumePercent;
        private Label _edgeBrightnessPercent;
        private Label _audioStatus;
        private Label _displayName;
        private Label _brightnessStatus;

        private ModernSlider _volumeSlider;
        private ModernSlider _brightnessSlider;
        private ModernButton _volumeDownButton;
        private ModernButton _volumeUpButton;
        private ModernButton _muteButton;
        private ModernButton _brightnessDownButton;
        private ModernButton _brightnessUpButton;
        private ModernButton _brightnessRetry;
        private ModernButton _settingsButton;
        private ModernButton _displaySettingsButton;
        private ModernButton _horizontalVolumeDown;
        private ModernButton _horizontalVolumeUp;
        private GlyphButton _horizontalMute;
        private ModernButton _horizontalBrightnessDown;
        private ModernButton _horizontalBrightnessUp;
        private GlyphButton _horizontalExpand;
        private GlyphButton _horizontalHide;
        private ModernButton _verticalVolumeDown;
        private ModernButton _verticalVolumeUp;
        private GlyphButton _verticalMute;
        private ModernButton _verticalBrightnessDown;
        private ModernButton _verticalBrightnessUp;
        private GlyphButton _verticalCollapse;
        private GlyphButton _verticalExpand;
        private GlyphButton _verticalHide;
        private GlyphButton _fullLayoutButton;
        private GlyphButton _fullCollapseButton;
        private GlyphButton _fullHideButton;
        private GlyphButton _edgeOpenButton;
        private GlyphControl _edgeVolumeGlyph;
        private ComboBox _displayCombo;
        private ModernProgress _edgeVolumeProgress;
        private ModernProgress _edgeBrightnessProgress;
        private ToolStripMenuItem _menuSettings;
        private ToolStripMenuItem _menuHide;

        private AppSettings _settings;
        private PanelLayoutMode _preferredLayout;
        private PanelLayoutMode _currentLayout;
        private PanelDockEdge _resolvedDockEdge;
        private bool _loaded;
        private bool _allowClose;
        private bool _updatingDisplaySelection;
        private bool _applyingBounds;
        private bool _audioAvailable;
        private bool _muted;
        private int _volumeValue;
        private bool _brightnessAvailable;
        private int _brightnessValue;
        private string _brightnessMessage;
        private bool _showWithoutActivation;

        public PanelForm(AppSettings settings)
        {
            _settings = settings ?? AppSettings.CreateDefaults();
            _preferredLayout = _settings.PanelLayoutMode;
            _currentLayout = PanelLayoutMode.Full;
            _resolvedDockEdge = _settings.DockEdge;
            _logicalSizes[PanelLayoutMode.Full] = new Size(FullWidth, FullHeight);
            _logicalSizes[PanelLayoutMode.HorizontalMini] = new Size(HorizontalWidth, HorizontalHeight);
            _logicalSizes[PanelLayoutMode.VerticalMini] = new Size(VerticalWidth, VerticalHeight);
            _logicalSizes[PanelLayoutMode.EdgeDock] = new Size(EdgeWidth, EdgeHeight);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = AppColors.StrongBorder;
            ForeColor = AppColors.Text;
            Font = AppText.CreateFont(10F, FontStyle.Regular);
            TopMost = _settings.AlwaysOnTop;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = _logicalSizes[PanelLayoutMode.Full];
            Padding = new Padding(1);

            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 6000;
            BuildFullView();
            BuildHorizontalView();
            BuildVerticalView();
            BuildEdgeView();
            _layoutMenu = BuildLayoutMenu();

            _collapseTimer = new Timer();
            _collapseTimer.Interval = 6000;
            _collapseTimer.Tick += CollapseTimerTick;
            Resize += delegate
            {
                if (Width > 0 && Height > 0) UiHelpers.ApplyRoundedRegion(this, _currentLayout == PanelLayoutMode.EdgeDock ? 2 : 4);
            };
            LocationChanged += PanelLocationChanged;
            FormClosing += PanelFormClosing;
            ApplyLanguage();
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

        public bool IsCompact { get { return _currentLayout != PanelLayoutMode.Full; } }
        public int SelectedDisplayIndex { get { return _displayCombo.SelectedIndex; } }
        public PanelLayoutMode CurrentLayout { get { return _currentLayout; } }
        public PanelLayoutMode PreferredLayout { get { return _preferredLayout; } }

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

        protected override bool ShowWithoutActivation
        {
            get { return _showWithoutActivation; }
        }

        public void SetAudioState(AudioState state)
        {
            _audioAvailable = state != null && state.Available;
            _muted = _audioAvailable && state.Muted;
            _volumeValue = _audioAvailable ? state.Volume : 0;
            ApplyAudioState();
        }

        public void SetBrightnessDevices(IList<BrightnessDevice> devices, string selectedId)
        {
            _updatingDisplaySelection = true;
            _displayCombo.Items.Clear();
            int selectedIndex = -1;
            if (devices != null)
            {
                for (int index = 0; index < devices.Count; index++)
                {
                    _displayCombo.Items.Add(devices[index]);
                    if (string.Equals(devices[index].Id, selectedId, StringComparison.Ordinal)) selectedIndex = index;
                }
            }
            if (_displayCombo.Items.Count > 0) _displayCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            _displayCombo.Visible = _displayCombo.Items.Count > 1;
            _displayName.Visible = _displayCombo.Items.Count <= 1;
            _displayName.Text = _displayCombo.Items.Count == 1 ? Convert.ToString(_displayCombo.Items[0]) : string.Empty;
            _updatingDisplaySelection = false;
        }

        public void SetBrightnessState(bool available, int value, string status)
        {
            _brightnessAvailable = available;
            _brightnessValue = available ? value : 0;
            _brightnessMessage = status;
            ApplyBrightnessState();
        }

        public void ApplySettings(AppSettings settings)
        {
            if (settings == null) return;
            PanelLayoutMode oldLayout = _preferredLayout;
            PanelDockEdge oldEdge = _settings.DockEdge;
            _settings = settings;
            _preferredLayout = settings.PanelLayoutMode;
            TopMost = settings.AlwaysOnTop;
            ApplyLanguage();
            if (_loaded && Visible && (oldLayout != _preferredLayout || oldEdge != settings.DockEdge))
                SetLayoutMode(_preferredLayout, false);
            if (settings.AutoCollapse) ResetCollapseTimer();
            else _collapseTimer.Stop();
        }

        public void ApplyLanguage()
        {
            Font = AppText.CreateFont(10F, FontStyle.Regular);
            ApplyFonts(this);
            _titleLabel.Text = AppText.Get("Panel.Title");
            _volumeTitle.Text = AppText.Get("Panel.Volume");
            _brightnessTitle.Text = AppText.Get("Panel.Brightness");
            _verticalVolumeTitle.Text = AppText.Get("Panel.Volume");
            _verticalBrightnessTitle.Text = AppText.Get("Panel.Brightness");
            _volumeDownButton.Text = AppText.Get("Panel.Quieter");
            _volumeUpButton.Text = AppText.Get("Panel.Louder");
            _brightnessDownButton.Text = AppText.Get("Panel.Dimmer");
            _brightnessUpButton.Text = AppText.Get("Panel.Brighter");
            _brightnessRetry.Text = AppText.Get("Common.TryAgain");
            _settingsButton.Text = AppText.Get("Common.Settings");
            _displaySettingsButton.Text = AppText.Get("Panel.DisplaySettings");
            _volumeSlider.AccessibleName = AppText.Get("Panel.Volume");
            _brightnessSlider.AccessibleName = AppText.Get("Panel.Brightness");
            _views[PanelLayoutMode.HorizontalMini].AccessibleName = AppText.Get("Panel.HorizontalMini.Accessible");
            _views[PanelLayoutMode.VerticalMini].AccessibleName = AppText.Get("Panel.VerticalMini.Accessible");
            _views[PanelLayoutMode.EdgeDock].AccessibleName = AppText.Get("Panel.EdgeTab.Accessible");
            _views[PanelLayoutMode.EdgeDock].AccessibleDescription = AppText.Get("Panel.EdgeTab.Description");

            SetAccessibility(_fullLayoutButton, "Settings.PanelLayout");
            SetAccessibility(_fullCollapseButton, "Panel.Collapse.Accessible");
            SetAccessibility(_fullHideButton, "Panel.HideToTray.Accessible");
            SetAccessibility(_horizontalExpand, "Panel.Expand.Accessible");
            SetAccessibility(_horizontalHide, "Panel.HideToTray.Accessible");
            SetAccessibility(_horizontalMute, "Panel.Mute.Accessible");
            SetAccessibility(_verticalCollapse, "Panel.ReturnToEdgeTab.Accessible");
            SetAccessibility(_verticalExpand, "Panel.OpenFull.Accessible");
            SetAccessibility(_verticalHide, "Panel.HideToTray.Accessible");
            SetAccessibility(_verticalMute, "Panel.Mute.Accessible");
            SetAccessibility(_edgeOpenButton, "EdgeTab.ClickToOpen");
            SetAccessibility(_volumeDownButton, "Panel.DecreaseVolume.Accessible");
            SetAccessibility(_volumeUpButton, "Panel.IncreaseVolume.Accessible");
            SetAccessibility(_muteButton, "Panel.Mute.Accessible");
            SetAccessibility(_brightnessDownButton, "Panel.DecreaseBrightness.Accessible");
            SetAccessibility(_brightnessUpButton, "Panel.IncreaseBrightness.Accessible");
            SetAccessibility(_horizontalVolumeDown, "Panel.DecreaseVolume.Accessible");
            SetAccessibility(_horizontalVolumeUp, "Panel.IncreaseVolume.Accessible");
            SetAccessibility(_horizontalBrightnessDown, "Panel.DecreaseBrightness.Accessible");
            SetAccessibility(_horizontalBrightnessUp, "Panel.IncreaseBrightness.Accessible");
            SetAccessibility(_verticalVolumeDown, "Panel.DecreaseVolume.Accessible");
            SetAccessibility(_verticalVolumeUp, "Panel.IncreaseVolume.Accessible");
            SetAccessibility(_verticalBrightnessDown, "Panel.DecreaseBrightness.Accessible");
            SetAccessibility(_verticalBrightnessUp, "Panel.IncreaseBrightness.Accessible");

            _layoutItems[PanelLayoutMode.Full].Text = AppText.Get("Layout.Full");
            _layoutItems[PanelLayoutMode.HorizontalMini].Text = AppText.Get("Layout.HorizontalMini");
            _layoutItems[PanelLayoutMode.VerticalMini].Text = AppText.Get("Layout.VerticalMini");
            _layoutItems[PanelLayoutMode.EdgeDock].Text = AppText.Get("Layout.EdgeDock");
            _menuSettings.Text = AppText.Get("Common.Settings");
            _menuHide.Text = AppText.Get("Panel.HideToTray.Accessible");
            Font oldLayoutMenuFont = _layoutMenu.Font;
            _layoutMenu.Font = AppText.CreateFont(9.5F, FontStyle.Regular);
            if (oldLayoutMenuFont != null) oldLayoutMenuFont.Dispose();
            SetAccessibility(_verticalCollapse,
                _preferredLayout == PanelLayoutMode.EdgeDock
                    ? "Panel.ReturnToEdgeTab.Accessible"
                    : "Panel.HideToTray.Accessible");
            ApplyAudioState();
            ApplyBrightnessState();
        }

        public void SetLayoutMode(PanelLayoutMode mode, bool savePreference)
        {
            if (!Enum.IsDefined(typeof(PanelLayoutMode), mode)) mode = PanelLayoutMode.Full;
            if (savePreference)
            {
                _preferredLayout = mode;
                _settings.PanelLayoutMode = mode;
                _settings.PanelCompact = mode == PanelLayoutMode.HorizontalMini;
                SetAccessibility(_verticalCollapse,
                    mode == PanelLayoutMode.EdgeDock
                        ? "Panel.ReturnToEdgeTab.Accessible"
                        : "Panel.HideToTray.Accessible");
            }
            if (!_loaded)
            {
                _currentLayout = mode;
                return;
            }

            Rectangle oldBounds = Bounds;
            Size targetSize = _scaledSizes[mode];
            Rectangle area = Screen.FromRectangle(oldBounds).WorkingArea;
            Rectangle target;
            if (mode == PanelLayoutMode.EdgeDock)
            {
                _resolvedDockEdge = _settings.DockEdge == PanelDockEdge.Automatic
                    ? PanelPlacement.FindNearestEdge(oldBounds, area)
                    : _settings.DockEdge;
                target = PanelPlacement.GetDockBounds(oldBounds, area, targetSize, _resolvedDockEdge);
            }
            else
            {
                int centerX = oldBounds.Left + oldBounds.Width / 2;
                int centerY = oldBounds.Top + oldBounds.Height / 2;
                if (_preferredLayout == PanelLayoutMode.EdgeDock &&
                    (_currentLayout == PanelLayoutMode.EdgeDock || IsNearScreenEdge(oldBounds, area)))
                {
                    int left = _resolvedDockEdge == PanelDockEdge.Left
                        ? area.Left + 4
                        : area.Right - targetSize.Width - 4;
                    target = new Rectangle(left, centerY - targetSize.Height / 2, targetSize.Width, targetSize.Height);
                }
                else
                {
                    target = new Rectangle(centerX - targetSize.Width / 2,
                        centerY - targetSize.Height / 2, targetSize.Width, targetSize.Height);
                }
                target = PanelPlacement.Clamp(target, InsetArea(area, 4));
            }

            _applyingBounds = true;
            try
            {
                _currentLayout = mode;
                foreach (KeyValuePair<PanelLayoutMode, Panel> pair in _views) pair.Value.Visible = pair.Key == mode;
                Bounds = target;
                UpdateLayoutMenuChecks();
                UiHelpers.ApplyRoundedRegion(this, mode == PanelLayoutMode.EdgeDock ? 2 : 4);
            }
            finally { _applyingBounds = false; }
            ResetCollapseTimer();
            Raise(CompactStateChanged);
            Raise(PanelPositionChanged);
        }

        public void ShowPreferred()
        {
            if (!Visible) Show();
            SetLayoutMode(_preferredLayout, false);
            ClampToVisibleArea();
            BringToFront();
            Activate();
            ResetCollapseTimer();
        }

        public void ShowPreferredPassive()
        {
            bool previousValue = _showWithoutActivation;
            _showWithoutActivation = true;
            try
            {
                if (!Visible) Show();
                SetLayoutMode(_preferredLayout, false);
                ClampToVisibleArea();
                ResetCollapseTimer();
            }
            finally
            {
                _showWithoutActivation = previousValue;
            }
        }

        public void ShowExpanded()
        {
            if (!Visible) Show();
            SetLayoutMode(PanelLayoutMode.Full, false);
            ClampToVisibleArea();
            BringToFront();
            Activate();
            ResetCollapseTimer();
        }

        public void TogglePanel()
        {
            if (!Visible) ShowPreferred();
            else if (_currentLayout == PanelLayoutMode.EdgeDock)
            {
                SetLayoutMode(PanelLayoutMode.VerticalMini, false);
                BringToFront();
                Activate();
            }
            else Hide();
        }

        public void HandleDisplayConfigurationChanged()
        {
            if (!_loaded) return;
            if (_currentLayout == PanelLayoutMode.EdgeDock) SetLayoutMode(PanelLayoutMode.EdgeDock, false);
            else ClampToVisibleArea();
        }

        public Point GetSavedLocationFor(PanelLayoutMode layout, PanelDockEdge edge)
        {
            if (!_loaded || !_scaledSizes.ContainsKey(layout))
            {
                return new Point((int)Math.Round(_settings.PanelLeft), (int)Math.Round(_settings.PanelTop));
            }
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            Size size = _scaledSizes[layout];
            Rectangle target;
            if (layout == PanelLayoutMode.EdgeDock)
            {
                PanelDockEdge resolved = edge == PanelDockEdge.Automatic
                    ? PanelPlacement.FindNearestEdge(Bounds, area)
                    : edge;
                target = PanelPlacement.GetDockBounds(Bounds, area, size, resolved);
            }
            else
            {
                target = new Rectangle(
                    Left + Width / 2 - size.Width / 2,
                    Top + Height / 2 - size.Height / 2,
                    size.Width,
                    size.Height);
                target = PanelPlacement.Clamp(target, InsetArea(area, 4));
            }
            return target.Location;
        }

        public void ResetPosition()
        {
            if (!_loaded) return;

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Size size = _scaledSizes[_preferredLayout];
            Rectangle target = new Rectangle(
                area.Right - size.Width - 18,
                area.Top + (area.Height - size.Height) / 2,
                size.Width,
                size.Height);
            if (_preferredLayout == PanelLayoutMode.EdgeDock)
            {
                _resolvedDockEdge = _settings.DockEdge == PanelDockEdge.Automatic
                    ? PanelDockEdge.Right
                    : _settings.DockEdge;
                target = PanelPlacement.GetDockBounds(target, area, size, _resolvedDockEdge);
            }
            else
            {
                target = PanelPlacement.Clamp(target, InsetArea(area, 4));
            }

            _applyingBounds = true;
            try
            {
                _currentLayout = _preferredLayout;
                foreach (KeyValuePair<PanelLayoutMode, Panel> pair in _views)
                    pair.Value.Visible = pair.Key == _currentLayout;
                Bounds = target;
                UpdateLayoutMenuChecks();
                UiHelpers.ApplyRoundedRegion(this, _currentLayout == PanelLayoutMode.EdgeDock ? 2 : 4);
            }
            finally { _applyingBounds = false; }
            ResetCollapseTimer();
            Raise(CompactStateChanged);
            Raise(PanelPositionChanged);
        }

        public void PrepareForExit()
        {
            _allowClose = true;
            _collapseTimer.Stop();
        }

        protected override void OnLoad(EventArgs eventArgs)
        {
            base.OnLoad(eventArgs);
            float scale;
            using (Graphics graphics = CreateGraphics()) scale = Math.Max(1F, graphics.DpiX / 96F);
            UpdateScaledSizes(scale);
            _loaded = true;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            bool saved = _settings.PanelLeft != -1D || _settings.PanelTop != -1D;
            Point location = saved
                ? new Point((int)_settings.PanelLeft, (int)_settings.PanelTop)
                : new Point(area.Right - _scaledSizes[_preferredLayout].Width - 18,
                    area.Top + (area.Height - _scaledSizes[_preferredLayout].Height) / 2);
            if (saved) area = Screen.FromPoint(location).WorkingArea;
            _applyingBounds = true;
            try
            {
                _currentLayout = _preferredLayout;
                foreach (KeyValuePair<PanelLayoutMode, Panel> pair in _views) pair.Value.Visible = pair.Key == _currentLayout;
                Size size = _scaledSizes[_currentLayout];
                Rectangle target = new Rectangle(location, size);
                if (_currentLayout == PanelLayoutMode.EdgeDock)
                {
                    _resolvedDockEdge = _settings.DockEdge == PanelDockEdge.Automatic
                        ? PanelPlacement.FindNearestEdge(target, area)
                        : _settings.DockEdge;
                    target = PanelPlacement.GetDockBounds(target, area, size, _resolvedDockEdge);
                }
                else target = PanelPlacement.Clamp(target, InsetArea(area, 4));
                Bounds = target;
                UpdateLayoutMenuChecks();
            }
            finally { _applyingBounds = false; }
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);
            ResetCollapseTimer();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (_preferredLayout == PanelLayoutMode.EdgeDock && _currentLayout != PanelLayoutMode.EdgeDock)
                    SetLayoutMode(PanelLayoutMode.EdgeDock, false);
                else Hide();
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_collapseTimer != null) _collapseTimer.Dispose();
                if (_toolTip != null) _toolTip.Dispose();
                if (_layoutMenu != null)
                {
                    Font layoutMenuFont = _layoutMenu.Font;
                    _layoutMenu.Dispose();
                    if (layoutMenuFont != null) layoutMenuFont.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message message)
        {
            const int dpiChangedMessage = 0x02E0;
            int newDpi = message.Msg == dpiChangedMessage
                ? (int)(message.WParam.ToInt64() & 0xFFFF)
                : 0;
            base.WndProc(ref message);
            if (newDpi > 0 && _loaded)
            {
                UpdateScaledSizes(Math.Max(1F, newDpi / 96F));
            }
        }

        private void BuildFullView()
        {
            Panel view = CreateView(PanelLayoutMode.Full);
            AddAccentLine(view);
            Panel header = new Panel();
            header.BackColor = Color.Transparent;
            header.SetBounds(0, 4, FullWidth - 2, 60);
            header.MouseDown += DragWindow;
            view.Controls.Add(header);
            LogoControl logo = new LogoControl();
            logo.SetBounds(16, 14, 34, 34);
            logo.MouseDown += DragWindow;
            header.Controls.Add(logo);
            _titleLabel = CreateLabel(string.Empty, 18F, FontStyle.Bold, AppColors.Text);
            _titleLabel.SetBounds(62, 12, 250, 38);
            _titleLabel.MouseDown += DragWindow;
            header.Controls.Add(_titleLabel);
            _fullLayoutButton = CreateGlyphButton(QuickGlyph.Layout, 328, 16, 30, 30);
            _fullLayoutButton.Click += ShowLayoutMenu;
            header.Controls.Add(_fullLayoutButton);
            _fullCollapseButton = CreateGlyphButton(QuickGlyph.Collapse, 366, 16, 30, 30);
            _fullCollapseButton.Click += delegate { CollapseFullNow(); };
            header.Controls.Add(_fullCollapseButton);
            _fullHideButton = CreateGlyphButton(QuickGlyph.Close, 404, 16, 30, 30);
            _fullHideButton.Click += delegate { Hide(); };
            header.Controls.Add(_fullHideButton);

            RoundedPanel volumeCard = CreateCard(16, 72, 408, 136, AppColors.Accent);
            view.Controls.Add(volumeCard);
            _volumeTitle = CreateLabel(string.Empty, 11F, FontStyle.Bold, AppColors.Text);
            _volumeTitle.SetBounds(20, 10, 230, 26);
            volumeCard.Controls.Add(_volumeTitle);
            _volumePercent = CreateValueLabel(306, 6, 82, 34, 18F);
            volumeCard.Controls.Add(_volumePercent);
            _volumeSlider = new ModernSlider();
            _volumeSlider.SetBounds(18, 40, 370, 34);
            _volumeSlider.UserValueChanged += delegate { ResetCollapseTimer(); RaiseInt(VolumeChanged, _volumeSlider.Value); };
            volumeCard.Controls.Add(_volumeSlider);
            _volumeDownButton = CreateButton(18, 84, 92, 40);
            _volumeDownButton.Click += delegate { ResetCollapseTimer(); RaiseInt(VolumeStepRequested, -1); };
            volumeCard.Controls.Add(_volumeDownButton);
            _muteButton = CreateButton(118, 84, 170, 40);
            _muteButton.Click += delegate { ResetCollapseTimer(); Raise(MuteRequested); };
            volumeCard.Controls.Add(_muteButton);
            _volumeUpButton = CreateButton(296, 84, 92, 40);
            _volumeUpButton.Click += delegate { ResetCollapseTimer(); RaiseInt(VolumeStepRequested, 1); };
            volumeCard.Controls.Add(_volumeUpButton);
            _audioStatus = CreateLabel(string.Empty, 8.5F, FontStyle.Regular, AppColors.Danger);
            _audioStatus.SetBounds(18, 40, 370, 38);
            volumeCard.Controls.Add(_audioStatus);

            RoundedPanel brightnessCard = CreateCard(16, 220, 408, 164, AppColors.Accent2);
            view.Controls.Add(brightnessCard);
            _brightnessTitle = CreateLabel(string.Empty, 11F, FontStyle.Bold, AppColors.Text);
            _brightnessTitle.SetBounds(20, 8, 230, 26);
            brightnessCard.Controls.Add(_brightnessTitle);
            _brightnessPercent = CreateValueLabel(306, 4, 82, 34, 18F);
            brightnessCard.Controls.Add(_brightnessPercent);
            _displayCombo = new ComboBox();
            _displayCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _displayCombo.FlatStyle = FlatStyle.Flat;
            _displayCombo.BackColor = AppColors.CardHover;
            _displayCombo.ForeColor = AppColors.Text;
            _displayCombo.Font = AppText.CreateFont(9.5F, FontStyle.Regular);
            _displayCombo.SetBounds(18, 36, 370, 28);
            _displayCombo.SelectedIndexChanged += DisplaySelectionChanged;
            brightnessCard.Controls.Add(_displayCombo);
            _displayName = CreateLabel(string.Empty, 9F, FontStyle.Regular, AppColors.MutedText);
            _displayName.SetBounds(18, 38, 370, 24);
            brightnessCard.Controls.Add(_displayName);
            _brightnessSlider = new ModernSlider();
            _brightnessSlider.AccentColor = AppColors.Accent2;
            _brightnessSlider.SetBounds(18, 68, 370, 32);
            _brightnessSlider.UserValueChanged += delegate { ResetCollapseTimer(); RaiseInt(BrightnessChanged, _brightnessSlider.Value); };
            brightnessCard.Controls.Add(_brightnessSlider);
            _brightnessDownButton = CreateButton(18, 108, 181, 40);
            _brightnessDownButton.Click += delegate { ResetCollapseTimer(); RaiseInt(BrightnessStepRequested, -1); };
            brightnessCard.Controls.Add(_brightnessDownButton);
            _brightnessUpButton = CreateButton(207, 108, 181, 40);
            _brightnessUpButton.Click += delegate { ResetCollapseTimer(); RaiseInt(BrightnessStepRequested, 1); };
            brightnessCard.Controls.Add(_brightnessUpButton);
            _brightnessRetry = CreateButton(18, 108, 370, 40);
            _brightnessRetry.Visible = false;
            _brightnessRetry.Click += delegate { ResetCollapseTimer(); Raise(BrightnessRetryRequested); };
            brightnessCard.Controls.Add(_brightnessRetry);
            _brightnessStatus = CreateLabel(string.Empty, 8.5F, FontStyle.Regular, AppColors.Danger);
            _brightnessStatus.SetBounds(18, 66, 370, 38);
            brightnessCard.Controls.Add(_brightnessStatus);
            _settingsButton = CreateButton(16, 400, 196, 40);
            _settingsButton.Click += delegate { Raise(SettingsRequested); };
            view.Controls.Add(_settingsButton);
            _displaySettingsButton = CreateButton(220, 400, 204, 40);
            _displaySettingsButton.Click += delegate { Raise(OpenWindowsDisplaySettingsRequested); };
            view.Controls.Add(_displaySettingsButton);
        }

        private void BuildHorizontalView()
        {
            Panel view = CreateView(PanelLayoutMode.HorizontalMini);
            AddAccentLine(view);
            GlyphControl grip = CreateGlyph(QuickGlyph.Grip, AppColors.MutedText, 8, 20, 20, 34);
            grip.Cursor = Cursors.SizeAll;
            grip.MouseDown += DragWindow;
            view.Controls.Add(grip);
            view.Controls.Add(CreateGlyph(QuickGlyph.Speaker, AppColors.Accent, 34, 20, 30, 34));
            _horizontalVolumeDown = CreateMiniButton("−", 66, 16, 40, 40);
            _horizontalVolumeDown.Click += delegate { RaiseInt(VolumeStepRequested, -1); ResetCollapseTimer(); };
            view.Controls.Add(_horizontalVolumeDown);
            _horizontalVolumePercent = CreateValueLabel(108, 16, 58, 40, 11F);
            view.Controls.Add(_horizontalVolumePercent);
            _horizontalVolumeUp = CreateMiniButton("+", 168, 16, 40, 40);
            _horizontalVolumeUp.Click += delegate { RaiseInt(VolumeStepRequested, 1); ResetCollapseTimer(); };
            view.Controls.Add(_horizontalVolumeUp);
            _horizontalMute = CreateGlyphButton(QuickGlyph.Speaker, 212, 16, 40, 40);
            _horizontalMute.Click += delegate { Raise(MuteRequested); ResetCollapseTimer(); };
            view.Controls.Add(_horizontalMute);
            Panel divider = new Panel();
            divider.BackColor = AppColors.Border;
            divider.SetBounds(260, 14, 1, 44);
            view.Controls.Add(divider);
            view.Controls.Add(CreateGlyph(QuickGlyph.Sun, AppColors.Accent2, 268, 20, 30, 34));
            _horizontalBrightnessDown = CreateMiniButton("−", 300, 16, 40, 40);
            _horizontalBrightnessDown.Click += delegate { RaiseInt(BrightnessStepRequested, -1); ResetCollapseTimer(); };
            view.Controls.Add(_horizontalBrightnessDown);
            _horizontalBrightnessPercent = CreateValueLabel(342, 16, 58, 40, 11F);
            view.Controls.Add(_horizontalBrightnessPercent);
            _horizontalBrightnessUp = CreateMiniButton("+", 402, 16, 40, 40);
            _horizontalBrightnessUp.Click += delegate { RaiseInt(BrightnessStepRequested, 1); ResetCollapseTimer(); };
            view.Controls.Add(_horizontalBrightnessUp);
            _horizontalExpand = CreateGlyphButton(QuickGlyph.Expand, 448, 16, 30, 40);
            _horizontalExpand.Click += delegate { ShowExpanded(); };
            view.Controls.Add(_horizontalExpand);
            _horizontalHide = CreateGlyphButton(QuickGlyph.Close, 484, 16, 30, 40);
            _horizontalHide.Click += delegate { Hide(); };
            view.Controls.Add(_horizontalHide);
        }

        private void BuildVerticalView()
        {
            Panel view = CreateView(PanelLayoutMode.VerticalMini);
            AddAccentLine(view);
            GlyphControl grip = CreateGlyph(QuickGlyph.Grip, AppColors.MutedText, 8, 12, 20, 28);
            grip.Cursor = Cursors.SizeAll;
            grip.MouseDown += DragWindow;
            view.Controls.Add(grip);
            LogoControl logo = new LogoControl();
            logo.SetBounds(34, 10, 30, 30);
            logo.MouseDown += DragWindow;
            view.Controls.Add(logo);
            _verticalCollapse = CreateGlyphButton(QuickGlyph.Collapse, 72, 10, 26, 30);
            _verticalCollapse.Click += delegate { ReturnToPreferred(); };
            view.Controls.Add(_verticalCollapse);
            _verticalHide = CreateGlyphButton(QuickGlyph.Close, 102, 10, 26, 30);
            _verticalHide.Click += delegate { Hide(); };
            view.Controls.Add(_verticalHide);
            _verticalVolumeTitle = CreateCenteredLabel(8, 52, 120, 20, 8.5F, AppColors.MutedText);
            view.Controls.Add(_verticalVolumeTitle);
            _verticalVolumePercent = CreateValueLabel(8, 72, 120, 34, 16F);
            view.Controls.Add(_verticalVolumePercent);
            _verticalVolumeDown = CreateMiniButton("−", 10, 108, 52, 40);
            _verticalVolumeDown.Click += delegate { RaiseInt(VolumeStepRequested, -1); ResetCollapseTimer(); };
            view.Controls.Add(_verticalVolumeDown);
            _verticalVolumeUp = CreateMiniButton("+", 74, 108, 52, 40);
            _verticalVolumeUp.Click += delegate { RaiseInt(VolumeStepRequested, 1); ResetCollapseTimer(); };
            view.Controls.Add(_verticalVolumeUp);
            _verticalMute = CreateGlyphButton(QuickGlyph.Speaker, 10, 154, 116, 36);
            _verticalMute.Click += delegate { Raise(MuteRequested); ResetCollapseTimer(); };
            view.Controls.Add(_verticalMute);
            Panel divider = new Panel();
            divider.BackColor = AppColors.Border;
            divider.SetBounds(12, 199, 112, 1);
            view.Controls.Add(divider);
            _verticalBrightnessTitle = CreateCenteredLabel(8, 207, 120, 20, 8.5F, AppColors.MutedText);
            view.Controls.Add(_verticalBrightnessTitle);
            _verticalBrightnessPercent = CreateValueLabel(8, 227, 120, 34, 16F);
            view.Controls.Add(_verticalBrightnessPercent);
            _verticalBrightnessDown = CreateMiniButton("−", 10, 263, 52, 40);
            _verticalBrightnessDown.Click += delegate { RaiseInt(BrightnessStepRequested, -1); ResetCollapseTimer(); };
            view.Controls.Add(_verticalBrightnessDown);
            _verticalBrightnessUp = CreateMiniButton("+", 74, 263, 52, 40);
            _verticalBrightnessUp.Click += delegate { RaiseInt(BrightnessStepRequested, 1); ResetCollapseTimer(); };
            view.Controls.Add(_verticalBrightnessUp);
            _verticalExpand = CreateGlyphButton(QuickGlyph.Expand, 10, 312, 116, 34);
            _verticalExpand.Click += delegate { ShowExpanded(); };
            view.Controls.Add(_verticalExpand);
        }

        private void BuildEdgeView()
        {
            Panel view = CreateView(PanelLayoutMode.EdgeDock);
            Panel accent = new Panel();
            accent.BackColor = AppColors.Accent;
            accent.SetBounds(0, 0, 4, EdgeHeight);
            view.Controls.Add(accent);
            LogoControl logo = new LogoControl();
            logo.SetBounds(10, 10, 28, 28);
            view.Controls.Add(logo);
            _edgeVolumeGlyph = CreateGlyph(QuickGlyph.Speaker, AppColors.Accent, 9, 48, 30, 28);
            view.Controls.Add(_edgeVolumeGlyph);
            _edgeVolumePercent = CreateValueLabel(5, 76, 38, 28, 9.5F);
            view.Controls.Add(_edgeVolumePercent);
            _edgeVolumeProgress = new ModernProgress();
            _edgeVolumeProgress.AccentColor = AppColors.Accent;
            _edgeVolumeProgress.SetBounds(8, 106, 32, 8);
            view.Controls.Add(_edgeVolumeProgress);
            view.Controls.Add(CreateGlyph(QuickGlyph.Sun, AppColors.Accent2, 9, 126, 30, 28));
            _edgeBrightnessPercent = CreateValueLabel(5, 154, 38, 28, 9.5F);
            view.Controls.Add(_edgeBrightnessPercent);
            _edgeBrightnessProgress = new ModernProgress();
            _edgeBrightnessProgress.AccentColor = AppColors.Accent2;
            _edgeBrightnessProgress.SetBounds(8, 184, 32, 8);
            view.Controls.Add(_edgeBrightnessProgress);
            _edgeOpenButton = CreateGlyphButton(QuickGlyph.Expand, 8, 198, 32, 28);
            _edgeOpenButton.Click += delegate { SetLayoutMode(PanelLayoutMode.VerticalMini, false); Activate(); };
            view.Controls.Add(_edgeOpenButton);
            AttachEdgeOpen(view);
        }

        private Panel CreateView(PanelLayoutMode mode)
        {
            Panel view = new Panel();
            view.Dock = DockStyle.Fill;
            view.BackColor = AppColors.Window;
            view.Visible = mode == PanelLayoutMode.Full;
            view.MouseUp += ShowContextMenu;
            Controls.Add(view);
            _views[mode] = view;
            return view;
        }

        private static RoundedPanel CreateCard(int x, int y, int width, int height, Color accent)
        {
            RoundedPanel card = new RoundedPanel();
            card.SetBounds(x, y, width, height);
            Panel rule = new Panel();
            rule.BackColor = accent;
            rule.SetBounds(0, 12, 3, height - 24);
            card.Controls.Add(rule);
            return card;
        }

        private static void AddAccentLine(Control parent)
        {
            Panel line = new Panel();
            line.BackColor = AppColors.Accent;
            line.SetBounds(0, 0, parent.Width, 4);
            line.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(line);
        }

        private ContextMenuStrip BuildLayoutMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = AppText.CreateFont(9.5F, FontStyle.Regular);
            AddLayoutItem(menu, PanelLayoutMode.Full, "Layout.Full");
            AddLayoutItem(menu, PanelLayoutMode.HorizontalMini, "Layout.HorizontalMini");
            AddLayoutItem(menu, PanelLayoutMode.VerticalMini, "Layout.VerticalMini");
            AddLayoutItem(menu, PanelLayoutMode.EdgeDock, "Layout.EdgeDock");
            menu.Items.Add(new ToolStripSeparator());
            _menuSettings = new ToolStripMenuItem();
            _menuSettings.Click += delegate { Raise(SettingsRequested); };
            menu.Items.Add(_menuSettings);
            _menuHide = new ToolStripMenuItem();
            _menuHide.Click += delegate { Hide(); };
            menu.Items.Add(_menuHide);
            return menu;
        }

        private void AddLayoutItem(ContextMenuStrip menu, PanelLayoutMode mode, string key)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(AppText.Get(key));
            item.Click += delegate { SetLayoutMode(mode, true); };
            menu.Items.Add(item);
            _layoutItems[mode] = item;
        }

        private void ShowLayoutMenu(object sender, EventArgs eventArgs)
        {
            Control control = sender as Control;
            UpdateLayoutMenuChecks();
            _layoutMenu.Show(control, new Point(0, control == null ? 0 : control.Height));
        }

        private void ShowContextMenu(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Right) return;
            UpdateLayoutMenuChecks();
            _layoutMenu.Show(sender as Control, eventArgs.Location);
        }

        private void UpdateLayoutMenuChecks()
        {
            foreach (KeyValuePair<PanelLayoutMode, ToolStripMenuItem> pair in _layoutItems)
                pair.Value.Checked = pair.Key == _preferredLayout;
        }

        private void ApplyAudioState()
        {
            if (_volumeSlider == null) return;
            string text = _audioAvailable ? (_muted ? AppText.Get("Panel.VolumeMuted") : _volumeValue + "%") : "--%";
            _volumePercent.Text = text;
            _horizontalVolumePercent.Text = text;
            _verticalVolumePercent.Text = text;
            _edgeVolumePercent.Text = _audioAvailable ? _volumeValue + "%" : "--";
            _edgeVolumeProgress.Value = _audioAvailable ? _volumeValue : 0;
            _volumeSlider.Value = _audioAvailable ? _volumeValue : 0;
            _volumeSlider.Visible = _audioAvailable;
            _volumeSlider.Enabled = _audioAvailable;
            SetEnabled(_audioAvailable, _muteButton, _volumeDownButton, _volumeUpButton,
                _horizontalVolumeDown, _horizontalVolumeUp, _horizontalMute,
                _verticalVolumeDown, _verticalVolumeUp, _verticalMute);
            _muteButton.Text = _muted ? AppText.Get("Panel.Unmute") : AppText.Get("Panel.Mute");
            _horizontalMute.Muted = _muted;
            _verticalMute.Muted = _muted;
            _edgeVolumeGlyph.Muted = _muted;
            _horizontalMute.Invalidate();
            _verticalMute.Invalidate();
            _edgeVolumeGlyph.Invalidate();
            _audioStatus.Text = _audioAvailable ? string.Empty : AppText.Get("Panel.AudioUnavailable");
            _audioStatus.Visible = !_audioAvailable;
            string accessible = _audioAvailable
                ? (_muted ? AppText.Get("Panel.VolumeMuted") : AppText.Format("Panel.VolumePercent.Accessible", _volumeValue))
                : AppText.Get("Panel.AudioUnavailable");
            _volumePercent.AccessibleName = accessible;
            _horizontalVolumePercent.AccessibleName = accessible;
            _verticalVolumePercent.AccessibleName = accessible;
            _edgeVolumePercent.AccessibleName = accessible;
        }

        private void ApplyBrightnessState()
        {
            if (_brightnessSlider == null) return;
            string text = _brightnessAvailable ? _brightnessValue + "%" : "--%";
            _brightnessPercent.Text = text;
            _horizontalBrightnessPercent.Text = text;
            _verticalBrightnessPercent.Text = text;
            _edgeBrightnessPercent.Text = _brightnessAvailable ? _brightnessValue + "%" : "--";
            _edgeBrightnessProgress.Value = _brightnessAvailable ? _brightnessValue : 0;
            _brightnessSlider.Value = _brightnessAvailable ? _brightnessValue : 0;
            _brightnessSlider.Enabled = _brightnessAvailable;
            _brightnessSlider.Visible = _brightnessAvailable;
            SetEnabled(_brightnessAvailable, _brightnessDownButton, _brightnessUpButton,
                _horizontalBrightnessDown, _horizontalBrightnessUp,
                _verticalBrightnessDown, _verticalBrightnessUp);
            _brightnessDownButton.Visible = _brightnessAvailable;
            _brightnessUpButton.Visible = _brightnessAvailable;
            _brightnessStatus.Text = _brightnessAvailable ? string.Empty :
                (string.IsNullOrEmpty(_brightnessMessage) ? AppText.Get("Panel.DisplayUnsupported") : _brightnessMessage);
            _brightnessStatus.Visible = !_brightnessAvailable;
            _brightnessRetry.Visible = !_brightnessAvailable;
            _brightnessRetry.Enabled = !_brightnessAvailable;
            string accessible = _brightnessAvailable
                ? AppText.Format("Panel.BrightnessPercent.Accessible", _brightnessValue)
                : AppText.Get("Panel.BrightnessUnavailable");
            _brightnessPercent.AccessibleName = accessible;
            _horizontalBrightnessPercent.AccessibleName = accessible;
            _verticalBrightnessPercent.AccessibleName = accessible;
        }

        private void CollapseFullNow()
        {
            SetLayoutMode(GetIdleLayout(), false);
        }

        private void ReturnToPreferred()
        {
            if (_currentLayout != _preferredLayout) SetLayoutMode(_preferredLayout, false);
            else Hide();
        }

        private void CollapseTimerTick(object sender, EventArgs eventArgs)
        {
            if (!_settings.AutoCollapse || !Visible)
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
            PanelLayoutMode target = GetIdleLayout();
            if (_currentLayout != target) SetLayoutMode(target, false);
            else _collapseTimer.Stop();
        }

        private void ResetCollapseTimer()
        {
            _collapseTimer.Stop();
            if (!_settings.AutoCollapse || !Visible) return;
            if (_currentLayout != GetIdleLayout()) _collapseTimer.Start();
        }

        private PanelLayoutMode GetIdleLayout()
        {
            return _preferredLayout == PanelLayoutMode.Full
                ? PanelLayoutMode.HorizontalMini
                : _preferredLayout;
        }

        private void DisplaySelectionChanged(object sender, EventArgs eventArgs)
        {
            if (_updatingDisplaySelection || _displayCombo.SelectedIndex < 0) return;
            ResetCollapseTimer();
            RaiseInt(DisplaySelectionRequested, _displayCombo.SelectedIndex);
        }

        private void DragWindow(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left) return;
            _collapseTimer.Stop();
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            if (_currentLayout == PanelLayoutMode.EdgeDock) SetLayoutMode(PanelLayoutMode.EdgeDock, false);
            else ClampToVisibleArea();
            ResetCollapseTimer();
        }

        private void PanelLocationChanged(object sender, EventArgs eventArgs)
        {
            if (_applyingBounds || !_loaded) return;
            ClampToVisibleArea();
            Raise(PanelPositionChanged);
        }

        private void ClampToVisibleArea()
        {
            if (!_loaded || _applyingBounds) return;
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            Rectangle target = _currentLayout == PanelLayoutMode.EdgeDock
                ? PanelPlacement.GetDockBounds(Bounds, area, Size, _resolvedDockEdge)
                : PanelPlacement.Clamp(Bounds, InsetArea(area, 4));
            if (target == Bounds) return;
            _applyingBounds = true;
            try { Bounds = target; }
            finally { _applyingBounds = false; }
        }

        private void AttachEdgeOpen(Control parent)
        {
            parent.Cursor = Cursors.Hand;
            parent.AccessibleName = AppText.Get("Panel.EdgeTab.Accessible");
            parent.AccessibleDescription = AppText.Get("Panel.EdgeTab.Description");
            parent.Click += EdgeViewClicked;
            foreach (Control child in parent.Controls)
            {
                if (child == _edgeOpenButton) continue;
                child.Cursor = Cursors.Hand;
                child.Click += EdgeViewClicked;
            }
        }

        private void EdgeViewClicked(object sender, EventArgs eventArgs)
        {
            SetLayoutMode(PanelLayoutMode.VerticalMini, false);
            BringToFront();
            Activate();
        }

        private void PanelFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (!_allowClose && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                Hide();
            }
        }

        private void SetAccessibility(Control control, string key)
        {
            string value = AppText.Get(key);
            control.AccessibleName = value;
            _toolTip.SetToolTip(control, value);
        }

        private static void ApplyFonts(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                Font font = control.Font;
                bool ownsFont = !object.ReferenceEquals(font, parent.Font);
                if (ownsFont)
                {
                    bool emphasized = font.Bold || font.Name.IndexOf("Semibold", StringComparison.OrdinalIgnoreCase) >= 0;
                    control.Font = new Font(AppText.GetFontFamilyName(emphasized), font.SizeInPoints,
                        font.Style, GraphicsUnit.Point);
                }
                if (control.HasChildren) ApplyFonts(control);
            }
        }

        private void UpdateScaledSizes(float scale)
        {
            foreach (KeyValuePair<PanelLayoutMode, Size> pair in _logicalSizes)
            {
                _scaledSizes[pair.Key] = new Size(
                    (int)Math.Round(pair.Value.Width * scale),
                    (int)Math.Round(pair.Value.Height * scale));
            }
        }

        private static GlyphButton CreateGlyphButton(QuickGlyph glyph, int x, int y, int width, int height)
        {
            GlyphButton button = new GlyphButton();
            button.Glyph = glyph;
            button.SetBounds(x, y, width, height);
            return button;
        }

        private static GlyphControl CreateGlyph(QuickGlyph glyph, Color color, int x, int y, int width, int height)
        {
            GlyphControl control = new GlyphControl();
            control.Glyph = glyph;
            control.GlyphColor = color;
            control.SetBounds(x, y, width, height);
            return control;
        }

        private static ModernButton CreateMiniButton(string text, int x, int y, int width, int height)
        {
            ModernButton button = CreateButton(x, y, width, height);
            button.Text = text;
            button.Font = AppText.CreateFont(13F, FontStyle.Bold);
            return button;
        }

        private static ModernButton CreateButton(int x, int y, int width, int height)
        {
            ModernButton button = new ModernButton();
            button.SetBounds(x, y, width, height);
            return button;
        }

        private static Label CreateValueLabel(int x, int y, int width, int height, float size)
        {
            Label label = CreateLabel("--%", size, FontStyle.Bold, AppColors.Text);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.SetBounds(x, y, width, height);
            return label;
        }

        private static Label CreateCenteredLabel(int x, int y, int width, int height, float size, Color color)
        {
            Label label = CreateLabel(string.Empty, size, FontStyle.Bold, color);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.SetBounds(x, y, width, height);
            return label;
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = AppText.CreateFont(size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.AutoEllipsis = true;
            return label;
        }

        private static void SetEnabled(bool enabled, params Control[] controls)
        {
            for (int index = 0; index < controls.Length; index++) controls[index].Enabled = enabled;
        }

        private static Rectangle InsetArea(Rectangle area, int inset)
        {
            if (area.Width <= inset * 2 || area.Height <= inset * 2) return area;
            return new Rectangle(area.Left + inset, area.Top + inset,
                area.Width - inset * 2, area.Height - inset * 2);
        }

        private static bool IsNearScreenEdge(Rectangle bounds, Rectangle area)
        {
            return Math.Abs(bounds.Left - area.Left) <= 12 || Math.Abs(bounds.Right - area.Right) <= 12;
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

    internal enum QuickGlyph { Speaker, Sun, Grip, Layout, Collapse, Expand, Close }

    internal static class QuickGlyphPainter
    {
        public static void Draw(Graphics graphics, Rectangle bounds, QuickGlyph glyph, Color color, bool muted)
        {
            if (bounds.Width < 4 || bounds.Height < 4) return;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Math.Min(bounds.Width, bounds.Height) / 24F;
            float ox = bounds.Left + (bounds.Width - 24F * scale) / 2F;
            float oy = bounds.Top + (bounds.Height - 24F * scale) / 2F;
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(ox, oy);
            graphics.ScaleTransform(scale, scale);
            using (Pen pen = new Pen(color, 1.8F))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                switch (glyph)
                {
                    case QuickGlyph.Speaker:
                        PointF[] speaker = { new PointF(4, 10), new PointF(8, 10), new PointF(13, 6),
                            new PointF(13, 18), new PointF(8, 14), new PointF(4, 14) };
                        graphics.FillPolygon(brush, speaker);
                        if (!muted) graphics.DrawArc(pen, 13, 8, 7, 8, -55, 110);
                        if (muted) graphics.DrawLine(pen, 16, 8, 21, 16);
                        break;
                    case QuickGlyph.Sun:
                        graphics.DrawEllipse(pen, 8, 8, 8, 8);
                        for (int index = 0; index < 8; index++)
                        {
                            double angle = index * Math.PI / 4D;
                            graphics.DrawLine(pen,
                                12F + (float)Math.Cos(angle) * 6F, 12F + (float)Math.Sin(angle) * 6F,
                                12F + (float)Math.Cos(angle) * 9F, 12F + (float)Math.Sin(angle) * 9F);
                        }
                        break;
                    case QuickGlyph.Grip:
                        for (int row = 0; row < 3; row++)
                        {
                            graphics.FillEllipse(brush, 8, 6 + row * 5, 2.5F, 2.5F);
                            graphics.FillEllipse(brush, 14, 6 + row * 5, 2.5F, 2.5F);
                        }
                        break;
                    case QuickGlyph.Layout:
                        graphics.DrawRectangle(pen, 4, 5, 6, 6);
                        graphics.DrawRectangle(pen, 14, 5, 6, 6);
                        graphics.DrawRectangle(pen, 4, 15, 16, 4);
                        break;
                    case QuickGlyph.Collapse:
                        graphics.DrawLine(pen, 5, 12, 19, 12);
                        break;
                    case QuickGlyph.Expand:
                        graphics.DrawLine(pen, 5, 10, 5, 5);
                        graphics.DrawLine(pen, 5, 5, 10, 5);
                        graphics.DrawLine(pen, 14, 5, 19, 5);
                        graphics.DrawLine(pen, 19, 5, 19, 10);
                        graphics.DrawLine(pen, 19, 14, 19, 19);
                        graphics.DrawLine(pen, 19, 19, 14, 19);
                        graphics.DrawLine(pen, 10, 19, 5, 19);
                        graphics.DrawLine(pen, 5, 19, 5, 14);
                        break;
                    case QuickGlyph.Close:
                        graphics.DrawLine(pen, 7, 7, 17, 17);
                        graphics.DrawLine(pen, 17, 7, 7, 17);
                        break;
                }
            }
            graphics.Restore(state);
        }
    }

    internal sealed class GlyphControl : Control
    {
        public GlyphControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            GlyphColor = AppColors.Text;
        }
        public QuickGlyph Glyph { get; set; }
        public Color GlyphColor { get; set; }
        public bool Muted { get; set; }
        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            QuickGlyphPainter.Draw(eventArgs.Graphics, ClientRectangle, Glyph, GlyphColor, Muted);
        }
    }

    internal sealed class GlyphButton : ModernButton
    {
        public QuickGlyph Glyph { get; set; }
        public bool Muted { get; set; }
        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            Text = string.Empty;
            base.OnPaint(eventArgs);
            QuickGlyphPainter.Draw(eventArgs.Graphics, ClientRectangle, Glyph,
                Enabled ? ForeColor : AppColors.MutedText, Muted);
        }
    }

    public sealed class IntValueEventArgs : EventArgs
    {
        public IntValueEventArgs(int value) { Value = value; }
        public int Value { get; private set; }
    }
}
