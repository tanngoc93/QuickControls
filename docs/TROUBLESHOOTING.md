# Quick Controls Troubleshooting

The redesigned Settings guidance below describes the current `main` branch. A stable release that predates the [Unreleased](../CHANGELOG.md#unreleased) changes may use an older single-page Settings layout.

## A keyboard shortcut does not work

Another application may already own the same global shortcut.

1. Open Quick Controls from the system tray.
2. Select **Settings > Keyboard shortcuts** in the flat dark sidebar.
3. Select the shortcut field for the affected action.
4. Hold `Ctrl`, `Alt`, `Shift`, or the Windows key and press another key.
5. Confirm the combination shown as separate keycaps, then select **Save changes**.

Each shortcut must include `Ctrl`, `Alt`, `Shift`, or the Windows key, and no two actions can use the same combination. Shortcuts may not reach Quick Controls while a higher-privilege application or some exclusive full-screen software has focus.

## A shortcut field keeps asking for a modifier

A letter, number, arrow, or function key cannot be registered by itself. Select the field, hold at least one modifier (`Ctrl`, `Alt`, `Shift`, or the Windows key), and then press the second key. The finished combination appears as keycaps in the field.

If **Save changes** reports a duplicate, choose a different combination for one of the two actions. Restoring defaults repopulates the fields but does not apply them until you save.

## I cannot find an option in Settings

The redesigned Settings window separates options into three entries in the flat dark sidebar:

- **Interface** contains language, the four direct panel-layout tiles, and Edge Dock position.
- **Keyboard shortcuts** contains the six keycap capture fields.
- **General** contains adjustment amount, modern toggle rows, and panel-position reset.

The custom title bar can be dragged to move Settings. The close button, **Cancel**, and `Esc` close the window without applying candidate changes; use **Save changes** when you want the selected values to take effect.

## The panel is missing

- Press `Ctrl + Alt + Space`.
- Select the Quick Controls icon near the Windows clock.
- Check the Windows hidden-icons menu.
- Open **Quick Controls** from the Start menu.

Closing the panel hides it to the system tray. To stop the app completely, right-click its tray icon and select **Exit**.

If **Edge Dock** is selected, look for the narrow Quick Controls tab on the left or right side of the current display. Select the tab to open the Vertical Mini controls. You can also right-click the tray icon and choose a different option under **Panel layout**.

If the panel was moved off-screen after a monitor was disconnected, open **Settings** from the tray menu, select **General**, choose **Reset panel position**, and save the changes.

## Edge Dock covers part of another window

Edge Dock is a small overlay. It does not reserve Windows desktop work area, so a maximized window can extend behind it. This behavior avoids changing the taskbar or the way other applications are arranged.

Choose one of these options:

- Move Edge Dock to the opposite side under **Settings > Interface > Dock edge**.
- Turn off **Keep the panel above other windows** under **Settings > General**.
- Choose **Horizontal Mini** or **Vertical Mini** under **Panel layout**.
- Hide Quick Controls to the system tray and use the keyboard shortcuts.

## The app is using the wrong language

Open **Settings > Interface**, choose English, Vietnamese, Japanese, Simplified Chinese, or French under **Language**, then select **Save changes**. The interface changes after the settings are saved.

If you cannot read the current labels, open Settings from the tray menu, select the first page in the left navigation, use the first selection box on that page, and then select the highlighted save button in the lower-right corner.

If the saved language code is missing or unsupported, Quick Controls falls back to English. See the [localization guide](LOCALIZATION.md) when diagnosing a developer build with missing or mismatched translations.

## No audio output device was found

Make sure Windows has an active default audio output device. Connect or enable speakers, headphones, or a display audio device, then reopen the panel. Quick Controls controls the master volume of the current default Windows output; it does not control individual app volumes.

## Brightness is unavailable

For a built-in display, first confirm that the Windows brightness slider works. For an external monitor, enable DDC/CI in the monitor menu and try a direct HDMI or DisplayPort connection.

Read [Display brightness compatibility](DISPLAY-COMPATIBILITY.md) for supported paths and common dock, hub, adapter, TV, and DisplayLink limitations.

## Hardware Monitor shows Not reported

**Not reported** means Windows and the installed device driver did not supply a usable value. It is a supported state, not a warning that the hardware is damaged or overheating.

- CPU temperature is expected to be unavailable on many computers because standard Windows APIs do not provide a dependable CPU package or die temperature.
- GPU temperature appears only when the installed Windows graphics driver exposes compatible telemetry.
- Storage temperature appears only when the drive, storage controller, and Windows driver expose compatible telemetry.

The CPU, GPU, memory, and storage activity graphs continue with the readings that are available. A real `0%` reading is shown as zero; it is not replaced with **Not reported**. Quick Controls does not request administrator access or install a sensor driver to force unavailable readings.

If a GPU or storage temperature used to appear, install the current driver offered by Windows Update or the computer/device manufacturer, restart Windows, and open Hardware Monitor again. Avoid installing an unrelated third-party sensor driver only to fill a missing value.

## Hardware graphs are empty or paused

Leave Hardware Monitor open for a few seconds. Initial device and performance-counter discovery can take longer than later samples; after warm-up, the rolling line fills gradually until it contains the latest 60 seconds.

Sampling runs only while the Hardware Monitor window is open. Closing it intentionally stops the background sampling worker and clears that window's in-memory history; reopening it starts a new graph. If the window is open but every activity graph remains unavailable, close and reopen it, restart Windows, and update the relevant Windows device drivers before filing a bug report.

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
