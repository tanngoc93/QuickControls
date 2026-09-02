using System;
using System.Reflection;
using Microsoft.Win32;

namespace QuickControls.Services
{
    public static class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "QuickControls";

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = enabled
                    ? Registry.CurrentUser.CreateSubKey(RunKeyPath)
                    : Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        return !enabled;
                    }

                    if (enabled)
                    {
                        string executable = Assembly.GetExecutingAssembly().Location;
                        key.SetValue(ValueName, BuildStartupCommand(executable), RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }

                    // Persist the change before returning so a sudden loss of power cannot
                    // leave the user's saved preference without a matching startup entry.
                    key.Flush();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string actual = Convert.ToString(key.GetValue(
                        ValueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames));
                    string executable = Assembly.GetExecutingAssembly().Location;
                    return IsRegisteredCommand(actual, executable);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string BuildStartupCommand(string executable)
        {
            return "\"" + executable + "\" --startup";
        }

        private static bool IsRegisteredCommand(string actual, string executable)
        {
            if (string.Equals(actual, BuildStartupCommand(executable), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Treat the old hidden-start command as enabled while the application upgrades
            // it. This preserves the user's preference across an in-place update.
            string legacyCommand = "\"" + executable + "\" --background";
            return string.Equals(actual, legacyCommand, StringComparison.OrdinalIgnoreCase);
        }
    }
}
