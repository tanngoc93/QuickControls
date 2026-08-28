using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QuickControls.Installer
{
    internal sealed class UninstallerForm : Form
    {
        private readonly Label statusLabel;
        private readonly Label detailLabel;
        private readonly ProgressBar progressBar;
        private readonly Button removeButton;
        private readonly Button cancelButtonControl;
        private readonly CheckBox removeSettingsCheck;
        private BackgroundWorker worker;
        private bool removing;
        private bool removed;

        internal UninstallerForm()
        {
            Text = "Uninstall " + InstallEngine.ProductName;
            ClientSize = new Size(520, 315);
            MinimumSize = new Size(536, 354);
            MaximumSize = new Size(536, 354);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = Color.FromArgb(248, 250, 252);
            Controls.Add(header);

            Label titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(28, 20);
            titleLabel.Size = new Size(464, 34);
            titleLabel.Text = "Uninstall " + InstallEngine.ProductName + "?";
            titleLabel.UseMnemonic = false;
            titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = Color.FromArgb(31, 41, 55);
            header.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.AutoSize = false;
            subtitleLabel.Location = new Point(30, 57);
            subtitleLabel.Size = new Size(460, 24);
            subtitleLabel.Text = "Windows shortcuts and the Start with Windows entry will be removed.";
            subtitleLabel.ForeColor = Color.FromArgb(100, 116, 139);
            header.Controls.Add(subtitleLabel);

            statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.Location = new Point(30, 118);
            statusLabel.Size = new Size(460, 28);
            statusLabel.Text = "The app will be closed before it is uninstalled.";
            statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            statusLabel.ForeColor = Color.FromArgb(31, 41, 55);
            Controls.Add(statusLabel);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(30, 154);
            progressBar.Size = new Size(460, 12);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Style = ProgressBarStyle.Continuous;
            Controls.Add(progressBar);

            detailLabel = new Label();
            detailLabel.AutoSize = false;
            detailLabel.Location = new Point(30, 178);
            detailLabel.Size = new Size(460, 50);
            detailLabel.Text = "You can reinstall at any time with the installer.";
            detailLabel.ForeColor = Color.FromArgb(100, 116, 139);
            Controls.Add(detailLabel);

            removeSettingsCheck = new CheckBox();
            removeSettingsCheck.AutoSize = true;
            removeSettingsCheck.Location = new Point(30, 216);
            removeSettingsCheck.Text = "Also delete saved shortcuts and settings";
            removeSettingsCheck.Checked = false;
            Controls.Add(removeSettingsCheck);

            cancelButtonControl = new Button();
            cancelButtonControl.Location = new Point(260, 249);
            cancelButtonControl.Size = new Size(105, 42);
            cancelButtonControl.Text = "Keep app";
            cancelButtonControl.FlatStyle = FlatStyle.Flat;
            cancelButtonControl.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            cancelButtonControl.Click += CancelButtonClick;
            Controls.Add(cancelButtonControl);

            removeButton = new Button();
            removeButton.Location = new Point(375, 249);
            removeButton.Size = new Size(115, 42);
            removeButton.Text = "Uninstall";
            removeButton.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            removeButton.BackColor = Color.FromArgb(220, 38, 38);
            removeButton.ForeColor = Color.White;
            removeButton.FlatStyle = FlatStyle.Flat;
            removeButton.FlatAppearance.BorderSize = 0;
            removeButton.Cursor = Cursors.Hand;
            removeButton.Click += RemoveButtonClick;
            Controls.Add(removeButton);

            AcceptButton = removeButton;
            CancelButton = cancelButtonControl;
            FormClosing += UninstallerFormClosing;
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
            statusLabel.Text = "Preparing...";
            detailLabel.Text = "Keep this window open for a moment.";
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
            removeButton.Enabled = true;
            removeButton.Text = "Close";
            removeButton.BackColor = Color.FromArgb(91, 80, 220);
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
