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
                        key.SetValue(ValueName, "\"" + executable + "\" --background", RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
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
                    string expected = "\"" + executable + "\" --background";
                    return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
