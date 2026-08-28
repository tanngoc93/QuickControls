using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QuickControls.Installer
{
    internal sealed class UninstallerForm : Form
    {
        private const int WsExNoActivate = 0x08000000;

        private readonly Label statusLabel;
        private readonly Label detailLabel;
        private readonly ProgressBar progressBar;
        private readonly Button removeButton;
        private readonly Button cancelButtonControl;
        private readonly CheckBox removeSettingsCheck;
        private BackgroundWorker worker;
        private readonly bool previewMode;
        private bool removing;
        private bool removed;

        internal UninstallerForm() : this(false)
        {
        }

        internal UninstallerForm(bool previewMode)
        {
            this.previewMode = previewMode;
            SuspendLayout();

            Text = "Uninstall " + InstallEngine.ProductName;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 360);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            SizeGripStyle = SizeGripStyle.Hide;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            TableLayoutPanel rootLayout = new TableLayoutPanel();
            rootLayout.Name = "UninstallerRootLayout";
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Margin = Padding.Empty;
            rootLayout.Padding = Padding.Empty;
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 3;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            Controls.Add(rootLayout);

            Panel header = CreateHeader();
            rootLayout.Controls.Add(header, 0, 0);

            TableLayoutPanel contentLayout = new TableLayoutPanel();
            contentLayout.Name = "UninstallerContentLayout";
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.Margin = Padding.Empty;
            contentLayout.Padding = new Padding(30, 22, 30, 16);
            contentLayout.BackColor = Color.White;
            contentLayout.ColumnCount = 1;
            contentLayout.RowCount = 5;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(contentLayout, 0, 1);

            statusLabel = new Label();
            statusLabel.Name = "UninstallStatusLabel";
            statusLabel.AutoSize = true;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Margin = new Padding(0, 0, 0, 8);
            statusLabel.Text = "The app will be closed before it is uninstalled.";
            statusLabel.UseMnemonic = false;
            statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            statusLabel.ForeColor = Color.FromArgb(15, 23, 42);
            statusLabel.AccessibleName = "Uninstall status";
            contentLayout.Controls.Add(statusLabel, 0, 0);

            progressBar = new ProgressBar();
            progressBar.Name = "UninstallProgressBar";
            progressBar.Dock = DockStyle.Top;
            progressBar.Height = 10;
            progressBar.Margin = new Padding(0, 4, 0, 12);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Visible = false;
            progressBar.AccessibleName = "Uninstall progress";
            contentLayout.Controls.Add(progressBar, 0, 1);

            detailLabel = new Label();
            detailLabel.Name = "UninstallDetailLabel";
            detailLabel.AutoSize = true;
            detailLabel.Dock = DockStyle.Fill;
            detailLabel.Margin = Padding.Empty;
            detailLabel.MaximumSize = new Size(500, 0);
            detailLabel.Text = "You can reinstall at any time with the installer.";
            detailLabel.UseMnemonic = false;
            detailLabel.ForeColor = Color.FromArgb(71, 85, 105);
            detailLabel.TextAlign = ContentAlignment.TopLeft;
            detailLabel.AccessibleName = "Uninstall details";
            contentLayout.Controls.Add(detailLabel, 0, 2);

            removeSettingsCheck = new CheckBox();
            removeSettingsCheck.Name = "RemoveSettingsCheckBox";
            removeSettingsCheck.AutoSize = true;
            removeSettingsCheck.Dock = DockStyle.Left;
            removeSettingsCheck.Margin = new Padding(0, 8, 0, 0);
            removeSettingsCheck.Text = "Also delete saved shortcuts and settings";
            removeSettingsCheck.Checked = false;
            removeSettingsCheck.UseMnemonic = false;
            removeSettingsCheck.AccessibleName = "Also delete saved shortcuts and settings";
            contentLayout.Controls.Add(removeSettingsCheck, 0, 3);

            Panel footer = CreateFooter();
            rootLayout.Controls.Add(footer, 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Name = "UninstallerActions";
            actions.Dock = DockStyle.Fill;
            actions.Margin = Padding.Empty;
            actions.Padding = Padding.Empty;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            footer.Controls.Add(actions);

            removeButton = new Button();
            removeButton.Name = "UninstallButton";
            removeButton.Margin = Padding.Empty;
            removeButton.Size = new Size(124, 42);
            removeButton.Text = "Uninstall";
            removeButton.UseMnemonic = false;
            removeButton.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            removeButton.BackColor = Color.FromArgb(220, 38, 38);
            removeButton.ForeColor = Color.White;
            removeButton.FlatStyle = FlatStyle.Flat;
            removeButton.FlatAppearance.BorderSize = 0;
            removeButton.Cursor = Cursors.Hand;
            removeButton.AccessibleName = "Uninstall Quick Controls";
            if (!previewMode)
            {
                removeButton.Click += RemoveButtonClick;
            }
            actions.Controls.Add(removeButton);

            cancelButtonControl = new Button();
            cancelButtonControl.Name = "KeepAppButton";
            cancelButtonControl.Margin = new Padding(0, 0, 12, 0);
            cancelButtonControl.Size = new Size(108, 42);
            cancelButtonControl.Text = "Keep app";
            cancelButtonControl.UseMnemonic = false;
            cancelButtonControl.BackColor = Color.White;
            cancelButtonControl.ForeColor = Color.FromArgb(15, 23, 42);
            cancelButtonControl.FlatStyle = FlatStyle.Flat;
            cancelButtonControl.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            cancelButtonControl.FlatAppearance.BorderSize = 1;
            cancelButtonControl.Cursor = Cursors.Hand;
            cancelButtonControl.AccessibleName = "Keep Quick Controls installed";
            if (!previewMode)
            {
                cancelButtonControl.Click += CancelButtonClick;
            }
            actions.Controls.Add(cancelButtonControl);

            if (!previewMode)
            {
                AcceptButton = removeButton;
                CancelButton = cancelButtonControl;
                FormClosing += UninstallerFormClosing;
            }

            ResumeLayout(true);
        }

        private static Panel CreateHeader()
        {
            Panel header = new Panel();
            header.Name = "UninstallerHeader";
            header.Dock = DockStyle.Fill;
            header.Margin = Padding.Empty;
            header.Padding = new Padding(30, 20, 30, 18);
            header.BackColor = Color.FromArgb(248, 250, 252);

            TableLayoutPanel headerLayout = new TableLayoutPanel();
            headerLayout.Dock = DockStyle.Fill;
            headerLayout.Margin = Padding.Empty;
            headerLayout.Padding = Padding.Empty;
            headerLayout.ColumnCount = 2;
            headerLayout.RowCount = 1;
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.Controls.Add(headerLayout);

            Panel accent = new Panel();
            accent.Dock = DockStyle.Fill;
            accent.Margin = new Padding(0, 4, 0, 4);
            accent.BackColor = Color.FromArgb(220, 38, 38);
            headerLayout.Controls.Add(accent, 0, 0);

            TableLayoutPanel textLayout = new TableLayoutPanel();
            textLayout.Dock = DockStyle.Fill;
            textLayout.Margin = new Padding(16, 0, 0, 0);
            textLayout.Padding = Padding.Empty;
            textLayout.ColumnCount = 1;
            textLayout.RowCount = 2;
            textLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            textLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
            textLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
            headerLayout.Controls.Add(textLayout, 1, 0);

            Label titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Margin = Padding.Empty;
            titleLabel.Text = "Uninstall " + InstallEngine.ProductName + "?";
            titleLabel.UseMnemonic = false;
            titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = Color.FromArgb(15, 23, 42);
            titleLabel.TextAlign = ContentAlignment.BottomLeft;
            textLayout.Controls.Add(titleLabel, 0, 0);

            Label subtitleLabel = new Label();
            subtitleLabel.AutoSize = false;
            subtitleLabel.Dock = DockStyle.Fill;
            subtitleLabel.Margin = Padding.Empty;
            subtitleLabel.Text = "Shortcuts and the Start with Windows entry will be removed.";
            subtitleLabel.UseMnemonic = false;
            subtitleLabel.ForeColor = Color.FromArgb(71, 85, 105);
            subtitleLabel.TextAlign = ContentAlignment.TopLeft;
            textLayout.Controls.Add(subtitleLabel, 0, 1);

            return header;
        }

        private static Panel CreateFooter()
        {
            Panel footer = new Panel();
            footer.Name = "UninstallerFooter";
            footer.Dock = DockStyle.Fill;
            footer.Margin = Padding.Empty;
            footer.Padding = new Padding(30, 17, 30, 17);
            footer.BackColor = Color.FromArgb(248, 250, 252);
            footer.Paint += delegate(object sender, PaintEventArgs args)
            {
                using (Pen divider = new Pen(Color.FromArgb(226, 232, 240)))
                {
                    args.Graphics.DrawLine(divider, 0, 0, footer.ClientSize.Width, 0);
                }
            };
            return footer;
        }

        protected override bool ShowWithoutActivation
        {
            get { return previewMode; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                if (previewMode)
                {
                    parameters.ExStyle |= WsExNoActivate;
                }
                return parameters;
            }
        }

        private void RemoveButtonClick(object sender, EventArgs e)
        {
            if (removed)
            {
                Close();
                return;
            }

            if (removing)
            {
                return;
            }

            removing = true;
            removeButton.Enabled = false;
            cancelButtonControl.Enabled = false;
            removeSettingsCheck.Enabled = false;
            removeButton.Text = "Uninstalling...";
            removeButton.BackColor = Color.FromArgb(220, 38, 38);
            statusLabel.Text = "Preparing...";
            statusLabel.ForeColor = Color.FromArgb(15, 23, 42);
            detailLabel.Text = "Keep this window open for a moment.";
            progressBar.Visible = true;
            progressBar.Value = 2;
            bool removeSettings = removeSettingsCheck.Checked;

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate
            {
                InstallEngine.Uninstall(delegate(int percent, string message)
                {
                    worker.ReportProgress(percent, message);
                }, removeSettings);
            };
            worker.ProgressChanged += delegate(object progressSender, ProgressChangedEventArgs args)
            {
                int percent = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, args.ProgressPercentage));
                progressBar.Value = percent;
                statusLabel.Text = args.UserState as string ?? "Uninstalling...";
            };
            worker.RunWorkerCompleted += RemovalCompleted;
            worker.RunWorkerAsync();
        }

        private void RemovalCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            removing = false;

            if (args.Error != null)
            {
                progressBar.Value = 0;
                progressBar.Visible = false;
                statusLabel.Text = "Couldn't uninstall the app";
                statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
                detailLabel.Text = InstallEngine.GetFriendlyError(args.Error);
                removeButton.Enabled = true;
                removeButton.Text = "Try again";
                cancelButtonControl.Enabled = true;
                removeSettingsCheck.Enabled = true;
                return;
            }

            removed = true;
            progressBar.Value = 100;
            statusLabel.Text = "Ready to finish uninstalling";
            statusLabel.ForeColor = Color.FromArgb(21, 128, 61);
            detailLabel.Text = "Click Close to delete the remaining files. Thanks for using the app.";
            removeSettingsCheck.Visible = false;
            removeButton.Enabled = true;
            removeButton.Text = "Close";
            removeButton.BackColor = Color.FromArgb(37, 99, 235);
            cancelButtonControl.Visible = false;
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            if (!removing)
            {
                Close();
            }
        }

        private void UninstallerFormClosing(object sender, FormClosingEventArgs e)
        {
            if (removing)
            {
                e.Cancel = true;
                MessageBox.Show(
                    this,
                    "The app is being uninstalled. Please wait a little longer.",
                    InstallEngine.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
