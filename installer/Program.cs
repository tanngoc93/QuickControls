using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace QuickControls.Installer
{
    internal static class Program
    {
        private const string SingleInstanceName = "Local\\QuickControls.Installer.44DFD49D";

        [STAThread]
        private static void Main(string[] args)
        {
            if (HasArgument(args, "/cleanup"))
            {
                Environment.ExitCode = CleanupRunner.Run(args);
                return;
            }

            bool isUninstall = HasArgument(args, "/uninstall") ||
                string.Equals(
                    Path.GetFileNameWithoutExtension(Application.ExecutablePath),
                    "Uninstall",
                    StringComparison.OrdinalIgnoreCase);
            bool isSilent = HasArgument(args, "/silent") || HasArgument(args, "/quiet");

            bool ownsMutex;
            using (Mutex mutex = new Mutex(true, SingleInstanceName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    if (!isSilent)
                    {
                        MessageBox.Show(
                            "The installer is already open. Finish using that window first.",
                            InstallEngine.ProductName,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    Environment.ExitCode = 1618;
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (isSilent)
                {
                    RunSilent(isUninstall, HasArgument(args, "/remove-settings"));
                    return;
                }

                Application.Run(isUninstall ? (Form)new UninstallerForm() : new InstallerForm());
            }
        }

        private static void RunSilent(bool uninstall, bool removeSettings)
        {
            try
            {
                if (uninstall)
                {
                    InstallEngine.Uninstall(null, removeSettings);
                }
                else
                {
                    InstallEngine.Install(null);
                    InstallEngine.StartApplication(true);
                }

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                InstallEngine.WriteLog(exception);
                Environment.ExitCode = 1;
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            int index;
            for (index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
