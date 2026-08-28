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
    public sealed class SettingsForm : Form
    {
        private enum SettingsPage { Interface, Shortcuts, General }

        private const int LogicalWidth = 900;
        private const int LogicalHeight = 640;
        private const int TitleBarHeight = 52;
        private const int SidebarWidth = 208;
        private const int FooterHeight = 72;

        private readonly AppSettings _candidate;
        private readonly Func<AppSettings, string> _applySettings;
        private readonly Dictionary<HotkeyAction, HotkeyTextBox> _hotkeyInputs;
        private readonly Dictionary<SettingsPage, Panel> _pages;
        private readonly Dictionary<SettingsPage, ModernButton> _navButtons;
        private readonly Dictionary<PanelLayoutMode, LayoutOptionButton> _layoutButtons;
        private Panel _selectionRail;
        private readonly ModernChoiceBox _languageChoice;
        private readonly ModernChoiceBox _edgeChoice;
        private readonly ModernChoiceBox _stepChoice;
        private readonly ModernToggle _startupToggle;
        private readonly ModernToggle _alwaysOnTopToggle;
        private readonly ModernToggle _autoCollapseToggle;
        private readonly Label _statusLabel;
        private readonly Control _dockEdgeSurface;
        private PanelLayoutMode _selectedLayout;
        private bool _fitApplied;

        public SettingsForm(AppSettings settings, Func<AppSettings, string> applySettings)
        {
            _candidate = settings.Clone();
            _applySettings = applySettings;
            _hotkeyInputs = new Dictionary<HotkeyAction, HotkeyTextBox>();
            _pages = new Dictionary<SettingsPage, Panel>();
            _navButtons = new Dictionary<SettingsPage, ModernButton>();
            _layoutButtons = new Dictionary<PanelLayoutMode, LayoutOptionButton>();
            _selectedLayout = _candidate.PanelLayoutMode;

            Text = AppText.Get("Settings.WindowTitle");
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(LogicalWidth, LogicalHeight);
            BackColor = AppColors.Window;
            ForeColor = AppColors.Text;
            Font = AppText.CreateFont(10F, FontStyle.Regular);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += SettingsKeyDown;

            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = AppColors.Window;
            root.Paint += delegate(object sender, PaintEventArgs eventArgs)
            {
                using (Pen border = new Pen(AppColors.StrongBorder))
                    eventArgs.Graphics.DrawRectangle(border, 0, 0, root.Width - 1, root.Height - 1);
            };
            Controls.Add(root);

            Panel titleBar = BuildTitleBar(root);
            Panel navigation = BuildNavigation(root);

            Panel contentHost = new Panel();
            contentHost.SetBounds(SidebarWidth, TitleBarHeight,
                LogicalWidth - SidebarWidth, LogicalHeight - TitleBarHeight - FooterHeight);
            contentHost.BackColor = AppColors.Window;
            root.Controls.Add(contentHost);

            Panel interfacePage = CreatePage(contentHost, SettingsPage.Interface);
            AddPageHeader(interfacePage, AppText.Get("Settings.Interface"), AppText.Get("Settings.InterfaceIntro"));

            RoundedPanel languageSurface = CreateSurface(32, 104, 628, 80);
            interfacePage.Controls.Add(languageSurface);
            Label languageLabel = CreateLabel(AppText.Get("Settings.Language"), 10F, FontStyle.Bold, AppColors.Text);
            languageLabel.SetBounds(16, 12, 342, 25);
            languageSurface.Controls.Add(languageLabel);
            Label languageHelp = CreateLabel(AppText.Get("Settings.LanguageHelp"), 8.5F, FontStyle.Regular, AppColors.MutedText);
            languageHelp.SetBounds(16, 39, 342, 24);
            languageSurface.Controls.Add(languageHelp);
            _languageChoice = new ModernChoiceBox();
            _languageChoice.SetBounds(390, 20, 218, 40);
            _languageChoice.AccessibleName = AppText.Get("Settings.Language");
            foreach (AppLanguageOption language in AppText.LanguageOptions)
                _languageChoice.Items.Add(new LanguageChoice(language));
            SelectLanguageChoice(_candidate.LanguageCode);
            languageSurface.Controls.Add(_languageChoice);

            Label panelHeading = CreateLabel(AppText.Get("Settings.PanelLayout"), 11F, FontStyle.Bold, AppColors.Text);
            panelHeading.SetBounds(32, 204, 628, 28);
            interfacePage.Controls.Add(panelHeading);
            Label panelHelp = CreateLabel(AppText.Get("Settings.PanelLayoutHelp"), 8.8F, FontStyle.Regular, AppColors.MutedText);
            panelHelp.SetBounds(32, 232, 628, 25);
            interfacePage.Controls.Add(panelHelp);

            AddLayoutButton(interfacePage, PanelLayoutMode.Full, AppText.Get("Layout.Full"), 32, 266);
            AddLayoutButton(interfacePage, PanelLayoutMode.HorizontalMini, AppText.Get("Layout.HorizontalMini"), 356, 266);
            AddLayoutButton(interfacePage, PanelLayoutMode.VerticalMini, AppText.Get("Layout.VerticalMini"), 32, 350);
            AddLayoutButton(interfacePage, PanelLayoutMode.EdgeDock, AppText.Get("Layout.EdgeDock"), 356, 350);

            RoundedPanel dockEdgeSurface = CreateSurface(32, 438, 628, 64);
            _dockEdgeSurface = dockEdgeSurface;
            interfacePage.Controls.Add(dockEdgeSurface);
            Label edgeLabel = CreateLabel(AppText.Get("Settings.DockEdge"), 10F, FontStyle.Bold, AppColors.Text);
            edgeLabel.SetBounds(16, 18, 350, 28);
            dockEdgeSurface.Controls.Add(edgeLabel);
            _edgeChoice = new ModernChoiceBox();
            _edgeChoice.SetBounds(390, 12, 218, 40);
            _edgeChoice.AccessibleName = AppText.Get("Settings.DockEdge");
            _edgeChoice.Items.Add(new EdgeChoice(PanelDockEdge.Automatic, AppText.Get("Edge.Auto")));
            _edgeChoice.Items.Add(new EdgeChoice(PanelDockEdge.Left, AppText.Get("Edge.Left")));
            _edgeChoice.Items.Add(new EdgeChoice(PanelDockEdge.Right, AppText.Get("Edge.Right")));
            SelectEdgeChoice(_candidate.DockEdge);
            dockEdgeSurface.Controls.Add(_edgeChoice);

            Panel shortcutsPage = CreatePage(contentHost, SettingsPage.Shortcuts);
            AddPageHeader(shortcutsPage, AppText.Get("Settings.KeyboardShortcuts"), AppText.Get("Settings.Intro"));
            RoundedPanel shortcutSurface = CreateSurface(32, 108, 628, 334);
            shortcutsPage.Controls.Add(shortcutSurface);
            Label actionHeader = CreateLabel(AppText.Get("Settings.ActionColumn"), 8F, FontStyle.Bold, AppColors.MutedText);
            actionHeader.SetBounds(18, 9, 290, 25);
            shortcutSurface.Controls.Add(actionHeader);
            Label shortcutHeader = CreateLabel(AppText.Get("Settings.ShortcutColumn"), 8F, FontStyle.Bold, AppColors.MutedText);
            shortcutHeader.SetBounds(352, 9, 256, 25);
            shortcutSurface.Controls.Add(shortcutHeader);
            AddDivider(shortcutSurface, 40, 0, 628);
            AddHotkeyRow(shortcutSurface, HotkeyAction.VolumeUp, AppText.Get("Action.IncreaseVolume"), 40);
            AddHotkeyRow(shortcutSurface, HotkeyAction.VolumeDown, AppText.Get("Action.DecreaseVolume"), 88);
            AddHotkeyRow(shortcutSurface, HotkeyAction.BrightnessUp, AppText.Get("Action.IncreaseBrightness"), 136);
            AddHotkeyRow(shortcutSurface, HotkeyAction.BrightnessDown, AppText.Get("Action.DecreaseBrightness"), 184);
            AddHotkeyRow(shortcutSurface, HotkeyAction.ToggleMute, AppText.Get("Action.ToggleMute"), 232);
            AddHotkeyRow(shortcutSurface, HotkeyAction.TogglePanel, AppText.Get("Action.TogglePanel"), 280);

            Panel generalPage = CreatePage(contentHost, SettingsPage.General);
            AddPageHeader(generalPage, AppText.Get("Settings.General"), AppText.Get("Settings.GeneralIntro"));
            RoundedPanel generalSurface = CreateSurface(32, 108, 628, 324);
            generalPage.Controls.Add(generalSurface);
            Label stepLabel = CreateLabel(AppText.Get("Settings.ChangeAmount"), 10F, FontStyle.Bold, AppColors.Text);
            stepLabel.SetBounds(18, 18, 390, 30);
            generalSurface.Controls.Add(stepLabel);
            _stepChoice = new ModernChoiceBox();
            _stepChoice.SetBounds(456, 13, 152, 40);
            _stepChoice.AccessibleName = AppText.Get("Settings.ChangeAmount");
            _stepChoice.Items.Add("2%");
            _stepChoice.Items.Add("5%");
            _stepChoice.Items.Add("10%");
            SelectStepChoice(_candidate.StepPercent);
            generalSurface.Controls.Add(_stepChoice);
            AddDivider(generalSurface, 66, 0, 628);

            _startupToggle = CreateToggle(AppText.Get("Settings.StartWithWindows"), 18, 72, _candidate.StartWithWindows);
            _alwaysOnTopToggle = CreateToggle(AppText.Get("Settings.AlwaysOnTop"), 18, 120, _candidate.AlwaysOnTop);
            _autoCollapseToggle = CreateToggle(AppText.Get("Settings.AutoCollapse"), 18, 168, _candidate.AutoCollapse);
            generalSurface.Controls.Add(_startupToggle);
            generalSurface.Controls.Add(_alwaysOnTopToggle);
            generalSurface.Controls.Add(_autoCollapseToggle);
            AddDivider(generalSurface, 218, 0, 628);
            ModernButton resetPositionButton = CreateSecondaryButton(AppText.Get("Settings.ResetPanelPosition"), 18, 242, 260);
            resetPositionButton.AccessibleName = AppText.Get("Settings.ResetPanelPosition");
            resetPositionButton.Click += ResetPanelPosition;
            generalSurface.Controls.Add(resetPositionButton);

            Panel footer = new Panel();
            footer.SetBounds(0, LogicalHeight - FooterHeight, LogicalWidth, FooterHeight);
            footer.BackColor = AppColors.Card;
            root.Controls.Add(footer);
            AddDivider(footer, 0, 0, LogicalWidth);
            _statusLabel = CreateLabel(string.Empty, 8.8F, FontStyle.Regular, AppColors.Danger);
            _statusLabel.SetBounds(224, 15, 410, 42);
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(_statusLabel);
            ModernButton defaultsButton = CreateSecondaryButton(AppText.Get("Settings.RestoreDefaults"), 24, 16, 168);
            defaultsButton.Click += RestoreDefaults;
            footer.Controls.Add(defaultsButton);
            ModernButton cancelButton = CreateSecondaryButton(AppText.Get("Common.Cancel"), 648, 16, 100);
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancelButton);
            ModernButton saveButton = CreatePrimaryButton(AppText.Get("Settings.SaveChanges"), 760, 16, 116);
            saveButton.Click += SaveSettings;
            footer.Controls.Add(saveButton);
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            root.Controls.SetChildIndex(titleBar, 0);
            root.Controls.SetChildIndex(navigation, 1);
            root.Controls.SetChildIndex(footer, 2);

            LoadBindings();
            SelectLayout(_selectedLayout);
            SelectPage(SettingsPage.Interface);
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

        public void SelectPreviewPage(string pageName)
        {
            if (string.Equals(pageName, "Shortcuts", StringComparison.OrdinalIgnoreCase))
                SelectPage(SettingsPage.Shortcuts);
            else if (string.Equals(pageName, "General", StringComparison.OrdinalIgnoreCase))
                SelectPage(SettingsPage.General);
            else
                SelectPage(SettingsPage.Interface);
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            if (!_fitApplied)
            {
                float widthScale = (area.Width - 24F) / Math.Max(1, Width);
                float heightScale = (area.Height - 24F) / Math.Max(1, Height);
                float fitScale = Math.Min(1F, Math.Min(widthScale, heightScale));
                if (fitScale < 0.99F)
                {
                    SuspendLayout();
                    Scale(new SizeF(fitScale, fitScale));
                    ResumeLayout(true);
                }
                _fitApplied = true;
            }
            Location = new Point(
                area.Left + Math.Max(0, (area.Width - Width) / 2),
                area.Top + Math.Max(0, (area.Height - Height) / 2));
        }

        private Panel BuildTitleBar(Control root)
        {
            Panel titleBar = new Panel();
            titleBar.SetBounds(0, 0, LogicalWidth, TitleBarHeight);
            titleBar.BackColor = AppColors.Card;
            root.Controls.Add(titleBar);
            LogoControl logo = new LogoControl();
            logo.SetBounds(16, 10, 32, 32);
            titleBar.Controls.Add(logo);
            Label brand = CreateLabel(AppText.Get("App.Name"), 10F, FontStyle.Bold, AppColors.Text);
            brand.SetBounds(60, 10, 170, 32);
            brand.TextAlign = ContentAlignment.MiddleLeft;
            titleBar.Controls.Add(brand);
            Label divider = CreateLabel("/", 10F, FontStyle.Regular, AppColors.Border);
            divider.SetBounds(226, 10, 20, 32);
            divider.TextAlign = ContentAlignment.MiddleCenter;
            titleBar.Controls.Add(divider);
            Label pageName = CreateLabel(AppText.Get("Settings.Title"), 9.5F, FontStyle.Regular, AppColors.MutedText);
            pageName.SetBounds(248, 10, 220, 32);
            pageName.TextAlign = ContentAlignment.MiddleLeft;
            titleBar.Controls.Add(pageName);
            ModernButton closeButton = new ModernButton();
            closeButton.Text = "×";
            closeButton.AccessibleName = AppText.Get("Common.Close");
            closeButton.Font = AppText.CreateFont(12F, FontStyle.Bold);
            closeButton.SetBounds(852, 8, 36, 36);
            closeButton.CornerRadius = 3;
            closeButton.BorderThickness = 0;
            closeButton.FillColor = AppColors.Card;
            closeButton.HoverColor = AppColors.IsLight
                ? Color.FromArgb(254, 228, 226)
                : Color.FromArgb(80, 24, 30);
            closeButton.PressedColor = AppColors.IsLight
                ? Color.FromArgb(254, 205, 202)
                : Color.FromArgb(110, 28, 36);
            closeButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            titleBar.Controls.Add(closeButton);
            AddDivider(titleBar, TitleBarHeight - 1, 0, LogicalWidth);
            WireTitleBarDrag(titleBar);
            WireTitleBarDrag(logo);
            WireTitleBarDrag(brand);
            WireTitleBarDrag(divider);
            WireTitleBarDrag(pageName);
            return titleBar;
        }

        private Panel BuildNavigation(Control root)
        {
            Panel navigation = new Panel();
            navigation.SetBounds(0, TitleBarHeight, SidebarWidth,
                LogicalHeight - TitleBarHeight - FooterHeight);
            navigation.BackColor = AppColors.Sidebar;
            root.Controls.Add(navigation);
            Label sectionLabel = CreateLabel(AppText.Get("Settings.Title").ToUpperInvariant(),
                7.8F, FontStyle.Bold, AppColors.SidebarMuted);
            sectionLabel.SetBounds(20, 18, 168, 24);
            navigation.Controls.Add(sectionLabel);
            _selectionRail = new Panel();
            _selectionRail.BackColor = AppColors.Accent;
            _selectionRail.SetBounds(0, 52, 3, 44);
            navigation.Controls.Add(_selectionRail);
            AddNavigationButton(navigation, SettingsPage.Interface, AppText.Get("Settings.Interface"), 52);
            AddNavigationButton(navigation, SettingsPage.Shortcuts, AppText.Get("Settings.KeyboardShortcuts"), 104);
            AddNavigationButton(navigation, SettingsPage.General, AppText.Get("Settings.General"), 156);
            Label localNote = CreateLabel("QUICK CONTROLS", 7.2F, FontStyle.Bold, AppColors.SidebarMuted);
            localNote.SetBounds(20, navigation.Height - 42, 168, 24);
            navigation.Controls.Add(localNote);
            return navigation;
        }

        private void AddNavigationButton(Control parent, SettingsPage page, string text, int y)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(16, 0, 12, 0);
            button.Font = AppText.CreateFont(9F, FontStyle.Regular);
            button.ForeColor = AppColors.SidebarMuted;
            button.FillColor = AppColors.Sidebar;
            button.HoverColor = AppColors.SidebarHover;
            button.PressedColor = AppColors.SidebarSelected;
            button.BorderThickness = 0;
            button.CornerRadius = 3;
            button.SetBounds(12, y, 184, 44);
            button.Click += delegate { SelectPage(page); };
            parent.Controls.Add(button);
            _navButtons[page] = button;
        }

        private Panel CreatePage(Control host, SettingsPage page)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = AppColors.Window;
            panel.Visible = false;
            host.Controls.Add(panel);
            _pages[page] = panel;
            return panel;
        }

        private static void AddPageHeader(Control page, string titleText, string helpText)
        {
            Label title = CreateLabel(titleText, 20F, FontStyle.Bold, AppColors.Text);
            title.SetBounds(32, 22, 628, 38);
            page.Controls.Add(title);
            Label help = CreateLabel(helpText, 9F, FontStyle.Regular, AppColors.MutedText);
            help.SetBounds(32, 64, 628, 28);
            page.Controls.Add(help);
        }

        private void SelectPage(SettingsPage page)
        {
            foreach (KeyValuePair<SettingsPage, Panel> item in _pages)
                item.Value.Visible = item.Key == page;
            foreach (KeyValuePair<SettingsPage, ModernButton> item in _navButtons)
            {
                bool selected = item.Key == page;
                item.Value.FillColor = selected ? AppColors.SidebarSelected : AppColors.Sidebar;
                item.Value.ForeColor = selected ? AppColors.SidebarText : AppColors.SidebarMuted;
                item.Value.Invalidate();
            }
            ModernButton selectedButton = _navButtons[page];
            _selectionRail.Top = selectedButton.Top;
            _selectionRail.BringToFront();
            _pages[page].BringToFront();
        }

        private void AddLayoutButton(Control parent, PanelLayoutMode mode, string caption, int x, int y)
        {
            LayoutOptionButton button = new LayoutOptionButton();
            button.Mode = mode;
            button.Text = caption;
            button.AccessibleName = caption;
            button.SetBounds(x, y, 304, 72);
            button.Click += delegate { SelectLayout(mode); };
            parent.Controls.Add(button);
            _layoutButtons[mode] = button;
        }

        private void SelectLayout(PanelLayoutMode mode)
        {
            if (!Enum.IsDefined(typeof(PanelLayoutMode), mode)) mode = PanelLayoutMode.Full;
            _selectedLayout = mode;
            foreach (KeyValuePair<PanelLayoutMode, LayoutOptionButton> pair in _layoutButtons)
                pair.Value.Selected = pair.Key == mode;
            if (_dockEdgeSurface != null) _dockEdgeSurface.Visible = mode == PanelLayoutMode.EdgeDock;
        }

        private void AddHotkeyRow(Control parent, HotkeyAction action, string caption, int y)
        {
            Panel accent = new Panel();
            accent.BackColor = action == HotkeyAction.BrightnessUp || action == HotkeyAction.BrightnessDown
                ? AppColors.Accent2 : AppColors.Accent;
            accent.SetBounds(18, y + 16, 3, 16);
            parent.Controls.Add(accent);
            Label label = CreateLabel(caption, 9.2F, FontStyle.Regular, AppColors.Text);
            label.SetBounds(30, y + 9, 300, 30);
            label.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(label);
            HotkeyTextBox input = new HotkeyTextBox();
            input.SetBounds(352, y + 6, 256, 36);
            input.AccessibleName = AppText.Format("Settings.ShortcutFor", caption);
            input.InvalidCombination += delegate
            {
                _statusLabel.ForeColor = AppColors.Danger;
                _statusLabel.Text = AppText.Get("Settings.ShortcutModifierRequired");
            };
            parent.Controls.Add(input);
            _hotkeyInputs[action] = input;
            if (y < 280) AddDivider(parent, y + 47, 18, 590);
        }

        private void LoadBindings()
        {
            foreach (KeyValuePair<HotkeyAction, HotkeyTextBox> pair in _hotkeyInputs)
                pair.Value.Binding = _candidate.GetHotkey(pair.Key);
        }

        private void RestoreDefaults(object sender, EventArgs eventArgs)
        {
            AppSettings defaults = AppSettings.CreateDefaults();
            foreach (KeyValuePair<HotkeyAction, HotkeyTextBox> pair in _hotkeyInputs)
                pair.Value.Binding = defaults.GetHotkey(pair.Key);
            SelectStepChoice(defaults.StepPercent);
            _startupToggle.Checked = defaults.StartWithWindows;
            _alwaysOnTopToggle.Checked = defaults.AlwaysOnTop;
            _autoCollapseToggle.Checked = defaults.AutoCollapse;
            SelectLanguageChoice(defaults.LanguageCode);
            SelectLayout(defaults.PanelLayoutMode);
            SelectEdgeChoice(defaults.DockEdge);
            _candidate.PanelLeft = defaults.PanelLeft;
            _candidate.PanelTop = defaults.PanelTop;
            _candidate.SelectedDisplayId = defaults.SelectedDisplayId;
            _statusLabel.ForeColor = AppColors.MutedText;
            _statusLabel.Text = AppText.Get("Settings.DefaultsRestored");
        }

        private void ResetPanelPosition(object sender, EventArgs eventArgs)
        {
            _candidate.PanelLeft = -1D;
            _candidate.PanelTop = -1D;
            _statusLabel.ForeColor = AppColors.MutedText;
            _statusLabel.Text = AppText.Get("Settings.PanelPositionWillReset");
        }

        private void SaveSettings(object sender, EventArgs eventArgs)
        {
            _statusLabel.ForeColor = AppColors.Danger;
            foreach (KeyValuePair<HotkeyAction, HotkeyTextBox> pair in _hotkeyInputs)
                _candidate.SetHotkey(pair.Key, pair.Value.Binding);
            string duplicate = FindDuplicateBinding();
            if (!string.IsNullOrEmpty(duplicate))
            {
                _statusLabel.Text = duplicate;
                SelectPage(SettingsPage.Shortcuts);
                return;
            }

            int step;
            string stepText = Convert.ToString(_stepChoice.SelectedItem).Replace("%", string.Empty);
            if (!int.TryParse(stepText, out step)) step = 5;
            _candidate.StepPercent = step;
            _candidate.StartWithWindows = _startupToggle.Checked;
            _candidate.AlwaysOnTop = _alwaysOnTopToggle.Checked;
            _candidate.AutoCollapse = _autoCollapseToggle.Checked;
            LanguageChoice language = _languageChoice.SelectedItem as LanguageChoice;
            EdgeChoice edge = _edgeChoice.SelectedItem as EdgeChoice;
            if (language != null) _candidate.LanguageCode = language.Code;
            if (edge != null) _candidate.DockEdge = edge.Value;
            _candidate.PanelLayoutMode = _selectedLayout;
            _candidate.PanelCompact = _selectedLayout == PanelLayoutMode.HorizontalMini;
            _candidate.SettingsVersion = AppSettings.CurrentSettingsVersion;

            string error = _applySettings == null ? null : _applySettings(_candidate.Clone());
            if (!string.IsNullOrEmpty(error))
            {
                _statusLabel.Text = error;
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private string FindDuplicateBinding()
        {
            List<HotkeyBinding> seen = new List<HotkeyBinding>();
            foreach (HotkeyTextBox input in _hotkeyInputs.Values)
            {
                HotkeyBinding binding = input.Binding;
                if (!binding.IsValid()) return AppText.Get("Settings.ShortcutInvalid");
                for (int index = 0; index < seen.Count; index++)
                    if (seen[index].SameAs(binding)) return AppText.Get("Settings.ShortcutDuplicate");
                seen.Add(binding);
            }
            return null;
        }

        private void SelectLanguageChoice(string code)
        {
            string normalized = AppText.NormalizeLanguageCode(code);
            for (int index = 0; index < _languageChoice.Items.Count; index++)
            {
                LanguageChoice choice = _languageChoice.Items[index] as LanguageChoice;
                if (choice != null && string.Equals(choice.Code, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    _languageChoice.SelectedIndex = index;
                    return;
                }
            }
            if (_languageChoice.Items.Count > 0) _languageChoice.SelectedIndex = 0;
        }

        private void SelectEdgeChoice(PanelDockEdge value)
        {
            for (int index = 0; index < _edgeChoice.Items.Count; index++)
            {
                EdgeChoice choice = _edgeChoice.Items[index] as EdgeChoice;
                if (choice != null && choice.Value == value)
                {
                    _edgeChoice.SelectedIndex = index;
                    return;
                }
            }
            if (_edgeChoice.Items.Count > 0) _edgeChoice.SelectedIndex = 0;
        }

        private void SelectStepChoice(int value)
        {
            string text = value + "%";
            for (int index = 0; index < _stepChoice.Items.Count; index++)
            {
                if (string.Equals(Convert.ToString(_stepChoice.Items[index]), text, StringComparison.Ordinal))
                {
                    _stepChoice.SelectedIndex = index;
                    return;
                }
            }
            _stepChoice.SelectedIndex = 1;
        }

        private static RoundedPanel CreateSurface(int x, int y, int width, int height)
        {
            RoundedPanel panel = new RoundedPanel();
            panel.SetBounds(x, y, width, height);
            panel.CornerRadius = 3;
            panel.FillColor = AppColors.Card;
            panel.BorderColor = AppColors.Divider;
            return panel;
        }

        private static ModernToggle CreateToggle(string text, int x, int y, bool value)
        {
            ModernToggle toggle = new ModernToggle();
            toggle.Text = text;
            toggle.Checked = value;
            toggle.AccessibleName = text;
            toggle.SetBounds(x, y, 590, 42);
            return toggle;
        }

        private static ModernButton CreateSecondaryButton(string text, int x, int y, int width)
        {
            ModernButton button = new ModernButton();
            button.Text = text;
            button.Font = AppText.CreateFont(9F, FontStyle.Regular);
            button.CornerRadius = 3;
            button.BorderColor = AppColors.StrongBorder;
            button.SetBounds(x, y, width, 40);
            return button;
        }

        private static ModernButton CreatePrimaryButton(string text, int x, int y, int width)
        {
            ModernButton button = CreateSecondaryButton(text, x, y, width);
            button.FillColor = AppColors.Accent;
            button.BorderColor = AppColors.AccentPressed;
            button.ForeColor = Color.White;
            return button;
        }

        private static void AddDivider(Control parent, int y, int x, int width)
        {
            Panel line = new Panel();
            line.BackColor = AppColors.Divider;
            line.SetBounds(x, y, width, 1);
            parent.Controls.Add(line);
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

        private void SettingsKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode != Keys.Escape) return;
            if (ActiveControl is HotkeyTextBox) return;
            DialogResult = DialogResult.Cancel;
            Close();
            eventArgs.Handled = true;
        }

        private void WireTitleBarDrag(Control control)
        {
            control.MouseDown += delegate(object sender, MouseEventArgs eventArgs)
            {
                if (eventArgs.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            };
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wordParameter, IntPtr longParameter);

        private sealed class LanguageChoice
        {
            public LanguageChoice(AppLanguageOption option) { Code = option.Code; Text = option.NativeName; }
            public string Code { get; private set; }
            public string Text { get; private set; }
            public override string ToString() { return Text; }
        }

        private sealed class EdgeChoice
        {
            public EdgeChoice(PanelDockEdge value, string text) { Value = value; Text = text; }
            public PanelDockEdge Value { get; private set; }
            public string Text { get; private set; }
            public override string ToString() { return Text; }
        }
    }

    internal sealed class LayoutOptionButton : Control
    {
        private bool _selected;
        private bool _hovered;

        public LayoutOptionButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = AppColors.Text;
            Font = AppText.CreateFont(9.2F, FontStyle.Bold);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public PanelLayoutMode Mode { get; set; }
        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new LayoutAccessibleObject(this);
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
            base.OnMouseDown(eventArgs);
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Space || eventArgs.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
                return;
            }
            if (eventArgs.KeyCode == Keys.Left || eventArgs.KeyCode == Keys.Right ||
                eventArgs.KeyCode == Keys.Up || eventArgs.KeyCode == Keys.Down)
            {
                MoveSelection(eventArgs.KeyCode);
                eventArgs.Handled = true;
                return;
            }
            base.OnKeyDown(eventArgs);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down) return true;
            return base.IsInputKey(keyData);
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
            float stroke = Selected || Focused ? Math.Max(2F, 2F * scale) : Math.Max(1F, scale);
            RectangleF bounds = new RectangleF(stroke / 2F, stroke / 2F,
                Math.Max(1F, Width - stroke), Math.Max(1F, Height - stroke));
            Color fill = Selected ? AppColors.AccentSoft : (_hovered ? AppColors.CardHover : AppColors.Card);
            Color border = Selected ? AppColors.Accent : (Focused ? AppColors.Focus : AppColors.Border);
            using (GraphicsPath path = UiHelpers.RoundedRectangle(bounds, 3F * scale))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, stroke))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(pen, path);
            }

            Rectangle schematicBounds = new Rectangle(
                (int)Math.Round(16F * scale), (int)Math.Round(14F * scale),
                (int)Math.Round(64F * scale), (int)Math.Round(44F * scale));
            DrawSchematic(eventArgs.Graphics, schematicBounds, scale);
            TextRenderer.DrawText(eventArgs.Graphics, Text, Font,
                new Rectangle((int)Math.Round(94F * scale), (int)Math.Round(2F * scale),
                    Math.Max(1, Width - (int)Math.Round(128F * scale)),
                    Math.Max(1, Height - (int)Math.Round(4F * scale))),
                Selected ? AppColors.Accent : AppColors.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);
            if (Selected)
            {
                int centerX = Width - (int)Math.Round(20F * scale);
                int centerY = (int)Math.Round(18F * scale);
                int circleRadius = Math.Max(6, (int)Math.Round(8F * scale));
                using (SolidBrush circle = new SolidBrush(AppColors.Accent))
                    eventArgs.Graphics.FillEllipse(circle, centerX - circleRadius, centerY - circleRadius,
                        circleRadius * 2, circleRadius * 2);
                using (Pen check = new Pen(Color.White, Math.Max(1.8F, 1.8F * scale)))
                {
                    check.StartCap = LineCap.Round;
                    check.EndCap = LineCap.Round;
                    eventArgs.Graphics.DrawLine(check,
                        centerX - 4F * scale, centerY,
                        centerX - 1F * scale, centerY + 3F * scale);
                    eventArgs.Graphics.DrawLine(check,
                        centerX - 1F * scale, centerY + 3F * scale,
                        centerX + 5F * scale, centerY - 4F * scale);
                }
            }
        }

        private void DrawSchematic(Graphics graphics, Rectangle bounds, float scale)
        {
            using (SolidBrush background = new SolidBrush(AppColors.Window))
            using (Pen border = new Pen(AppColors.StrongBorder, Math.Max(1F, scale)))
            {
                graphics.FillRectangle(background, bounds);
                graphics.DrawRectangle(border, bounds);
            }

            Rectangle shape;
            switch (Mode)
            {
                case PanelLayoutMode.HorizontalMini:
                    shape = new Rectangle(
                        bounds.Left + (int)Math.Round(6F * scale),
                        bounds.Top + (int)Math.Round(15F * scale),
                        bounds.Width - (int)Math.Round(12F * scale),
                        (int)Math.Round(14F * scale));
                    break;
                case PanelLayoutMode.VerticalMini:
                    shape = new Rectangle(
                        bounds.Left + (int)Math.Round(22F * scale),
                        bounds.Top + (int)Math.Round(5F * scale),
                        (int)Math.Round(20F * scale),
                        bounds.Height - (int)Math.Round(10F * scale));
                    break;
                case PanelLayoutMode.EdgeDock:
                    shape = new Rectangle(
                        bounds.Right - (int)Math.Round(13F * scale),
                        bounds.Top + (int)Math.Round(5F * scale),
                        (int)Math.Round(7F * scale),
                        bounds.Height - (int)Math.Round(10F * scale));
                    break;
                default:
                    shape = new Rectangle(
                        bounds.Left + (int)Math.Round(9F * scale),
                        bounds.Top + (int)Math.Round(5F * scale),
                        bounds.Width - (int)Math.Round(18F * scale),
                        bounds.Height - (int)Math.Round(10F * scale));
                    break;
            }
            using (SolidBrush surface = new SolidBrush(AppColors.Card))
            using (Pen outline = new Pen(AppColors.Border, Math.Max(1F, scale)))
            {
                graphics.FillRectangle(surface, shape);
                graphics.DrawRectangle(outline, shape);
            }
            using (SolidBrush blue = new SolidBrush(AppColors.Accent))
                graphics.FillRectangle(blue, shape.Left, shape.Top,
                    Math.Max((int)Math.Round(3F * scale), shape.Width / 2),
                    Math.Max(2, (int)Math.Round(3F * scale)));
            using (SolidBrush orange = new SolidBrush(AppColors.Accent2))
            {
                int barHeight = Math.Max(2, (int)Math.Round(3F * scale));
                graphics.FillRectangle(orange, shape.Left, shape.Bottom - barHeight,
                    Math.Max(barHeight, shape.Width * 2 / 3), barHeight);
            }
        }

        private void MoveSelection(Keys key)
        {
            if (Parent == null) return;
            List<LayoutOptionButton> buttons = new List<LayoutOptionButton>();
            foreach (Control control in Parent.Controls)
            {
                LayoutOptionButton option = control as LayoutOptionButton;
                if (option != null) buttons.Add(option);
            }
            buttons.Sort(delegate(LayoutOptionButton first, LayoutOptionButton second)
            {
                int topComparison = first.Top.CompareTo(second.Top);
                return topComparison != 0 ? topComparison : first.Left.CompareTo(second.Left);
            });
            int index = buttons.IndexOf(this);
            if (index < 0) return;
            int next = index;
            if (key == Keys.Left) next = Math.Max(0, index - 1);
            else if (key == Keys.Right) next = Math.Min(buttons.Count - 1, index + 1);
            else if (key == Keys.Up) next = Math.Max(0, index - 2);
            else if (key == Keys.Down) next = Math.Min(buttons.Count - 1, index + 2);
            LayoutOptionButton target = buttons[next];
            target.Focus();
            target.OnClick(EventArgs.Empty);
        }

        private sealed class LayoutAccessibleObject : ControlAccessibleObject
        {
            private readonly LayoutOptionButton _owner;
            public LayoutAccessibleObject(LayoutOptionButton owner) : base(owner) { _owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.RadioButton; } }
            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates states = base.State;
                    if (_owner.Selected) states |= AccessibleStates.Checked;
                    return states;
                }
            }
            public override void DoDefaultAction() { _owner.OnClick(EventArgs.Empty); }
        }
    }
}
