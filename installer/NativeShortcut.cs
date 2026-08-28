using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace QuickControls.Installer
{
    internal static class NativeShortcut
    {
        internal static void Create(
            string shortcutPath,
            string targetPath,
            string workingDirectory,
            string description)
        {
            IShellLinkW shortcut = null;

            try
            {
                string shortcutDirectory = Path.GetDirectoryName(shortcutPath);
                if (!string.IsNullOrEmpty(shortcutDirectory))
                {
                    Directory.CreateDirectory(shortcutDirectory);
                }

                shortcut = (IShellLinkW)new ShellLink();
                shortcut.SetPath(targetPath);
                shortcut.SetWorkingDirectory(workingDirectory);
                shortcut.SetDescription(description);
                shortcut.SetIconLocation(targetPath, 0);
                shortcut.SetShowCmd(1);

                IPersistFile persistFile = (IPersistFile)shortcut;
                persistFile.Save(shortcutPath, false);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath(
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,
                int maximumPath,
                IntPtr findData,
                uint flags);

            void GetIDList(out IntPtr itemIdentifierList);
            void SetIDList(IntPtr itemIdentifierList);

            void GetDescription(
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description,
                int maximumName);

            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);

            void GetWorkingDirectory(
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,
                int maximumPath);

            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

            void GetArguments(
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,
                int maximumPath);

            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int showCommand);
            void SetShowCmd(int showCommand);

            void GetIconLocation(
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath,
                int iconPathLength,
                out int iconIndex);

            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
            void Resolve(IntPtr windowHandle, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
        }
    }
}
