using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace QuickControls.UI
{
    public sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About — Quick Controls";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(450, 300);
            BackColor = AppColors.Window;
            ForeColor = AppColors.Text;
            Font = new Font("Segoe UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            LogoControl logo = new LogoControl();
            logo.SetBounds(32, 28, 54, 54);
            Controls.Add(logo);

            Label title = new Label();
            title.Text = "Quick Controls";
            title.UseMnemonic = false;
            title.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            title.ForeColor = AppColors.Text;
            title.BackColor = Color.Transparent;
            title.SetBounds(104, 27, 310, 38);
            Controls.Add(title);

            Label version = new Label();
            version.Text = "Version 1.0.0";
            version.Font = new Font("Segoe UI", 9.5F);
            version.ForeColor = AppColors.MutedText;
            version.BackColor = Color.Transparent;
            version.SetBounds(107, 65, 250, 24);
            Controls.Add(version);

            Label description = new Label();
            description.Text = "A small utility for adjusting volume and brightness quickly on Windows.\r\n\r\nIt runs quietly in the system tray and doesn't require administrator access.";
            description.Font = new Font("Segoe UI", 10F);
            description.ForeColor = AppColors.Text;
            description.BackColor = Color.Transparent;
            description.SetBounds(34, 112, 380, 95);
            Controls.Add(description);

            ModernButton displaySettings = new ModernButton();
            displaySettings.Text = "Open Windows display settings";
            displaySettings.SetBounds(34, 226, 260, 42);
            displaySettings.Click += delegate
            {
                try { Process.Start("ms-settings:display"); }
                catch { }
            };
            Controls.Add(displaySettings);

            ModernButton close = new ModernButton();
            close.Text = "Close";
            close.FillColor = AppColors.Accent;
            close.ForeColor = Color.White;
            close.SetBounds(318, 226, 98, 42);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }
    }
}
