using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QuickControls.Installer
{
    internal sealed class InstallerForm : Form
    {
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly Button primaryButton;
        private readonly Label detailLabel;
        private BackgroundWorker worker;
        private bool installing;
        private bool installed;

        internal InstallerForm()
        {
            Text = "Install " + InstallEngine.ProductName;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 370);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            SizeGripStyle = SizeGripStyle.Hide;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 126;
            header.BackColor = Color.FromArgb(91, 80, 220);
            Controls.Add(header);

            PictureBox iconBox = new PictureBox();
            iconBox.Location = new Point(36, 31);
            iconBox.Size = new Size(64, 64);
            iconBox.SizeMode = PictureBoxSizeMode.Zoom;
            Icon setupIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (setupIcon != null)
            {
                iconBox.Image = setupIcon.ToBitmap();
                setupIcon.Dispose();
            }
            header.Controls.Add(iconBox);
            FormClosed += delegate
            {
                if (iconBox.Image != null) iconBox.Image.Dispose();
            };

            Label titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(112, 26);
            titleLabel.Size = new Size(420, 43);
            titleLabel.Text = InstallEngine.ProductName;
            titleLabel.UseMnemonic = false;
            titleLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = Color.White;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            header.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.AutoSize = false;
            subtitleLabel.Location = new Point(115, 70);
            subtitleLabel.Size = new Size(410, 30);
            subtitleLabel.Text = "Adjust volume and brightness in seconds";
            subtitleLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
            subtitleLabel.ForeColor = Color.FromArgb(224, 235, 255);
            header.Controls.Add(subtitleLabel);

            Label instructionLabel = new Label();
            instructionLabel.AutoSize = false;
            instructionLabel.Location = new Point(34, 148);
            instructionLabel.Size = new Size(492, 44);
            instructionLabel.Text = InstallEngine.IsInstalled
                ? "The app is already installed. Click below to reinstall or update it."
                : "Just click Install now. There are no folders or settings to choose.";
            instructionLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
            instructionLabel.ForeColor = Color.FromArgb(45, 55, 72);
            Controls.Add(instructionLabel);

            statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.Location = new Point(34, 205);
            statusLabel.Size = new Size(492, 25);
            statusLabel.Text = "Ready to install";
            statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            statusLabel.ForeColor = Color.FromArgb(31, 41, 55);
            Controls.Add(statusLabel);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(34, 235);
            progressBar.Size = new Size(492, 12);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Continuous;
            Controls.Add(progressBar);

            detailLabel = new Label();
            detailLabel.AutoSize = false;
            detailLabel.Location = new Point(34, 257);
            detailLabel.Size = new Size(492, 38);
            detailLabel.Text = "The app will open automatically after installation.";
            detailLabel.ForeColor = Color.FromArgb(100, 116, 139);
            Controls.Add(detailLabel);

            primaryButton = new Button();
            primaryButton.Location = new Point(326, 310);
            primaryButton.Size = new Size(200, 42);
            primaryButton.Text = InstallEngine.IsInstalled ? "Reinstall / update" : "Install now";
            primaryButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            primaryButton.BackColor = Color.FromArgb(91, 80, 220);
            primaryButton.ForeColor = Color.White;
            primaryButton.FlatStyle = FlatStyle.Flat;
            primaryButton.FlatAppearance.BorderSize = 0;
            primaryButton.Cursor = Cursors.Hand;
            primaryButton.Click += PrimaryButtonClick;
            Controls.Add(primaryButton);

            Button cancelButton = new Button();
            cancelButton.Location = new Point(216, 310);
            cancelButton.Size = new Size(100, 42);
            cancelButton.Text = "Maybe later";
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            cancelButton.Click += CancelButtonClick;
            Controls.Add(cancelButton);

            AcceptButton = primaryButton;
            CancelButton = cancelButton;
            FormClosing += InstallerFormClosing;
        }

        private void PrimaryButtonClick(object sender, EventArgs e)
        {
            if (installed)
            {
                Close();
                return;
            }

            if (installing)
            {
                return;
            }

            BeginInstallation();
        }

        private void BeginInstallation()
        {
            installing = true;
            primaryButton.Enabled = false;
            primaryButton.Text = "Installing...";
            statusLabel.Text = "Preparing...";
            detailLabel.Text = "Keep this window open for a moment.";
            progressBar.Value = 2;

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate
            {
                InstallEngine.Install(delegate(int percent, string message)
                {
                    worker.ReportProgress(percent, message);
                });
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs args)
            {
                int percent = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, args.ProgressPercentage));
                progressBar.Value = percent;
                statusLabel.Text = args.UserState as string ?? "Installing...";
            };
            worker.RunWorkerCompleted += InstallationCompleted;
            worker.RunWorkerAsync();
        }

        private void InstallationCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            installing = false;

            if (args.Error != null)
            {
                progressBar.Value = 0;
                statusLabel.Text = "Couldn't install the app";
                statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
                detailLabel.Text = InstallEngine.GetFriendlyError(args.Error);
                primaryButton.Enabled = true;
                primaryButton.Text = "Try again";
                return;
            }

            bool launchSucceeded = false;
            try
            {
                launchSucceeded = InstallEngine.StartApplication(false);
            }
            catch (Exception exception)
            {
                InstallEngine.WriteLog(exception);
            }

            installed = true;
            progressBar.Value = 100;
            statusLabel.Text = "Installed successfully!";
            statusLabel.ForeColor = Color.FromArgb(21, 128, 61);
            if (!launchSucceeded)
            {
                detailLabel.Text = "Installation finished, but Windows couldn't open the app. Try the desktop shortcut.";
            }
            else if (InstallEngine.IsStartupEnabled)
            {
                detailLabel.Text = "The app is open and will now start with Windows.";
            }
            else
            {
                detailLabel.Text = "The app is open. Start with Windows is turned off.";
            }
            primaryButton.Enabled = true;
            primaryButton.Text = "Done";
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            if (!installing)
            {
                Close();
            }
        }

        private void InstallerFormClosing(object sender, FormClosingEventArgs e)
        {
            if (installing)
            {
                e.Cancel = true;
                MessageBox.Show(
                    this,
                    "The app is being installed. Please wait a little longer.",
                    InstallEngine.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
