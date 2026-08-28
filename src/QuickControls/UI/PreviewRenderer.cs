using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using QuickControls.Models;
using QuickControls.Services;

namespace QuickControls.UI
{
    internal static class PreviewRenderer
    {
        private static bool _visualStylesInitialized;

        public static void Render(string outputPath, bool compact)
        {
            EnsureVisualStyles();

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            AppSettings settings = AppSettings.CreateDefaults();
            settings.FirstRun = false;
            settings.AutoCollapse = false;
            settings.PanelCompact = compact;
            settings.PanelLeft = 20;
            settings.PanelTop = 20;

            using (PanelForm form = new PanelForm(settings))
            {
                List<BrightnessDevice> devices = new List<BrightnessDevice>();
                devices.Add(new PreviewBrightnessDevice("preview:laptop", "Built-in display"));
                devices.Add(new PreviewBrightnessDevice("preview:external", "External display"));
                form.SetAudioState(new AudioState(true, 72, false));
                form.SetBrightnessDevices(devices, devices[0].Id);
                form.SetBrightnessState(true, 58, string.Empty);
                form.Opacity = 0D;
                form.Show();
                form.Refresh();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
                form.PrepareForExit();
                form.Close();
            }
        }

        public static void RenderSettings(string outputPath)
        {
            EnsureVisualStyles();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            AppSettings settings = AppSettings.CreateDefaults();
            settings.FirstRun = false;
            using (SettingsForm form = new SettingsForm(settings, delegate { return null; }))
            {
                DrawForm(form, outputPath);
            }
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
            form.Opacity = 0D;
            form.Show();
            form.Refresh();
            Application.DoEvents();
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(outputPath, ImageFormat.Png);
            }
            form.Close();
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
