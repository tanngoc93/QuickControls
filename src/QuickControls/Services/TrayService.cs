using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickControls.Services
{
    public sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _startupItem;
        private Icon _icon;

        public TrayService(bool startWithWindows)
        {
            _notifyIcon = new NotifyIcon();
            _icon = CreateIcon();
            _notifyIcon.Icon = _icon;
            _notifyIcon.Text = "Quick Controls — Volume & brightness";
            _notifyIcon.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 10F);
            menu.Padding = new Padding(4);
            menu.Items.Add(CreateItem("Open control panel", delegate { Raise(OpenRequested); }, true));
            menu.Items.Add(CreateItem("Mute / unmute", delegate { Raise(MuteRequested); }, false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateItem("Settings", delegate { Raise(SettingsRequested); }, false));
            _startupItem = CreateItem("Start with Windows", ToggleStartup, false);
            _startupItem.Checked = startWithWindows;
            _startupItem.CheckOnClick = true;
            menu.Items.Add(_startupItem);
            menu.Items.Add(CreateItem("About", delegate { Raise(AboutRequested); }, false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateItem("Exit", delegate { Raise(ExitRequested); }, false));
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += NotifyIconMouseClick;
        }

        public event EventHandler OpenRequested;
        public event EventHandler MuteRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler AboutRequested;
        public event EventHandler ExitRequested;
        public event EventHandler<StartupToggleEventArgs> StartupToggleRequested;

        public void SetStartupChecked(bool value)
        {
            _startupItem.Checked = value;
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
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_icon != null) _icon.Dispose();
        }

        private void NotifyIconMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                Raise(OpenRequested);
            }
        }

        private void ToggleStartup(object sender, EventArgs eventArgs)
        {
            EventHandler<StartupToggleEventArgs> handler = StartupToggleRequested;
            if (handler != null)
            {
                handler(this, new StartupToggleEventArgs(_startupItem.Checked));
            }
        }

        private static ToolStripMenuItem CreateItem(string text, EventHandler click, bool bold)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Padding = new Padding(2, 5, 2, 5);
            if (bold) item.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
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
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, 32, 32), Color.FromArgb(112, 78, 255), Color.FromArgb(47, 196, 222), 45F))
                {
                    graphics.FillEllipse(brush, 1, 1, 30, 30);
                }
                using (Pen pen = new Pen(Color.White, 2.2F))
                {
                    graphics.DrawLine(pen, 9, 14, 13, 14);
                    graphics.DrawLine(pen, 13, 14, 18, 10);
                    graphics.DrawLine(pen, 18, 10, 18, 22);
                    graphics.DrawLine(pen, 18, 22, 13, 18);
                    graphics.DrawLine(pen, 13, 18, 9, 18);
                    graphics.DrawArc(pen, 17, 12, 8, 8, -55, 110);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }

    public sealed class StartupToggleEventArgs : EventArgs
    {
        public StartupToggleEventArgs(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; private set; }
    }
}
