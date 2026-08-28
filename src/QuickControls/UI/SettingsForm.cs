using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using QuickControls.Models;

namespace QuickControls.UI
{
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings _candidate;
        private readonly Func<AppSettings, string> _applySettings;
        private readonly Dictionary<HotkeyAction, HotkeyTextBox> _hotkeyInputs;
        private readonly ComboBox _stepCombo;
        private readonly CheckBox _startupCheck;
        private readonly CheckBox _alwaysOnTopCheck;
        private readonly CheckBox _autoCollapseCheck;
        private readonly Label _statusLabel;

        public SettingsForm(AppSettings settings, Func<AppSettings, string> applySettings)
        {
            _candidate = settings.Clone();
            _applySettings = applySettings;
            _hotkeyInputs = new Dictionary<HotkeyAction, HotkeyTextBox>();

            Text = "Settings — Quick Controls";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(620, 650);
            BackColor = AppColors.Window;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Panel scroll = new Panel();
            scroll.SetBounds(0, 0, 620, 578);
            scroll.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            scroll.AutoScroll = true;
            Controls.Add(scroll);

            Panel footer = new Panel();
            footer.SetBounds(0, 578, 620, 72);
            footer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            footer.BackColor = AppColors.Window;
            Controls.Add(footer);

            Label title = CreateLabel("Settings", 24F, FontStyle.Bold, AppColors.Text);
            title.SetBounds(24, 18, 572, 36);
            scroll.Controls.Add(title);

            Label intro = CreateLabel("Choose a shortcut field, then press the key combination you want.", 10F, FontStyle.Regular, AppColors.MutedText);
            intro.SetBounds(24, 54, 572, 24);
            scroll.Controls.Add(intro);

            Label hotkeyHeading = CreateLabel("Keyboard shortcuts", 11F, FontStyle.Bold, AppColors.Text);
            hotkeyHeading.SetBounds(24, 88, 572, 24);
            scroll.Controls.Add(hotkeyHeading);

            RoundedPanel hotkeyCard = new RoundedPanel();
            hotkeyCard.SetBounds(24, 116, 572, 246);
            hotkeyCard.CornerRadius = 14;
            scroll.Controls.Add(hotkeyCard);

            AddHotkeyRow(hotkeyCard, HotkeyAction.VolumeUp, "Increase volume", 12);
            AddHotkeyRow(hotkeyCard, HotkeyAction.VolumeDown, "Decrease volume", 50);
            AddHotkeyRow(hotkeyCard, HotkeyAction.BrightnessUp, "Increase brightness", 88);
            AddHotkeyRow(hotkeyCard, HotkeyAction.BrightnessDown, "Decrease brightness", 126);
            AddHotkeyRow(hotkeyCard, HotkeyAction.ToggleMute, "Mute or unmute", 164);
            AddHotkeyRow(hotkeyCard, HotkeyAction.TogglePanel, "Show or hide panel", 202);

            Label generalHeading = CreateLabel("General", 11F, FontStyle.Bold, AppColors.Text);
            generalHeading.SetBounds(24, 380, 572, 24);
            scroll.Controls.Add(generalHeading);

            RoundedPanel behaviorCard = new RoundedPanel();
            behaviorCard.SetBounds(24, 408, 572, 150);
            behaviorCard.CornerRadius = 14;
            scroll.Controls.Add(behaviorCard);

            Label stepLabel = CreateLabel("Change amount", 10F, FontStyle.Regular, AppColors.Text);
            stepLabel.SetBounds(16, 14, 280, 25);
            behaviorCard.Controls.Add(stepLabel);

            _stepCombo = new ComboBox();
            _stepCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _stepCombo.FlatStyle = FlatStyle.Flat;
            _stepCombo.BackColor = AppColors.CardHover;
            _stepCombo.ForeColor = AppColors.Text;
            _stepCombo.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _stepCombo.Items.AddRange(new object[] { "2%", "5%", "10%" });
            _stepCombo.SetBounds(400, 11, 156, 31);
            _stepCombo.SelectedItem = _candidate.StepPercent + "%";
            behaviorCard.Controls.Add(_stepCombo);

            _startupCheck = CreateCheckBox("Open Quick Controls when I sign in to Windows", 16, 52, _candidate.StartWithWindows);
            _alwaysOnTopCheck = CreateCheckBox("Keep this panel above other windows", 16, 82, _candidate.AlwaysOnTop);
            _autoCollapseCheck = CreateCheckBox("Switch to compact view when not in use", 16, 112, _candidate.AutoCollapse);
            behaviorCard.Controls.Add(_startupCheck);
            behaviorCard.Controls.Add(_alwaysOnTopCheck);
            behaviorCard.Controls.Add(_autoCollapseCheck);

            _statusLabel = CreateLabel(string.Empty, 9.5F, FontStyle.Regular, AppColors.Danger);
            _statusLabel.SetBounds(24, 2, 572, 22);
            footer.Controls.Add(_statusLabel);

            ModernButton defaultsButton = new ModernButton();
            defaultsButton.Text = "Restore defaults";
            defaultsButton.SetBounds(24, 26, 160, 40);
            defaultsButton.Click += RestoreDefaults;
            footer.Controls.Add(defaultsButton);

            ModernButton cancelButton = new ModernButton();
            cancelButton.Text = "Cancel";
            cancelButton.SetBounds(404, 26, 80, 40);
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancelButton);

            ModernButton saveButton = new ModernButton();
            saveButton.Text = "Save changes";
            saveButton.FillColor = AppColors.Accent;
            saveButton.ForeColor = Color.White;
            saveButton.HoverColor = Color.FromArgb(145, 120, 250);
            saveButton.PressedColor = Color.FromArgb(92, 70, 210);
            saveButton.SetBounds(492, 26, 104, 40);
            saveButton.Click += SaveSettings;
            footer.Controls.Add(saveButton);
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            LoadBindings();
        }

        private void AddHotkeyRow(Control parent, HotkeyAction action, string caption, int y)
        {
            Label label = CreateLabel(caption, 10F, FontStyle.Regular, AppColors.Text);
            label.SetBounds(16, y + 3, 258, 28);
            parent.Controls.Add(label);

            HotkeyTextBox input = new HotkeyTextBox();
            input.SetBounds(294, y, 262, 30);
            input.AccessibleName = "Shortcut for " + caption;
            input.InvalidCombination += delegate { _statusLabel.Text = "Each shortcut must include Ctrl, Alt, Shift, or the Windows key."; };
            parent.Controls.Add(input);
            _hotkeyInputs[action] = input;
        }

        private void LoadBindings()
        {
            foreach (KeyValuePair<HotkeyAction, HotkeyTextBox> pair in _hotkeyInputs)
            {
                pair.Value.Binding = _candidate.GetHotkey(pair.Key);
            }
        }

        private void RestoreDefaults(object sender, EventArgs eventArgs)
        {
            AppSettings defaults = AppSettings.CreateDefaults();
            foreach (KeyValuePair<HotkeyAction, HotkeyTextBox> pair in _hotkeyInputs)
            {
                pair.Value.Binding = defaults.GetHotkey(pair.Key);
            }
            _stepCombo.SelectedItem = "5%";
            _startupCheck.Checked = defaults.StartWithWindows;
            _alwaysOnTopCheck.Checked = defaults.AlwaysOnTop;
            _autoCollapseCheck.Checked = defaults.AutoCollapse;
            _statusLabel.ForeColor = AppColors.MutedText;
            _statusLabel.Text = "Default settings restored. Click Save changes to apply them.";
        }

        private void SaveSettings(object sender, EventArgs eventArgs)
        {
            _statusLabel.ForeColor = AppColors.Danger;
            foreach (KeyValuePair<HotkeyAction, HotkeyTextBox> pair in _hotkeyInputs)
            {
                _candidate.SetHotkey(pair.Key, pair.Value.Binding);
            }

            string duplicate = FindDuplicateBinding();
            if (!string.IsNullOrEmpty(duplicate))
            {
                _statusLabel.Text = duplicate;
                return;
            }

            string stepText = Convert.ToString(_stepCombo.SelectedItem).Replace("%", string.Empty);
            int step;
            if (!int.TryParse(stepText, out step)) step = 5;
            _candidate.StepPercent = step;
            _candidate.StartWithWindows = _startupCheck.Checked;
            _candidate.AlwaysOnTop = _alwaysOnTopCheck.Checked;
            _candidate.AutoCollapse = _autoCollapseCheck.Checked;

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
                if (!binding.IsValid()) return "One shortcut is invalid.";
                for (int index = 0; index < seen.Count; index++)
                {
                    if (seen[index].SameAs(binding)) return "Two actions use the same shortcut.";
                }
                seen.Add(binding);
            }
            return null;
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int targetWidth = Math.Min(Width, Math.Max(520, workingArea.Width - 32));
            int targetHeight = Math.Min(Height, Math.Max(520, workingArea.Height - 32));
            Size = new Size(targetWidth, targetHeight);
            Location = new Point(
                workingArea.Left + (workingArea.Width - Width) / 2,
                workingArea.Top + (workingArea.Height - Height) / 2);
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

        private static CheckBox CreateCheckBox(string text, int x, int y, bool value)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.Checked = value;
            checkBox.ForeColor = AppColors.Text;
            checkBox.BackColor = Color.Transparent;
            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.AutoSize = true;
            checkBox.Location = new Point(x, y);
            return checkBox;
        }
    }
}
