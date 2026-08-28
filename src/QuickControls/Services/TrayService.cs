using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuickControls.Models;

namespace QuickControls.Services
{
    public sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _openItem;
        private readonly ToolStripMenuItem _muteItem;
        private readonly ToolStripMenuItem _layoutRoot;
        private readonly ToolStripMenuItem _hardwareItem;
        private readonly ToolStripMenuItem _settingsItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _aboutItem;
        private readonly ToolStripMenuItem _exitItem;
        private readonly Dictionary<PanelLayoutMode, ToolStripMenuItem> _layoutItems;
        private Icon _icon;
        private bool _disposed;

        public TrayService(bool startWithWindows)
        {
            _layoutItems = new Dictionary<PanelLayoutMode, ToolStripMenuItem>();
            _notifyIcon = new NotifyIcon();
            _icon = CreateIcon();
            _notifyIcon.Icon = _icon;
            _notifyIcon.Visible = true;

            _menu = new ContextMenuStrip();
            _menu.Font = AppText.CreateFont(10F, FontStyle.Regular);
            _menu.Padding = new Padding(4);
            _openItem = CreateItem(delegate { Raise(OpenRequested); }, true);
            _muteItem = CreateItem(delegate { Raise(MuteRequested); }, false);
            _menu.Items.Add(_openItem);
            _menu.Items.Add(_muteItem);
            _menu.Items.Add(new ToolStripSeparator());

            _layoutRoot = new ToolStripMenuItem();
            AddLayoutItem(PanelLayoutMode.Full);
            AddLayoutItem(PanelLayoutMode.HorizontalMini);
            AddLayoutItem(PanelLayoutMode.VerticalMini);
            AddLayoutItem(PanelLayoutMode.EdgeDock);
            _menu.Items.Add(_layoutRoot);

            _hardwareItem = CreateItem(delegate { Raise(HardwareMonitorRequested); }, false);
            _menu.Items.Add(_hardwareItem);
            _settingsItem = CreateItem(delegate { Raise(SettingsRequested); }, false);
            _menu.Items.Add(_settingsItem);
            _startupItem = CreateItem(ToggleStartup, false);
            _startupItem.Checked = startWithWindows;
            _startupItem.CheckOnClick = true;
            _menu.Items.Add(_startupItem);
            _aboutItem = CreateItem(delegate { Raise(AboutRequested); }, false);
            _menu.Items.Add(_aboutItem);
            _menu.Items.Add(new ToolStripSeparator());
            _exitItem = CreateItem(delegate { Raise(ExitRequested); }, false);
            _menu.Items.Add(_exitItem);
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.MouseClick += NotifyIconMouseClick;
            ApplyLanguage();
        }

        public event EventHandler OpenRequested;
        public event EventHandler MuteRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler HardwareMonitorRequested;
        public event EventHandler AboutRequested;
        public event EventHandler ExitRequested;
        public event EventHandler<StartupToggleEventArgs> StartupToggleRequested;
        public event EventHandler<PanelLayoutEventArgs> LayoutRequested;

        public void ApplyLanguage()
        {
            if (_disposed) return;
            Font oldMenuFont = _menu.Font;
            Font oldOpenFont = _openItem.Font;
            _menu.Font = AppText.CreateFont(10F, FontStyle.Regular);
            _openItem.Font = AppText.CreateFont(10F, FontStyle.Bold);
            if (oldMenuFont != null) oldMenuFont.Dispose();
            if (oldOpenFont != null) oldOpenFont.Dispose();
            _notifyIcon.Text = AppText.Get("Tray.Tooltip");
            _openItem.Text = AppText.Get("Tray.OpenPanel");
            _muteItem.Text = AppText.Get("Tray.MuteUnmute");
            _layoutRoot.Text = AppText.Get("Tray.Layout");
            _layoutItems[PanelLayoutMode.Full].Text = AppText.Get("Layout.Full");
            _layoutItems[PanelLayoutMode.HorizontalMini].Text = AppText.Get("Layout.HorizontalMini");
            _layoutItems[PanelLayoutMode.VerticalMini].Text = AppText.Get("Layout.VerticalMini");
            _layoutItems[PanelLayoutMode.EdgeDock].Text = AppText.Get("Layout.EdgeDock");
            _hardwareItem.Text = AppText.Get("Tray.HardwareMonitor");
            _settingsItem.Text = AppText.Get("Tray.Settings");
            _startupItem.Text = AppText.Get("Tray.StartWithWindows");
            _aboutItem.Text = AppText.Get("Tray.About");
            _exitItem.Text = AppText.Get("Tray.Exit");
        }

        public void SetStartupChecked(bool value)
        {
            _startupItem.Checked = value;
        }

        public void SetLayoutChecked(PanelLayoutMode mode)
        {
            foreach (KeyValuePair<PanelLayoutMode, ToolStripMenuItem> item in _layoutItems)
                item.Value.Checked = item.Key == mode;
        }

        public void ShowInfo(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(5000);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _notifyIcon.MouseClick -= NotifyIconMouseClick;
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip = null;
            Font menuFont = _menu.Font;
            Font openFont = _openItem.Font;
            _notifyIcon.Dispose();
            _menu.Dispose();
            if (openFont != null) openFont.Dispose();
            if (menuFont != null && !object.ReferenceEquals(menuFont, openFont)) menuFont.Dispose();
            if (_icon != null) _icon.Dispose();
            _icon = null;
        }

        private void AddLayoutItem(PanelLayoutMode mode)
        {
            ToolStripMenuItem item = new ToolStripMenuItem();
            item.Padding = new Padding(2, 4, 2, 4);
            item.Click += delegate
            {
                SetLayoutChecked(mode);
                EventHandler<PanelLayoutEventArgs> handler = LayoutRequested;
                if (handler != null) handler(this, new PanelLayoutEventArgs(mode));
            };
            _layoutRoot.DropDownItems.Add(item);
            _layoutItems[mode] = item;
        }

        private void NotifyIconMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left) Raise(OpenRequested);
        }

        private void ToggleStartup(object sender, EventArgs eventArgs)
        {
            EventHandler<StartupToggleEventArgs> handler = StartupToggleRequested;
            if (handler != null) handler(this, new StartupToggleEventArgs(_startupItem.Checked));
        }

        private static ToolStripMenuItem CreateItem(EventHandler click, bool bold)
        {
            ToolStripMenuItem item = new ToolStripMenuItem();
            item.Padding = new Padding(2, 5, 2, 5);
            if (bold) item.Font = AppText.CreateFont(10F, FontStyle.Bold);
            item.Click += click;
            return item;
        }

        private static void Raise(EventHandler handler)
        {
            if (handler != null) handler(null, EventArgs.Empty);
        }

        private static Icon CreateIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = RoundedRectangle(new RectangleF(1, 1, 30, 30), 6F))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(21, 94, 239)))
                    graphics.FillPath(brush, path);
                using (Pen pen = new Pen(Color.White, 2.2F))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    PointF[] speaker = { new PointF(8, 14), new PointF(12, 14), new PointF(18, 9),
                        new PointF(18, 23), new PointF(12, 18), new PointF(8, 18) };
                    using (SolidBrush white = new SolidBrush(Color.White)) graphics.FillPolygon(white, speaker);
                    graphics.DrawArc(pen, 17, 12, 8, 8, -55, 110);
                }
                IntPtr handle = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2F;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }

    public sealed class StartupToggleEventArgs : EventArgs
    {
        public StartupToggleEventArgs(bool enabled) { Enabled = enabled; }
        public bool Enabled { get; private set; }
    }

    public sealed class PanelLayoutEventArgs : EventArgs
    {
        public PanelLayoutEventArgs(PanelLayoutMode mode) { Mode = mode; }
        public PanelLayoutMode Mode { get; private set; }
    }
}
