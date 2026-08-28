using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QuickControls.Installer
{
    public static class InstallerPreviewRenderer
    {
        [ThreadStatic]
        private static bool visualStylesInitialized;

        public static void RenderUninstaller(string outputPath)
        {
            RunOnSta(delegate { RenderUninstallerCore(outputPath, 1F); });
        }

        public static void RenderUninstallerAtScale(string outputPath, float scaleFactor)
        {
            if (scaleFactor <= 0F)
            {
                throw new ArgumentOutOfRangeException("scaleFactor");
            }

            RunOnSta(delegate { RenderUninstallerCore(outputPath, scaleFactor); });
        }

        public static void ValidateUninstallerLayouts()
        {
            RunOnSta(ValidateUninstallerLayoutsCore);
        }

        private static void RenderUninstallerCore(string outputPath, float scaleFactor)
        {
            EnsureVisualStyles();
            using (UninstallerForm form = new UninstallerForm(true))
            {
                Render(form, outputPath, scaleFactor);
            }
        }

        private static void ValidateUninstallerLayoutsCore()
        {
            EnsureVisualStyles();
            float[] scaleFactors = { 1F, 1.25F, 1.5F, 1.75F, 2F };
            foreach (float scaleFactor in scaleFactors)
            {
                using (UninstallerForm form = new UninstallerForm(true))
                {
                    int originalStatusFontHeight = FindRequiredControl(form, "UninstallStatusLabel").Font.Height;
                    PrepareHiddenForm(form, scaleFactor);
                    AssertScaledFont(form, scaleFactor, originalStatusFontHeight);

                    ValidateUninstallerState(form, scaleFactor, "ready", false, true, true);

                    SetPreviewState(
                        form,
                        "Preparing...",
                        "Keep this window open for a moment.",
                        true,
                        true,
                        true,
                        "Uninstalling...");
                    ValidateUninstallerState(form, scaleFactor, "working", true, true, true);

                    SetPreviewState(
                        form,
                        "Couldn't uninstall the app",
                        "Something went wrong. Click Try again.\r\n\r\n" +
                        "If the error continues, see the log at:\r\n" +
                        @"C:\Users\Example\AppData\Local\Temp\QuickControls-Installer.log",
                        false,
                        true,
                        true,
                        "Try again");
                    ValidateUninstallerState(form, scaleFactor, "error", false, true, true);

                    SetPreviewState(
                        form,
                        "Ready to finish uninstalling",
                        "Click Close to delete the remaining files. Thanks for using the app.",
                        true,
                        false,
                        false,
                        "Close");
                    ValidateUninstallerState(form, scaleFactor, "completed", true, false, false);

                    form.Close();
                }
            }
            Application.DoEvents();
        }

        private static void SetPreviewState(
            Form form,
            string statusText,
            string detailText,
            bool progressVisible,
            bool checkboxVisible,
            bool keepButtonVisible,
            string actionText)
        {
            FindRequiredControl(form, "UninstallStatusLabel").Text = statusText;
            FindRequiredControl(form, "UninstallDetailLabel").Text = detailText;
            FindRequiredControl(form, "UninstallProgressBar").Visible = progressVisible;
            FindRequiredControl(form, "RemoveSettingsCheckBox").Visible = checkboxVisible;
            FindRequiredControl(form, "KeepAppButton").Visible = keepButtonVisible;
            FindRequiredControl(form, "UninstallButton").Text = actionText;
            form.PerformLayout();
            Application.DoEvents();
            form.Update();
            if (form.ContainsFocus || GetForegroundWindow() == form.Handle)
            {
                throw new InvalidOperationException("The uninstaller preview window activated unexpectedly.");
            }
            if (form.AcceptButton != null || form.CancelButton != null)
            {
                throw new InvalidOperationException("The uninstaller preview exposes an actionable dialog button.");
            }
        }

        private static void ValidateUninstallerState(
            Form form,
            float scaleFactor,
            string stateName,
            bool progressExpected,
            bool checkboxExpected,
            bool keepButtonExpected)
        {
            Control content = FindRequiredControl(form, "UninstallerContentLayout");
            Control detail = FindRequiredControl(form, "UninstallDetailLabel");
            Control checkbox = FindRequiredControl(form, "RemoveSettingsCheckBox");
            Control progress = FindRequiredControl(form, "UninstallProgressBar");
            Control footer = FindRequiredControl(form, "UninstallerFooter");
            Control keepButton = FindRequiredControl(form, "KeepAppButton");
            Control uninstallButton = FindRequiredControl(form, "UninstallButton");

            AssertVisibleChildrenInsideParents(form, scaleFactor, stateName);

            AssertInside(form, content, scaleFactor, stateName);
            AssertInside(form, detail, scaleFactor, stateName);
            AssertInside(form, footer, scaleFactor, stateName);
            AssertInside(form, uninstallButton, scaleFactor, stateName);
            if (checkboxExpected) AssertInside(form, checkbox, scaleFactor, stateName);
            if (keepButtonExpected) AssertInside(form, keepButton, scaleFactor, stateName);

            Rectangle detailBounds = GetBoundsInForm(form, detail);
            Rectangle checkboxBounds = GetBoundsInForm(form, checkbox);
            Rectangle footerBounds = GetBoundsInForm(form, footer);
            Rectangle keepBounds = GetBoundsInForm(form, keepButton);
            Rectangle uninstallBounds = GetBoundsInForm(form, uninstallButton);

            if (checkboxExpected && detailBounds.IntersectsWith(checkboxBounds))
            {
                throw CreateLayoutException("Details overlap the settings checkbox", scaleFactor, stateName);
            }
            if (checkboxExpected && checkboxBounds.Bottom > footerBounds.Top)
            {
                throw CreateLayoutException("The settings checkbox overlaps the footer", scaleFactor, stateName);
            }
            if (keepButtonExpected && keepBounds.IntersectsWith(uninstallBounds))
            {
                throw CreateLayoutException("Action buttons overlap", scaleFactor, stateName);
            }
            if (progress.Visible != progressExpected)
            {
                throw CreateLayoutException("Progress visibility is incorrect", scaleFactor, stateName);
            }
            if (checkbox.Visible != checkboxExpected)
            {
                throw CreateLayoutException("Checkbox visibility is incorrect", scaleFactor, stateName);
            }
            if (keepButton.Visible != keepButtonExpected)
            {
                throw CreateLayoutException("Keep button visibility is incorrect", scaleFactor, stateName);
            }

            if (checkboxExpected)
            {
                AssertPreferredSize(checkbox, scaleFactor, stateName);
            }
            if (keepButtonExpected)
            {
                AssertPreferredSize(keepButton, scaleFactor, stateName);
            }
            AssertPreferredSize(uninstallButton, scaleFactor, stateName);

            Size preferredDetailSize = detail.GetPreferredSize(new Size(detail.Width, 0));
            if (detail.Height < preferredDetailSize.Height)
            {
                throw CreateLayoutException("Detail text is clipped", scaleFactor, stateName);
            }
        }

        private static void AssertPreferredSize(Control control, float scaleFactor, string stateName)
        {
            Size preferredSize = control.GetPreferredSize(Size.Empty);
            if (control.Width < preferredSize.Width || control.Height < preferredSize.Height)
            {
                throw CreateLayoutException(control.Name + " text is clipped", scaleFactor, stateName);
            }
        }

        private static void AssertScaledFont(Form form, float scaleFactor, int originalFontHeight)
        {
            if (scaleFactor <= 1F)
            {
                return;
            }

            int expectedHeight = Math.Max(originalFontHeight + 1, (int)Math.Floor(originalFontHeight * scaleFactor));
            int actualHeight = FindRequiredControl(form, "UninstallStatusLabel").Font.Height;
            if (actualHeight < expectedHeight)
            {
                throw new InvalidOperationException(
                    "Uninstaller preview fonts did not scale to " + scaleFactor + "x.");
            }
        }

        private static InvalidOperationException CreateLayoutException(
            string message,
            float scaleFactor,
            string stateName)
        {
            return new InvalidOperationException(
                message + " in the " + stateName + " state at " + scaleFactor + "x simulated scaling.");
        }

        private static void AssertInside(Form form, Control control, float scaleFactor, string stateName)
        {
            Rectangle bounds = GetBoundsInForm(form, control);
            if (!form.ClientRectangle.Contains(bounds))
            {
                throw CreateLayoutException(control.Name + " is outside the window", scaleFactor, stateName);
            }
        }

        private static void AssertVisibleChildrenInsideParents(
            Control parent,
            float scaleFactor,
            string stateName)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Visible && !parent.ClientRectangle.Contains(child.Bounds))
                {
                    throw CreateLayoutException(
                        child.Name + " is clipped by " + parent.Name,
                        scaleFactor,
                        stateName);
                }
                AssertVisibleChildrenInsideParents(child, scaleFactor, stateName);
            }
        }

        private static Rectangle GetBoundsInForm(Form form, Control control)
        {
            Point location = control.Location;
            Control parent = control.Parent;
            while (parent != null && parent != form)
            {
                location.Offset(parent.Left, parent.Top);
                parent = parent.Parent;
            }
            return new Rectangle(location, control.Size);
        }

        private static Control FindRequiredControl(Form form, string name)
        {
            Control[] matches = form.Controls.Find(name, true);
            if (matches.Length == 0)
            {
                throw new InvalidOperationException("Required installer control was not found: " + name);
            }
            return matches[0];
        }

        private static void Render(Form form, string outputPath, float scaleFactor)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("A preview output path is required.", "outputPath");
            }

            string fullPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PrepareHiddenForm(form, scaleFactor);
            for (int pass = 0; pass < 2; pass++)
            {
                using (Bitmap warmUp = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
                {
                    form.DrawToBitmap(warmUp, new Rectangle(Point.Empty, warmUp.Size));
                }
                form.Invalidate(true);
                form.Update();
                Application.DoEvents();
            }

            using (Bitmap bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
            {
                bitmap.SetResolution(96F * scaleFactor, 96F * scaleFactor);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(fullPath, ImageFormat.Png);
            }

            form.Close();
            Application.DoEvents();
        }

        private static void PrepareHiddenForm(Form form, float scaleFactor)
        {
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            if (Math.Abs(scaleFactor - 1F) > 0.001F)
            {
                form.Scale(new SizeF(scaleFactor, scaleFactor));
                ScaleFonts(form, scaleFactor);
            }

            form.Opacity = 0.01D;
            form.Show();
            form.Refresh();
            form.PerformLayout();
            Application.DoEvents();
            form.Update();
        }

        private static void ScaleFonts(Control control, float scaleFactor)
        {
            foreach (Control child in control.Controls)
            {
                ScaleFonts(child, scaleFactor);
            }

            Font font = control.Font;
            control.Font = new Font(
                font.FontFamily,
                font.Size * scaleFactor,
                font.Style,
                font.Unit,
                font.GdiCharSet,
                font.GdiVerticalFont);
        }

        private static void RunOnSta(ThreadStart action)
        {
            Exception failure = null;
            Thread thread = new Thread((ThreadStart)delegate
            {
                try { action(); }
                catch (Exception exception) { failure = exception; }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(30000))
            {
                throw new TimeoutException("Installer UI preview rendering timed out.");
            }
            if (failure != null)
            {
                throw new InvalidOperationException("Installer UI preview rendering failed.", failure);
            }
        }

        private static void EnsureVisualStyles()
        {
            if (visualStylesInitialized)
            {
                return;
            }

            Application.EnableVisualStyles();
            visualStylesInitialized = true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
