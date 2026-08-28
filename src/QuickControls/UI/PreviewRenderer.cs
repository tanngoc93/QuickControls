using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

        public static void RenderSettingsPage(string outputPath, string pageName, string languageCode)
        {
            RunOnSta(delegate { RenderSettingsPageCore(outputPath, pageName, languageCode); });
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
            for (int pass = 0; pass < 3; pass++)
            {
                using (Bitmap warmUp = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
                {
                    form.DrawToBitmap(warmUp, new Rectangle(0, 0, warmUp.Width, warmUp.Height));
                }
                form.Invalidate(true);
                form.Update();
                Application.DoEvents();
            }
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
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
    }
}
