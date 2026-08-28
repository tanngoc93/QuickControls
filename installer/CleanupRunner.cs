using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QuickControls.Installer
{
    internal static class CleanupRunner
    {
        private const int MoveFileDelayUntilReboot = 0x00000004;

        internal static string CreateHelperCopy()
        {
            string helperPath = Path.Combine(
                Path.GetTempPath(),
                "QuickControls-Cleanup-" + Guid.NewGuid().ToString("N") + ".exe");

            File.Copy(Process.GetCurrentProcess().MainModule.FileName, helperPath, true);
            return helperPath;
        }

        internal static void StartHelper(string helperPath, string directoryToDelete, int processIdToWaitFor)
        {
            string encodedDirectory = Convert.ToBase64String(Encoding.UTF8.GetBytes(directoryToDelete));
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = helperPath;
            startInfo.Arguments = "/cleanup " + encodedDirectory + " " + processIdToWaitFor.ToString();
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            Process process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Couldn't start the final cleanup step.");
            }

            process.Dispose();
        }

        internal static int Run(string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    return 2;
                }

                string requestedDirectory = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
                int processId;
                if (!int.TryParse(args[2], out processId))
                {
                    return 2;
                }

                string expectedDirectory = NormalizePath(InstallEngine.InstallDirectory);
                string normalizedRequestedDirectory = NormalizePath(requestedDirectory);
                if (!string.Equals(expectedDirectory, normalizedRequestedDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return 3;
                }

                WaitForProcess(processId);
                DeleteDirectoryWithRetries(normalizedRequestedDirectory);
                ScheduleSelfDelete();
                return Directory.Exists(normalizedRequestedDirectory) ? 1 : 0;
            }
            catch (Exception exception)
            {
                InstallEngine.WriteLog(exception);
                ScheduleSelfDelete();
                return 1;
            }
        }

        private static void WaitForProcess(int processId)
        {
            if (processId == Process.GetCurrentProcess().Id)
            {
                return;
            }

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    process.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
                // The process exited before the cleanup helper opened it.
            }
        }

        private static void DeleteDirectoryWithRetries(string directory)
        {
            int attempt;
            for (attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        return;
                    }

                    NormalizeAttributes(directory);
                    Directory.Delete(directory, true);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(300);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(300);
                }
            }
        }

        private static void NormalizeAttributes(string directory)
        {
            string[] files;
            string[] directories;
            int index;

            try
            {
                files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                for (index = 0; index < files.Length; index++)
                {
                    File.SetAttributes(files[index], FileAttributes.Normal);
                }

                directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);
                for (index = directories.Length - 1; index >= 0; index--)
                {
                    File.SetAttributes(directories[index], FileAttributes.Normal);
                }

                File.SetAttributes(directory, FileAttributes.Normal);
            }
            catch
            {
                // Directory.Delete will retry, while the outer loop handles temporary failures.
            }
        }

        private static string NormalizePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void ScheduleSelfDelete()
        {
            string executablePath = Application.ExecutablePath;

            try
            {
                string scriptPath = Path.Combine(
                    Path.GetTempPath(),
                    "QuickControls-Delete-" + Guid.NewGuid().ToString("N") + ".vbs");

                string script =
                    "On Error Resume Next\r\n" +
                    "Set fso = CreateObject(\"Scripting.FileSystemObject\")\r\n" +
                    "For i = 1 To 20\r\n" +
                    "  Err.Clear\r\n" +
                    "  fso.DeleteFile WScript.Arguments(0), True\r\n" +
                    "  If Err.Number = 0 Then Exit For\r\n" +
                    "  WScript.Sleep 300\r\n" +
                    "Next\r\n" +
                    "fso.DeleteFile WScript.ScriptFullName, True\r\n";

                File.WriteAllText(scriptPath, script, Encoding.Unicode);

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Path.Combine(Environment.SystemDirectory, "wscript.exe");
                startInfo.Arguments = Quote(scriptPath) + " " + Quote(executablePath);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process process = Process.Start(startInfo);
                if (process != null)
                {
                    process.Dispose();
                    MoveFileEx(executablePath, null, MoveFileDelayUntilReboot);
                    MoveFileEx(scriptPath, null, MoveFileDelayUntilReboot);
                    return;
                }
            }
            catch
            {
            }

            MoveFileEx(executablePath, null, MoveFileDelayUntilReboot);
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }
}
