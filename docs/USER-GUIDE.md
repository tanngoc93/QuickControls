# Quick Controls User Guide

Quick Controls gives Windows 10 and Windows 11 users fast access to system volume and display brightness. You can use global keyboard shortcuts, an expanded floating panel, or a compact panel that stays out of the way.

## Install Quick Controls

1. Download `QuickControls-Setup.exe` from the [latest GitHub release](https://github.com/tanngoc93/QuickControls/releases/latest).
2. Open the downloaded file.
3. Select **Install now**.
4. Quick Controls opens after installation and appears in the system tray near the Windows clock.

The installer is per-user and does not request administrator access. It creates Desktop and Start menu shortcuts and installs the app under `%LOCALAPPDATA%\Programs\QuickControls`.

> The current installer is unsigned. Read [Windows security warnings](TROUBLESHOOTING.md#windows-smartscreen-warns-about-the-installer) before continuing if Windows displays a warning.

## Use the floating panel

The expanded panel contains two simple cards:

- **Volume:** drag the slider, select **Quieter** or **Louder**, or select **Mute**.
- **Brightness:** drag the slider or select **Dimmer** or **Brighter**.

Select the minus button in the title bar to switch to the compact panel. Select the arrow in the compact panel to expand it again. Select the close button to hide the panel in the system tray; the app keeps running so its shortcuts still work.

If automatic compact mode is enabled, the expanded panel switches to compact mode after six seconds without interaction.

## Default keyboard shortcuts

The shortcuts work globally while another normal desktop app is focused.

| Shortcut | Action |
| --- | --- |
| `Ctrl + Alt + Up` | Increase volume |
| `Ctrl + Alt + Down` | Decrease volume |
| `Ctrl + Alt + Right` | Increase brightness |
| `Ctrl + Alt + Left` | Decrease brightness |
| `Ctrl + Alt + M` | Mute or unmute |
| `Ctrl + Alt + Space` | Show or hide the panel |

If another program already uses one of these combinations, open **Settings** and record a different shortcut.

## Customize Quick Controls

Open the panel and select **Settings**. You can:

- Record a different key combination for each action.
- Change each volume or brightness adjustment to `2%`, `5%`, or `10%`.
- Open Quick Controls when you sign in to Windows.
- Keep the panel above other windows.
- Automatically switch to compact mode when the panel is not in use.

Each shortcut must include at least one modifier key: `Ctrl`, `Alt`, `Shift`, or the Windows key. Every action must use a different combination.

## Control more than one display

When Quick Controls finds multiple brightness-capable displays, use the display selector in the Brightness card to choose which display to control. Your selection is saved.

Laptop panels normally use Windows WMI brightness controls. Compatible external monitors use DDC/CI. See [Display compatibility](DISPLAY-COMPATIBILITY.md) for setup details and limitations.

## Use the system tray menu

Right-click the Quick Controls icon near the Windows clock to open its menu. The tray menu lets you:

- Open the panel.
- Mute or unmute audio.
- Open Settings or About.
- Turn **Start with Windows** on or off.
- Exit the app completely.

If the icon is not visible, select the Windows hidden-icons arrow in the notification area.

## Update or reinstall

Download a newer `QuickControls-Setup.exe` and run it. The installer detects the existing installation and shows **Reinstall / update**. Saved shortcuts and preferences are preserved.

## Uninstall

1. Open **Windows Settings**.
2. Go to **Apps > Installed apps**.
3. Find **Quick Controls** and select **Uninstall**.
4. Optionally select **Also delete saved shortcuts and settings**.

Settings are stored at `%LOCALAPPDATA%\QuickControls\settings.xml` and remain after uninstall unless you choose to delete them.
