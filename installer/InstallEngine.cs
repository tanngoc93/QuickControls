using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace QuickControls.Installer
{
    internal static class InstallEngine
    {
        internal const string ProductName = "Quick Controls";
        internal const string ApplicationFileName = "QuickControls.exe";
        internal const string ConfigurationFileName = "QuickControls.exe.config";
        internal const string UninstallerFileName = "Uninstall.exe";

        private const string ApplicationResourceName = "QuickControls.Payload.exe";
        private const string ConfigurationResourceName = "QuickControls.Payload.config";
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryUninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickControls";
        private const string RegistryValueName = "QuickControls";
        private const string ExitEventName = "Local\\QuickControls.Exit.3D933385";

        internal static string InstallDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "QuickControls");
            }
        }

        internal static string ApplicationPath
        {
            get { return Path.Combine(InstallDirectory, ApplicationFileName); }
        }

        internal static string SettingsDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QuickControls");
            }
        }

        internal static bool IsInstalled
        {
            get { return File.Exists(ApplicationPath); }
        }

        internal static void Install(Action<int, string> reportProgress)
        {
            string applicationTemporaryPath = null;
            string configurationTemporaryPath = null;
            string uninstallerTemporaryPath = null;
            bool wasInstalled = IsInstalled;
            bool startupWasEnabled = IsStartupEnabled;
            bool installDirectoryAlreadyExisted = Directory.Exists(InstallDirectory);
            bool installCompleted = false;
            InstallationTransaction transaction = null;

            try
            {
                Report(reportProgress, 5, "Preparing...");
                Directory.CreateDirectory(InstallDirectory);
                StopApplication();

                string suffix = ".new-" + Guid.NewGuid().ToString("N");
                applicationTemporaryPath = ApplicationPath + suffix;
                configurationTemporaryPath = Path.Combine(InstallDirectory, ConfigurationFileName) + suffix;
                uninstallerTemporaryPath = Path.Combine(InstallDirectory, UninstallerFileName) + suffix;

                Report(reportProgress, 25, "Copying the app...");
                ExtractEmbeddedFile(ApplicationResourceName, applicationTemporaryPath);
                ExtractEmbeddedFile(ConfigurationResourceName, configurationTemporaryPath);

                Report(reportProgress, 50, "Finishing installation files...");
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, uninstallerTemporaryPath, true);

                transaction = new InstallationTransaction();

                transaction.ReplaceFile(applicationTemporaryPath, ApplicationPath);
                applicationTemporaryPath = null;
                transaction.ReplaceFile(
                    configurationTemporaryPath,
                    Path.Combine(InstallDirectory, ConfigurationFileName));
                configurationTemporaryPath = null;
                transaction.ReplaceFile(
                    uninstallerTemporaryPath,
                    Path.Combine(InstallDirectory, UninstallerFileName));
                uninstallerTemporaryPath = null;

                Report(reportProgress, 70, "Creating shortcuts...");
                CreateShortcuts();

                Report(reportProgress, 85, "Finishing installation...");
                if (!wasInstalled || startupWasEnabled) RegisterStartup();
                RegisterUninstaller();

                transaction.Commit();
                installCompleted = true;
                RemoveLegacyShortcuts();
                Report(reportProgress, 100, "Installation complete!");
            }
            catch (Exception exception)
            {
                WriteLog(exception);

                if (transaction != null)
                {
                    Exception rollbackException = transaction.Rollback();
                    if (rollbackException != null)
                    {
                        WriteLog(new InvalidOperationException(
                            "Installation rollback did not complete cleanly.",
                            rollbackException));
                    }
                }

                throw;
            }
            finally
            {
                DeleteFileQuietly(applicationTemporaryPath);
                DeleteFileQuietly(configurationTemporaryPath);
                DeleteFileQuietly(uninstallerTemporaryPath);
                if (transaction != null) transaction.Dispose();
                if (!installCompleted && !installDirectoryAlreadyExisted)
                {
                    DeleteDirectoryIfEmptyQuietly(InstallDirectory);
                }
            }
        }

        internal static void Uninstall(Action<int, string> reportProgress, bool removeSettings)
        {
            string cleanupHelperPath = null;

            try
            {
                Report(reportProgress, 10, "Closing the app...");
                StopApplication();

                Report(reportProgress, 30, "Preparing cleanup...");
                cleanupHelperPath = CleanupRunner.CreateHelperCopy();

                Report(reportProgress, 50, "Removing the Start with Windows entry...");
                RemoveStartupRegistration();
                RemoveUninstallerRegistration();

                Report(reportProgress, 70, "Removing shortcuts...");
                RemoveShortcuts();
                RemoveLegacyShortcuts();
                if (removeSettings) DeleteDirectoryQuietly(SettingsDirectory);

                Report(reportProgress, 90, "Removing the app...");
                DeleteInstalledFilesExceptRunningUninstaller();
                CleanupRunner.StartHelper(cleanupHelperPath, InstallDirectory, Process.GetCurrentProcess().Id);
                cleanupHelperPath = null;

                Report(reportProgress, 100, "Ready to finish uninstalling.");
            }
            catch (Exception exception)
            {
                WriteLog(exception);
                throw;
            }
            finally
            {
                if (cleanupHelperPath != null)
                {
                    DeleteFileQuietly(cleanupHelperPath);
                }
            }
        }

        internal static bool StartApplication(bool background)
        {
            if (!File.Exists(ApplicationPath))
            {
                throw new FileNotFoundException("The app was not found after installation.", ApplicationPath);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = ApplicationPath;
            startInfo.WorkingDirectory = InstallDirectory;
            startInfo.UseShellExecute = true;
            if (background) startInfo.Arguments = "--background";
            Process process = Process.Start(startInfo);
            if (process == null) return false;
            process.Dispose();
            return true;
        }

        internal static bool IsStartupEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, false))
                    {
                        return key != null && key.GetValue(RegistryValueName) != null;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static void WriteLog(Exception exception)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "QuickControls-Installer.log");
                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}{3}{2}",
                    DateTime.Now,
                    exception,
                    Environment.NewLine,
                    new string('-', 72));
                File.AppendAllText(logPath, message, Encoding.UTF8);
            }
            catch
            {
                // Logging must never interrupt installation or removal.
            }
        }

        internal static string GetFriendlyError(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
            {
                return "Windows couldn't write a file. Close the app, then click Try again.";
            }

            if (exception is IOException)
            {
                return "A file is in use. Close the app, then click Try again.";
            }

            return "Something went wrong. Click Try again.\r\n\r\n" +
                   "If the error continues, see the log at:\r\n" +
                   Path.Combine(Path.GetTempPath(), "QuickControls-Installer.log");
        }

        private static void ExtractEmbeddedFile(string resourceName, string destinationPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("The installer is missing a component: " + resourceName);
                }

                using (FileStream output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush();
                }
            }
        }

        private static void StopApplication()
        {
            SignalGracefulExit();
            Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ApplicationFileName));
            int index;

            for (index = 0; index < processes.Length; index++)
            {
                using (Process process = processes[index])
                {
                    if (process.Id == Process.GetCurrentProcess().Id || !IsInstalledApplicationProcess(process))
                    {
                        continue;
                    }

                    try
                    {
                        if (process.CloseMainWindow())
                        {
                            process.WaitForExit(2000);
                        }

                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                        }
                    }
                    catch
                    {
                        // Continue; the following file operation will report a clear error if the app still holds a lock.
                    }
                }
            }

            Thread.Sleep(150);
        }

        private static void SignalGracefulExit()
        {
            try
            {
                using (EventWaitHandle exitEvent = EventWaitHandle.OpenExisting(ExitEventName))
                {
                    exitEvent.Set();
                }
                Thread.Sleep(250);
            }
            catch
            {
            }
        }

        private static bool IsInstalledApplicationProcess(Process process)
        {
            try
            {
                string processPath = Path.GetFullPath(process.MainModule.FileName);
                string installedPath = Path.GetFullPath(ApplicationPath);
                return string.Equals(processPath, installedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void CreateShortcuts()
        {
            NativeShortcut.Create(
                GetStartMenuShortcutPath(),
                ApplicationPath,
                InstallDirectory,
                ProductName);
            NativeShortcut.Create(
                GetDesktopShortcutPath(),
                ApplicationPath,
                InstallDirectory,
                ProductName);
        }

        private static void RemoveShortcuts()
        {
            DeleteFileQuietly(GetStartMenuShortcutPath());
            DeleteFileQuietly(GetDesktopShortcutPath());
        }

        private static void RemoveLegacyShortcuts()
        {
            string[] legacyNames =
            {
                "Sound & Brightness"
            };
            string[] directories =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };

            for (int directoryIndex = 0; directoryIndex < directories.Length; directoryIndex++)
            {
                for (int nameIndex = 0; nameIndex < legacyNames.Length; nameIndex++)
                {
                    DeleteFileQuietly(Path.Combine(directories[directoryIndex], legacyNames[nameIndex] + ".lnk"));
                }
            }
        }

        private static string GetStartMenuShortcutPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                ProductName + ".lnk");
        }

        private static string GetDesktopShortcutPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                ProductName + ".lnk");
        }

        private static void RegisterStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryRunPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Couldn't enable Start with Windows.");
                }

                key.SetValue(RegistryValueName, BuildStartupCommand(ApplicationPath), RegistryValueKind.String);
                key.Flush();
            }
        }

        private static string BuildStartupCommand(string executablePath)
        {
            return Quote(executablePath) + " --startup";
        }

        private static void RegisterUninstaller()
        {
            string uninstallerPath = Path.Combine(InstallDirectory, UninstallerFileName);
            string displayVersion = GetDisplayVersion();
            int estimatedSize = GetEstimatedSizeInKilobytes();

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryUninstallPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Couldn't register the uninstaller.");
                }

                key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", displayVersion, RegistryValueKind.String);
                key.SetValue("Publisher", "QuickControls", RegistryValueKind.String);
                key.SetValue("InstallLocation", InstallDirectory, RegistryValueKind.String);
                key.SetValue("DisplayIcon", Quote(ApplicationPath) + ",0", RegistryValueKind.String);
                key.SetValue("UninstallString", Quote(uninstallerPath) + " /uninstall", RegistryValueKind.String);
                key.SetValue("QuietUninstallString", Quote(uninstallerPath) + " /uninstall /silent", RegistryValueKind.String);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", estimatedSize, RegistryValueKind.DWord);
            }
        }

        private static void RemoveStartupRegistration()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(RegistryValueName, false);
                }
            }
        }

        private static void RemoveUninstallerRegistration()
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryUninstallPath, false);
        }

        private static string GetDisplayVersion()
        {
            try
            {
                string version = FileVersionInfo.GetVersionInfo(ApplicationPath).FileVersion;
                if (!string.IsNullOrEmpty(version))
                {
                    return version;
                }
            }
            catch
            {
            }

            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private static int GetEstimatedSizeInKilobytes()
        {
            long bytes = 0;
            string[] files = Directory.GetFiles(InstallDirectory);
            int index;
            for (index = 0; index < files.Length; index++)
            {
                try
                {
                    string fileName = Path.GetFileName(files[index]);
                    if (fileName.IndexOf(".rollback-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fileName.IndexOf(".new-", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    bytes += new FileInfo(files[index]).Length;
                }
                catch
                {
                }
            }

            long kilobytes = Math.Max(1L, (bytes + 1023L) / 1024L);
            return kilobytes > int.MaxValue ? int.MaxValue : (int)kilobytes;
        }

        private static void DeleteInstalledFilesExceptRunningUninstaller()
        {
            DeleteFileQuietly(ApplicationPath);
            DeleteFileQuietly(Path.Combine(InstallDirectory, ConfigurationFileName));
            DeleteFileQuietly(ApplicationPath + ".old");
            DeleteFileQuietly(Path.Combine(InstallDirectory, ConfigurationFileName) + ".old");
        }

        private sealed class InstallationTransaction : IDisposable
        {
            private readonly string transactionSuffix;
            private readonly List<FileReplacement> replacements;
            private FileSnapshot startMenuShortcut;
            private FileSnapshot desktopShortcut;
            private RegistryValueSnapshot startupRegistration;
            private RegistryKeySnapshot uninstallRegistration;
            private bool committed;
            private bool rolledBack;

            internal InstallationTransaction()
            {
                transactionSuffix = ".rollback-" + Guid.NewGuid().ToString("N");
                replacements = new List<FileReplacement>();

                try
                {
                    startMenuShortcut = FileSnapshot.Capture(GetStartMenuShortcutPath());
                    desktopShortcut = FileSnapshot.Capture(GetDesktopShortcutPath());
                    startupRegistration = RegistryValueSnapshot.Capture(
                        RegistryRunPath,
                        RegistryValueName);
                    uninstallRegistration = RegistryKeySnapshot.Capture(RegistryUninstallPath);
                }
                catch
                {
                    AllowSnapshotBackupDeletion();
                    DisposeSnapshots();
                    throw;
                }
            }

            internal void ReplaceFile(string sourcePath, string destinationPath)
            {
                if (committed || rolledBack)
                {
                    throw new InvalidOperationException("The installation transaction is already complete.");
                }

                bool hadOriginal = File.Exists(destinationPath);
                string backupPath = destinationPath + transactionSuffix;
                FileReplacement replacement = new FileReplacement(
                    destinationPath,
                    backupPath,
                    hadOriginal);
                replacements.Add(replacement);

                if (hadOriginal)
                {
                    File.Replace(sourcePath, destinationPath, backupPath, true);
                }
                else
                {
                    File.Move(sourcePath, destinationPath);
                }

                replacement.Applied = true;
            }

            internal void Commit()
            {
                if (committed) return;
                if (rolledBack)
                {
                    throw new InvalidOperationException("A rolled-back transaction cannot be committed.");
                }

                int index;
                for (index = 0; index < replacements.Count; index++)
                {
                    DeleteFileQuietly(replacements[index].BackupPath);
                    replacements[index].BackupHandled = true;
                }

                committed = true;
                AllowSnapshotBackupDeletion();
                DisposeSnapshots();
            }

            internal Exception Rollback()
            {
                if (committed || rolledBack) return null;

                List<Exception> errors = new List<Exception>();
                int index;

                for (index = replacements.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        RestoreReplacement(replacements[index]);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                RestoreSnapshot(startMenuShortcut, errors);
                RestoreSnapshot(desktopShortcut, errors);

                try
                {
                    if (startupRegistration != null) startupRegistration.Restore();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                try
                {
                    if (uninstallRegistration != null) uninstallRegistration.Restore();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                rolledBack = true;
                DisposeSnapshots();

                return errors.Count == 0
                    ? null
                    : new AggregateException("One or more rollback operations failed.", errors);
            }

            public void Dispose()
            {
                DisposeSnapshots();

                if (committed || rolledBack)
                {
                    int index;
                    for (index = 0; index < replacements.Count; index++)
                    {
                        FileReplacement replacement = replacements[index];
                        if (committed || replacement.BackupHandled)
                        {
                            DeleteFileQuietly(replacement.BackupPath);
                        }
                    }
                }
            }

            private static void RestoreReplacement(FileReplacement replacement)
            {
                if (!replacement.Applied) return;
                DeleteFileStrict(replacement.DestinationPath);

                if (replacement.HadOriginal)
                {
                    if (!File.Exists(replacement.BackupPath))
                    {
                        throw new FileNotFoundException(
                            "The previous installed file could not be found during rollback.",
                            replacement.BackupPath);
                    }

                    File.Move(replacement.BackupPath, replacement.DestinationPath);
                }

                replacement.BackupHandled = true;
            }

            private static void RestoreSnapshot(FileSnapshot snapshot, List<Exception> errors)
            {
                if (snapshot == null) return;

                try
                {
                    snapshot.Restore();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            private static void DeleteFileStrict(string path)
            {
                if (!File.Exists(path)) return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }

            private void DisposeSnapshots()
            {
                if (startMenuShortcut != null) startMenuShortcut.Dispose();
                if (desktopShortcut != null) desktopShortcut.Dispose();
            }

            private void AllowSnapshotBackupDeletion()
            {
                if (startMenuShortcut != null) startMenuShortcut.AllowBackupDeletion();
                if (desktopShortcut != null) desktopShortcut.AllowBackupDeletion();
            }
        }

        private sealed class FileReplacement
        {
            internal FileReplacement(string destinationPath, string backupPath, bool hadOriginal)
            {
                DestinationPath = destinationPath;
                BackupPath = backupPath;
                HadOriginal = hadOriginal;
            }

            internal readonly string DestinationPath;
            internal readonly string BackupPath;
            internal readonly bool HadOriginal;
            internal bool Applied;
            internal bool BackupHandled;
        }

        private sealed class FileSnapshot : IDisposable
        {
            private FileSnapshot(string originalPath, bool existed, string backupPath)
            {
                OriginalPath = originalPath;
                Existed = existed;
                BackupPath = backupPath;
            }

            internal readonly string OriginalPath;
            internal readonly bool Existed;
            internal readonly string BackupPath;
            private bool backupMayBeDeleted;

            internal static FileSnapshot Capture(string path)
            {
                if (!File.Exists(path))
                {
                    return new FileSnapshot(path, false, null);
                }

                string backupPath = Path.Combine(
                    Path.GetTempPath(),
                    "QuickControls-Shortcut-" + Guid.NewGuid().ToString("N") + ".bak");

                try
                {
                    File.Copy(path, backupPath, true);
                    return new FileSnapshot(path, true, backupPath);
                }
                catch
                {
                    DeleteFileQuietly(backupPath);
                    throw;
                }
            }

            internal void Restore()
            {
                if (!Existed)
                {
                    DeleteFileStrict(OriginalPath);
                    backupMayBeDeleted = true;
                    return;
                }

                if (!File.Exists(BackupPath))
                {
                    throw new FileNotFoundException(
                        "The previous shortcut could not be found during rollback.",
                        BackupPath);
                }

                string directory = Path.GetDirectoryName(OriginalPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                DeleteFileStrict(OriginalPath);
                File.Copy(BackupPath, OriginalPath, true);
                backupMayBeDeleted = true;
            }

            internal void AllowBackupDeletion()
            {
                backupMayBeDeleted = true;
            }

            public void Dispose()
            {
                if (backupMayBeDeleted) DeleteFileQuietly(BackupPath);
            }

            private static void DeleteFileStrict(string path)
            {
                if (!File.Exists(path)) return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        private sealed class RegistryValueSnapshot
        {
            private RegistryValueSnapshot(
                string keyPath,
                string valueName,
                bool valueExisted,
                object value,
                RegistryValueKind valueKind)
            {
                KeyPath = keyPath;
                ValueName = valueName;
                ValueExisted = valueExisted;
                Value = value;
                ValueKind = valueKind;
            }

            private readonly string KeyPath;
            private readonly string ValueName;
            private readonly bool ValueExisted;
            private readonly object Value;
            private readonly RegistryValueKind ValueKind;

            internal static RegistryValueSnapshot Capture(string keyPath, string valueName)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, false))
                {
                    if (key == null || !ContainsValueName(key, valueName))
                    {
                        return new RegistryValueSnapshot(
                            keyPath,
                            valueName,
                            false,
                            null,
                            RegistryValueKind.String);
                    }

                    object value = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                    RegistryValueKind kind = key.GetValueKind(valueName);
                    return new RegistryValueSnapshot(keyPath, valueName, true, value, kind);
                }
            }

            internal void Restore()
            {
                if (ValueExisted)
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                    {
                        if (key == null)
                        {
                            throw new InvalidOperationException(
                                "The previous startup registration could not be restored.");
                        }

                        key.SetValue(ValueName, Value, ValueKind);
                    }
                }
                else
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
                    {
                        if (key != null) key.DeleteValue(ValueName, false);
                    }
                }
            }

            private static bool ContainsValueName(RegistryKey key, string valueName)
            {
                string[] names = key.GetValueNames();
                int index;
                for (index = 0; index < names.Length; index++)
                {
                    if (string.Equals(names[index], valueName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class RegistryKeySnapshot
        {
            private RegistryKeySnapshot(string keyPath, bool existed, RegistryKeyNode rootNode)
            {
                KeyPath = keyPath;
                Existed = existed;
                RootNode = rootNode;
            }

            private readonly string KeyPath;
            private readonly bool Existed;
            private readonly RegistryKeyNode RootNode;

            internal static RegistryKeySnapshot Capture(string keyPath)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, false))
                {
                    return key == null
                        ? new RegistryKeySnapshot(keyPath, false, null)
                        : new RegistryKeySnapshot(keyPath, true, RegistryKeyNode.Capture(key));
                }
            }

            internal void Restore()
            {
                Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
                if (!Existed) return;

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException(
                            "The previous uninstall registration could not be restored.");
                    }

                    RootNode.RestoreTo(key);
                }
            }
        }

        private sealed class RegistryKeyNode
        {
            private readonly List<RegistryStoredValue> values = new List<RegistryStoredValue>();
            private readonly List<RegistryStoredSubKey> subKeys = new List<RegistryStoredSubKey>();

            internal static RegistryKeyNode Capture(RegistryKey key)
            {
                RegistryKeyNode node = new RegistryKeyNode();
                string[] valueNames = key.GetValueNames();
                int index;

                for (index = 0; index < valueNames.Length; index++)
                {
                    string valueName = valueNames[index];
                    node.values.Add(new RegistryStoredValue(
                        valueName,
                        key.GetValue(
                            valueName,
                            null,
                            RegistryValueOptions.DoNotExpandEnvironmentNames),
                        key.GetValueKind(valueName)));
                }

                string[] subKeyNames = key.GetSubKeyNames();
                for (index = 0; index < subKeyNames.Length; index++)
                {
                    using (RegistryKey subKey = key.OpenSubKey(subKeyNames[index], false))
                    {
                        if (subKey != null)
                        {
                            node.subKeys.Add(new RegistryStoredSubKey(
                                subKeyNames[index],
                                Capture(subKey)));
                        }
                    }
                }

                return node;
            }

            internal void RestoreTo(RegistryKey key)
            {
                int index;
                for (index = 0; index < values.Count; index++)
                {
                    RegistryStoredValue value = values[index];
                    key.SetValue(value.Name, value.Value, value.Kind);
                }

                for (index = 0; index < subKeys.Count; index++)
                {
                    RegistryStoredSubKey subKey = subKeys[index];
                    using (RegistryKey restoredSubKey = key.CreateSubKey(subKey.Name))
                    {
                        if (restoredSubKey == null)
                        {
                            throw new InvalidOperationException(
                                "A previous uninstall registry subkey could not be restored.");
                        }

                        subKey.Node.RestoreTo(restoredSubKey);
                    }
                }
            }
        }

        private sealed class RegistryStoredValue
        {
            internal RegistryStoredValue(string name, object value, RegistryValueKind kind)
            {
                Name = name;
                Value = value;
                Kind = kind;
            }

            internal readonly string Name;
            internal readonly object Value;
            internal readonly RegistryValueKind Kind;
        }

        private sealed class RegistryStoredSubKey
        {
            internal RegistryStoredSubKey(string name, RegistryKeyNode node)
            {
                Name = name;
                Node = node;
            }

            internal readonly string Name;
            internal readonly RegistryKeyNode Node;
        }

        private static void DeleteFileQuietly(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void DeleteDirectoryQuietly(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private static void DeleteDirectoryIfEmptyQuietly(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                {
                    Directory.Delete(path, false);
                }
            }
            catch
            {
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private static void Report(Action<int, string> reportProgress, int percentage, string message)
        {
            if (reportProgress != null)
            {
                reportProgress(percentage, message);
            }
        }
    }
}
