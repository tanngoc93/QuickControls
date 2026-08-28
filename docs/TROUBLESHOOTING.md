# Quick Controls Troubleshooting

## A keyboard shortcut does not work

Another application may already own the same global shortcut.

1. Open Quick Controls from the system tray.
2. Select **Settings**.
3. Select the shortcut field for the affected action.
4. Press a different key combination and select **Save changes**.

Each shortcut must include `Ctrl`, `Alt`, `Shift`, or the Windows key, and no two actions can use the same combination. Shortcuts may not reach Quick Controls while a higher-privilege application or some exclusive full-screen software has focus.

## The panel is missing

- Press `Ctrl + Alt + Space`.
- Select the Quick Controls icon near the Windows clock.
- Check the Windows hidden-icons menu.
- Open **Quick Controls** from the Start menu.

Closing the panel hides it to the system tray. To stop the app completely, right-click its tray icon and select **Exit**.

## No audio output device was found

Make sure Windows has an active default audio output device. Connect or enable speakers, headphones, or a display audio device, then reopen the panel. Quick Controls controls the master volume of the current default Windows output; it does not control individual app volumes.

## Brightness is unavailable

For a built-in display, first confirm that the Windows brightness slider works. For an external monitor, enable DDC/CI in the monitor menu and try a direct HDMI or DisplayPort connection.

Read [Display brightness compatibility](DISPLAY-COMPATIBILITY.md) for supported paths and common dock, hub, adapter, TV, and DisplayLink limitations.

## Windows SmartScreen warns about the installer

The current installer does not have a commercial code-signing certificate. Windows SmartScreen may show **Windows protected your PC** because the publisher cannot be verified.

Only continue if you downloaded the file from this repository's official Releases page and verified that it is the file you intended to run. Select **More info > Run anyway** if that option is available. Never bypass a warning for a file from an unknown source.

## Application Control blocks the file

The message **An Application Control policy has blocked this file** is different from SmartScreen. A managed computer may require software signed by a certificate trusted by its organization. There may be no **Run anyway** option.

Do not try to bypass the policy. Ask the computer administrator to approve the app or use a build signed with an accepted certificate.

## Quick Controls does not start with Windows

Open **Settings** and enable **Open Quick Controls when I sign in to Windows**. You can also right-click the tray icon and toggle **Start with Windows**.

The startup entry is stored for the current user under the standard Windows `Run` registry key. Security software or an organization policy can prevent that entry from being created.

## Reset all saved settings

Use **Restore defaults** in Settings, then select **Save changes**. If the settings file is damaged, Quick Controls automatically falls back to defaults.

To remove settings manually:

1. Exit Quick Controls from its tray menu.
2. Delete `%LOCALAPPDATA%\QuickControls\settings.xml`.
3. Start Quick Controls again.

## The installer fails

Close Quick Controls and run the installer again. Installer errors are written to `%TEMP%\QuickControls-Installer.log`. Include that log and your Windows version when opening a bug report.
