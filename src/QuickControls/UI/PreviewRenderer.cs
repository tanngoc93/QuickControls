using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using QuickControls.Models;
using QuickControls.Services;

namespace QuickControls.UI
{
    internal static class PreviewRenderer
    {
        [ThreadStatic]
        private static bool _visualStylesInitialized;

        public static void Render(string outputPath, bool compact)
        {
            RenderLayout(outputPath, compact ? PanelLayoutMode.HorizontalMini : PanelLayoutMode.Full);
        }

        public static void RenderLayout(string outputPath, PanelLayoutMode layout)
        {
            RenderLayoutLanguage(outputPath, layout, "en");
        }

        public static void RenderLayoutLanguage(string outputPath, PanelLayoutMode layout, string languageCode)
        {
            RunOnSta(delegate { RenderLayoutLanguageCore(outputPath, layout, languageCode); });
        }

        private static void RenderLayoutLanguageCore(string outputPath, PanelLayoutMode layout, string languageCode)
        {
            EnsureVisualStyles();
            AppText.SetLanguage(languageCode);

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            AppSettings settings = AppSettings.CreateDefaults();
            settings.FirstRun = false;
            settings.LanguageCode = AppText.CurrentLanguageCode;
            settings.AutoCollapse = false;
            settings.PanelCompact = layout == PanelLayoutMode.HorizontalMini;
            settings.PanelLayoutMode = layout;
            settings.PanelLeft = 20;
            settings.PanelTop = 20;

            using (PanelForm form = new PanelForm(settings))
            {
                List<BrightnessDevice> devices = new List<BrightnessDevice>();
                devices.Add(new PreviewBrightnessDevice("preview:laptop", AppText.Get("Display.BuiltIn")));
                devices.Add(new PreviewBrightnessDevice("preview:external", AppText.Get("Display.External")));
                form.SetAudioState(new AudioState(true, 72, false));
                form.SetBrightnessDevices(devices, devices[0].Id);
                form.SetBrightnessState(true, 58, string.Empty);
                CaptureForm(form, outputPath);
                form.PrepareForExit();
                form.Close();
            }
            Application.DoEvents();
        }

        public static void RenderSettings(string outputPath)
        {
            RenderSettingsPage(outputPath, "Interface", "en");
        }

        public static void RenderHardwareMonitor(string outputPath)
        {
            RenderHardwareMonitorLanguage(outputPath, "en");
        }

        public static void RenderHardwareMonitorLanguage(string outputPath, string languageCode)
        {
            RunOnSta(delegate { RenderHardwareMonitorCore(outputPath, languageCode, 1F); });
        }

        public static void RenderHardwareMonitorAtScale(
            string outputPath,
            string languageCode,
            float scale)
        {
            RunOnSta(delegate { RenderHardwareMonitorCore(outputPath, languageCode, scale); });
        }

        private static void RenderHardwareMonitorCore(string outputPath, string languageCode, float scale)
        {
            EnsureVisualStyles();
            AppText.SetLanguage(languageCode);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (PreviewHardwareMonitorService service = new PreviewHardwareMonitorService())
            using (HardwareMonitorForm form = new HardwareMonitorForm(service, false, false))
            {
                for (int index = 0; index < 60; index++)
                    form.ApplySnapshot(CreatePreviewHardwareSnapshot(index, DateTime.Now));
                if (Math.Abs(scale - 1F) > 0.001F)
                {
                    CaptureScaledHardwareMonitor(form, outputPath, scale);
                }
                else
                {
                    CaptureForm(form, outputPath);
                }
            }
            Application.DoEvents();
        }

        public static void ValidateHardwareMonitorLanguageLifecycle()
        {
            RunOnSta(ValidateHardwareMonitorLanguageLifecycleCore);
        }

        public static void ValidateHardwareMonitorWorkingAreaLifecycle()
        {
            RunOnSta(ValidateHardwareMonitorWorkingAreaLifecycleCore);
        }

        public static void ValidateHardwareMonitorVisibilityLifecycle()
        {
            RunOnSta(ValidateHardwareMonitorVisibilityLifecycleCore);
        }

        private static void ValidateHardwareMonitorVisibilityLifecycleCore()
        {
            EnsureVisualStyles();
            AppText.SetLanguage("en");
            using (PreviewHardwareMonitorService service = new PreviewHardwareMonitorService())
            using (HardwareMonitorForm form = new HardwareMonitorForm(service, false, true))
            {
                form.Opacity = 0.01D;
                form.ShowInTaskbar = false;
                form.ShowMonitor();
                Application.DoEvents();
                form.Close();
                Application.DoEvents();
                if (form.Visible || form.IsDisposed)
                    throw new InvalidOperationException(
                        "Hardware Monitor did not hide and remain reusable after a user close.");

                form.ShowMonitor();
                Application.DoEvents();
                if (!form.Visible || form.IsDisposed)
                    throw new InvalidOperationException("Hardware Monitor did not reopen after being hidden.");
                form.PrepareForExit();
                form.Close();
                Application.DoEvents();
                if (!form.IsDisposed)
                    throw new InvalidOperationException("Hardware Monitor did not close during application exit.");
            }
            Application.DoEvents();
        }

        private static void ValidateHardwareMonitorWorkingAreaLifecycleCore()
        {
            EnsureVisualStyles();
            AppText.SetLanguage("en");
            MethodInfo fitMethod = typeof(HardwareMonitorForm).GetMethod(
                "FitToWorkingArea", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fitMethod == null)
                throw new InvalidOperationException("Hardware Monitor working-area test hook is unavailable.");

            using (PreviewHardwareMonitorService service = new PreviewHardwareMonitorService())
            using (HardwareMonitorForm form = new HardwareMonitorForm(service, false, false))
            {
                form.Opacity = 0.01D;
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();

                Rectangle physicalArea = Screen.FromControl(form).WorkingArea;
                Rectangle largeArea = new Rectangle(
                    physicalArea.Left,
                    physicalArea.Top,
                    Math.Max(1920, physicalArea.Width),
                    Math.Max(1080, physicalArea.Height));
                fitMethod.Invoke(form, new object[] { largeArea, false });
                AssertFormInsideArea(form, largeArea);
                Size firstLargeSize = form.Size;

                Rectangle smallArea = new Rectangle(physicalArea.Left, physicalArea.Top, 760, 540);
                fitMethod.Invoke(form, new object[] { smallArea, false });
                AssertFormInsideArea(form, smallArea);
                Size firstSmallSize = form.Size;

                fitMethod.Invoke(form, new object[] { largeArea, false });
                AssertFormInsideArea(form, largeArea);
                if (Math.Abs(form.Width - firstLargeSize.Width) > 2 ||
                    Math.Abs(form.Height - firstLargeSize.Height) > 2)
                    throw new InvalidOperationException(
                        "Hardware Monitor did not restore its display-normalized size.");

                fitMethod.Invoke(form, new object[] { smallArea, false });
                AssertFormInsideArea(form, smallArea);
                if (Math.Abs(form.Width - firstSmallSize.Width) > 2 ||
                    Math.Abs(form.Height - firstSmallSize.Height) > 2)
                    throw new InvalidOperationException(
                        "Hardware Monitor accumulated scaling across display changes.");
            }
            Application.DoEvents();
        }

        private static void AssertFormInsideArea(Form form, Rectangle area)
        {
            if (form.Width > area.Width || form.Height > area.Height ||
                form.Left < area.Left || form.Top < area.Top ||
                form.Right > area.Right || form.Bottom > area.Bottom)
                throw new InvalidOperationException("Hardware Monitor is outside the working area.");
        }

        private static void ValidateHardwareMonitorLanguageLifecycleCore()
        {
            EnsureVisualStyles();
            AppText.SetLanguage("en");
            FieldInfo titleField = typeof(HardwareMonitorForm).GetField(
                "_titleLabel", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo statusField = typeof(HardwareMonitorForm).GetField(
                "_statusLabel", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo memoryField = typeof(HardwareMonitorForm).GetField(
                "_memoryCard", BindingFlags.Instance | BindingFlags.NonPublic);
            if (titleField == null || statusField == null || memoryField == null)
                throw new InvalidOperationException("Hardware Monitor language test hooks are unavailable.");

            DateTime sampledAt = new DateTime(2026, 8, 28, 22, 10, 28);
            using (PreviewHardwareMonitorService service = new PreviewHardwareMonitorService())
            using (HardwareMonitorForm form = new HardwareMonitorForm(service, false, false))
            {
                form.ApplySnapshot(CreatePreviewHardwareSnapshot(18, sampledAt));
                form.Opacity = 0.01D;
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                string[] languages = new string[] { "en", "vi", "ja", "zh-CN", "fr", "en" };
                for (int index = 0; index < languages.Length; index++)
                {
                    AppText.SetLanguage(languages[index]);
                    form.ApplyLanguage();
                    Label title = titleField.GetValue(form) as Label;
                    Label status = statusField.GetValue(form) as Label;
                    Control memory = memoryField.GetValue(form) as Control;
                    if (title == null || status == null || memory == null)
                        throw new InvalidOperationException("Hardware Monitor language controls are unavailable.");
                    if (!string.Equals(title.Text, AppText.Get("Hardware.Title"), StringComparison.Ordinal))
                        throw new InvalidOperationException("Hardware Monitor title did not change language.");
                    if (!string.Equals(title.Font.FontFamily.Name, AppText.GetFontFamilyName(true),
                        StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Hardware Monitor title did not change language font.");
                    string expectedTime = sampledAt.ToString("T", AppText.CurrentCulture);
                    if (status.Text.IndexOf(expectedTime, StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException("Hardware Monitor time did not use the selected culture.");
                    if (memory.AccessibleDescription == null ||
                        memory.AccessibleDescription.IndexOf(
                            AppText.Get("Hardware.Temperature"), StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException(
                            "Hardware Monitor accessibility text did not change language.");
                    AssertHardwareMonitorPaintsWithoutErrorGlyph(form);
                }
            }
            Application.DoEvents();
        }

        private static void AssertHardwareMonitorPaintsWithoutErrorGlyph(Form form)
        {
            form.Refresh();
            Application.DoEvents();
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                int redPixels = 0;
                for (int y = 0; y < bitmap.Height; y += 4)
                {
                    for (int x = 0; x < bitmap.Width; x += 4)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.R > 245 && pixel.G < 10 && pixel.B < 10) redPixels++;
                    }
                }
                if (redPixels > 20)
                    throw new InvalidOperationException(
                        "Hardware Monitor rendered a Windows Forms paint-error glyph after a language change.");
            }
        }

        private static HardwareSnapshot CreatePreviewHardwareSnapshot(int index, DateTime sampledAt)
        {
            double cpu = 34D + Math.Sin(index / 4D) * 18D + (index % 9 == 0 ? 12D : 0D);
            double gpu = 48D + Math.Sin(index / 5.5D) * 25D;
            double memory = 61D + Math.Sin(index / 12D) * 3D;
            double storage = 18D + Math.Sin(index / 2.8D) * 14D + (index % 13 == 0 ? 20D : 0D);
            return new HardwareSnapshot(
                sampledAt,
                new HardwareMetricReading("AMD Ryzen 7 7840U", cpu, null, null, null, true),
                new HardwareMetricReading("AMD Radeon 780M Graphics", gpu,
                    62D + Math.Sin(index / 8D) * 3D, null, null, true),
                new HardwareMetricReading("32 GB DDR5", memory, null,
                    20L * 1024L * 1024L * 1024L, 32L * 1024L * 1024L * 1024L, true),
                new HardwareMetricReading("NVMe solid-state drive", storage, 42D, null, null, true),
                "Windows",
                false);
        }

        public static void RenderSettingsPage(string outputPath, string pageName, string languageCode)
        {
            RunOnSta(delegate { RenderSettingsPageCore(outputPath, pageName, languageCode); });
        }

        public static void ValidateChoiceDropDownLifecycle()
        {
            RunOnSta(ValidateChoiceDropDownLifecycleCore);
        }

        private static void ValidateChoiceDropDownLifecycleCore()
        {
            EnsureVisualStyles();
            MethodInfo showDropDown = typeof(ModernChoiceBox).GetMethod(
                "ShowDropDown", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo dropDownMenuField = typeof(ModernChoiceBox).GetField(
                "_dropDownMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            if (showDropDown == null || dropDownMenuField == null)
                throw new InvalidOperationException("ModernChoiceBox dropdown test hooks are unavailable.");

            ContextMenuStrip ownedMenu = null;
            using (Form host = new NonActivatingTestForm())
            {
                ModernChoiceBox choice = new ModernChoiceBox();
                choice.SetBounds(8, 8, 200, 40);
                choice.Items.Add("English");
                choice.Items.Add("Vietnamese");
                choice.SelectedIndex = 0;
                host.Controls.Add(choice);
                host.Show();
                host.Refresh();
                Application.DoEvents();

                showDropDown.Invoke(choice, null);
                Application.DoEvents();
                ownedMenu = dropDownMenuField.GetValue(choice) as ContextMenuStrip;
                AssertChoiceMenuReady(ownedMenu, 2);
                ownedMenu.Items[1].PerformClick();
                Application.DoEvents();
                AssertChoiceSelection(choice, ownedMenu, 1);

                showDropDown.Invoke(choice, null);
                Application.DoEvents();
                ContextMenuStrip reopenedMenu = dropDownMenuField.GetValue(choice) as ContextMenuStrip;
                if (!object.ReferenceEquals(ownedMenu, reopenedMenu))
                    throw new InvalidOperationException("ModernChoiceBox did not reuse its owned dropdown menu.");
                AssertChoiceMenuReady(reopenedMenu, 2);
                reopenedMenu.Items[0].PerformClick();
                Application.DoEvents();
                AssertChoiceSelection(choice, reopenedMenu, 0);

                host.Close();
            }
            Application.DoEvents();
            if (ownedMenu == null || !ownedMenu.IsDisposed)
                throw new InvalidOperationException("ModernChoiceBox did not dispose its owned dropdown menu.");
        }

        private static void AssertChoiceMenuReady(ContextMenuStrip menu, int expectedItems)
        {
            if (menu == null || menu.IsDisposed)
                throw new InvalidOperationException("ModernChoiceBox dropdown menu is unavailable.");
            if (!menu.Visible)
                throw new InvalidOperationException("ModernChoiceBox dropdown menu did not open.");
            if (menu.Items.Count != expectedItems)
                throw new InvalidOperationException("ModernChoiceBox dropdown menu contains unexpected items.");
        }

        private static void AssertChoiceSelection(
            ModernChoiceBox choice, ContextMenuStrip menu, int expectedIndex)
        {
            if (choice.SelectedIndex != expectedIndex)
                throw new InvalidOperationException("ModernChoiceBox did not apply the selected item.");
            if (menu.IsDisposed)
                throw new InvalidOperationException("ModernChoiceBox disposed its dropdown during item selection.");
            if (menu.Visible)
                throw new InvalidOperationException("ModernChoiceBox dropdown remained open after selection.");
        }

        private static void RenderSettingsPageCore(string outputPath, string pageName, string languageCode)
        {
            EnsureVisualStyles();
            AppText.SetLanguage(languageCode);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            AppSettings settings = AppSettings.CreateDefaults();
            settings.FirstRun = false;
            settings.LanguageCode = AppText.CurrentLanguageCode;
            using (SettingsForm form = new SettingsForm(settings, delegate { return null; }))
            {
                form.SelectPreviewPage(pageName);
                DrawForm(form, outputPath);
            }
            Application.DoEvents();
        }

        private static void RunOnSta(ThreadStart action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception failure = null;
            Thread thread = new Thread((ThreadStart)delegate
            {
                try { action(); }
                catch (Exception exception) { failure = exception; }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new InvalidOperationException("UI preview rendering failed.", failure);
        }

        private static void EnsureVisualStyles()
        {
            if (_visualStylesInitialized) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _visualStylesInitialized = true;
        }

        private static void DrawForm(Form form, string outputPath)
        {
            CaptureForm(form, outputPath);
            form.Close();
        }

        private static void CaptureForm(Form form, string outputPath)
        {
            // A fully transparent WinForms window can skip child WM_PAINT work after
            // several previews are rendered on the same UI thread. A nearly invisible
            // window keeps painting deterministic without flashing a usable window.
            form.Opacity = 0.01D;
            form.Show();
            form.Refresh();
            Application.DoEvents();
            form.Update();
            CaptureVisibleControl(form, outputPath);
        }

        private static void CaptureScaledHardwareMonitor(
            HardwareMonitorForm form,
            string outputPath,
            float scale)
        {
            // A top-level Form cannot grow past SystemInformation.MaxWindowTrackSize.
            // Keep the real window at its normal size and enlarge its ordinary root
            // Panel, which is not capped by the CI desktop's tracking dimensions.
            form.Opacity = 0.01D;
            form.Show();
            form.Refresh();
            Application.DoEvents();
            Size originalSize = form.ClientSize;
            Control surface = form.ApplyPreviewScale(scale);
            Size expectedSize = new Size(
                Math.Max(1, (int)Math.Round(originalSize.Width * scale)),
                Math.Max(1, (int)Math.Round(originalSize.Height * scale)));
            if (surface.ClientSize != expectedSize)
                throw new InvalidOperationException(
                    "Hardware Monitor preview surface did not preserve its requested scale. " +
                    "Expected " + expectedSize.Width + "x" + expectedSize.Height +
                    ", received " + surface.ClientSize.Width + "x" + surface.ClientSize.Height + ".");
            CaptureVisibleControl(surface, outputPath);
        }

        private static void CaptureVisibleControl(Control control, string outputPath)
        {
            control.Refresh();
            Application.DoEvents();
            control.Update();
            for (int pass = 0; pass < 3; pass++)
            {
                using (Bitmap warmUp = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb))
                {
                    control.DrawToBitmap(warmUp, new Rectangle(0, 0, warmUp.Width, warmUp.Height));
                }
                control.Invalidate(true);
                control.Update();
                Application.DoEvents();
            }
            using (Bitmap bitmap = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb))
            {
                control.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }

        private sealed class PreviewBrightnessDevice : BrightnessDevice
        {
            public PreviewBrightnessDevice(string id, string name) : base(id, name) { }
            public override bool TryGetPercent(out int percent) { percent = 58; return true; }
            public override bool SetPercent(int percent) { return true; }
            public override void Dispose() { }
        }

        private sealed class PreviewHardwareMonitorService : IHardwareMonitorService
        {
            public HardwareSnapshot ReadSnapshot() { return HardwareSnapshot.Empty(); }
            public void Dispose() { }
        }

        private sealed class NonActivatingTestForm : Form
        {
            public NonActivatingTestForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-10000, -10000);
                ClientSize = new Size(216, 56);
                Opacity = 0.01D;
            }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams parameters = base.CreateParams;
                    parameters.ExStyle |= 0x08000000;
                    return parameters;
                }
            }
        }
    }
}
