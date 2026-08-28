using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using QuickControls.UI;

namespace QuickControls
{
    internal static class Program
    {
        private const string InstanceMutexName = "Local\\QuickControls.Instance.3D933385";
        private const string ShowEventName = "Local\\QuickControls.Show.3D933385";
        private const string ExitEventName = "Local\\QuickControls.Exit.3D933385";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string previewPath = GetArgumentValue(args, "--render-preview");
            if (!string.IsNullOrEmpty(previewPath))
            {
                PreviewRenderer.Render(Path.GetFullPath(previewPath), HasArgument(args, "--compact"));
                return;
            }

            if (HasArgument(args, "--exit-for-update"))
            {
                SignalEvent(ExitEventName);
                return;
            }

            bool createdNew;
            using (EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
            using (EventWaitHandle exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName))
            using (Mutex instanceMutex = new Mutex(true, InstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    showEvent.Set();
                    return;
                }

                using (AppController controller = new AppController(
                    showEvent,
                    exitEvent,
                    HasArgument(args, "--background"),
                    HasArgument(args, "--no-startup")))
                {
                    Application.Run(controller);
                }
            }
        }

        private static void SignalEvent(string eventName)
        {
            try
            {
                using (EventWaitHandle targetEvent = EventWaitHandle.OpenExisting(eventName))
                {
                    targetEvent.Set();
                }
            }
            catch
            {
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            }
            return null;
        }
    }
}
