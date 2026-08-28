# Quick Controls User Guide

Quick Controls gives Windows 10 and Windows 11 users fast access to system volume and display brightness. It is especially useful when a keyboard has no media keys. You can use global keyboard shortcuts, choose one of four on-screen panel layouts, or keep the app in the system tray.

> This guide describes the current `main` branch. Features in [Unreleased](../CHANGELOG.md#unreleased), including the redesigned Settings window and new panel modes, may not be present in the latest stable installer yet. Check the release notes for the version you download.

## Install Quick Controls

1. Download `QuickControls-Setup.exe` from the [latest GitHub release](https://github.com/tanngoc93/QuickControls/releases/latest).
2. Open the downloaded file.
3. Select **Install now**.
4. Quick Controls opens after installation and appears in the system tray near the Windows clock.

The installer is per-user and does not request administrator access. It creates Desktop and Start menu shortcuts and installs the app under `%LOCALAPPDATA%\Programs\QuickControls`.

> The current installer is unsigned. Read [Windows security warnings](TROUBLESHOOTING.md#windows-smartscreen-warns-about-the-installer) before continuing if Windows displays a warning.

## Use the control panel

The Full panel contains two clear control sections:

- **Volume:** drag the slider, select **Quieter** or **Louder**, or select **Mute**.
- **Brightness:** drag the slider or select **Dimmer** or **Brighter**.

Use the layout button in the title bar to switch panel shape. Use the expand button in a mini layout to return to the Full panel. Use the close button to hide the panel in the system tray; the app keeps running and its keyboard shortcuts still work.

Quick Controls uses a sharp, high-contrast visual style with clear text, compact corners, visible borders, and vivid volume and brightness accents. Buttons and sliders are designed to remain easy to identify at a glance.

## Choose a layout for your screen

Open **Settings > Interface**, select one of the four tiles under **Panel layout**, and select **Save changes**. The choices are shown together in a direct `2 x 2` grid, so you can compare their shapes without opening another menu.

| Layout | Best for |
| --- | --- |
| **Full panel** | Sliders, display selection, and access to all controls. |
| **Horizontal Mini** | A low control bar above the taskbar or beneath another window. |
| **Vertical Mini** | A narrow control strip beside a window or on a portrait display. |
| **Edge Dock** | A small tab that stays at the left or right edge until you need it. |

You can also choose a layout immediately from the Full panel's layout button or from **Panel layout** in the system tray menu.

If **Switch to a mini view when not in use** is enabled, the Full panel changes to Horizontal Mini after six seconds without interaction. A panel temporarily expanded from another preferred layout returns to that selected layout. Moving the pointer over the panel or using one of its controls keeps it open.

### Use Edge Dock

Choose **Edge Dock** when you want the smallest always-available panel:

1. Open **Settings > Interface**.
2. Select the **Edge Dock** layout tile.
3. Under **Dock edge**, choose **Automatic**, **Left**, or **Right**.
4. Select **Save changes**.
5. Select the edge tab to open the Vertical Mini controls. Use the expand button when you need the Full panel.

**Automatic** uses the left or right edge nearest the saved panel position. The edge tab is a safe overlay and does not reserve Windows desktop work area, replace the taskbar, or change how windows maximize. A maximized window can extend behind it. Choose **Keep the panel above other windows** if the tab must remain visible, or hide the tab to the system tray when you do not need it.

When Windows startup is enabled, a saved Edge Dock tab returns at sign-in without taking keyboard focus from the app you are using.

Press `Esc` while a panel opened from Edge Dock is active to return to the edge tab. When Edge Dock is already visible, the show-or-hide shortcut opens its Vertical Mini controls.

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

If another program already uses one of these combinations, open **Settings > Keyboard shortcuts** and record a different shortcut.

### Record a custom shortcut

1. Select the shortcut field beside the action you want to change.
2. Hold at least one modifier key: `Ctrl`, `Alt`, `Shift`, or the Windows key.
3. While holding the modifier, press the other key in the combination.
4. Check the separate keycaps displayed in the field.
5. Select **Save changes**.

Quick Controls reports invalid combinations and prevents two actions from using the same shortcut. If you select a field but do not enter a new valid combination, its existing saved combination remains in place.

## Customize Quick Controls

Open the Full panel and select **Settings**, or open Settings from the system tray menu. The redesigned window uses a compact custom title bar and a flat dark sidebar, with options separated into three pages instead of one long form.

You can:

- Choose the interface language.
- Choose the Full, Horizontal Mini, Vertical Mini, or Edge Dock layout.
- Choose the automatic, left, or right edge for Edge Dock.
- Record a different key combination for each action.
- Change each volume or brightness adjustment to `2%`, `5%`, or `10%`.
- Open Quick Controls when you sign in to Windows.
- Keep the panel above other windows.
- Automatically switch back to a mini layout when the panel is not in use.
- Reset the panel to its recommended position if it was moved off-screen.

Each shortcut must include at least one modifier key: `Ctrl`, `Alt`, `Shift`, or the Windows key. Every action must use a different combination.

Settings are divided into three simple sidebar pages:

- **Interface:** the language selector, direct `2 x 2` panel-layout tiles, and Edge Dock position.
- **Keyboard shortcuts:** six capture fields that show saved combinations as individual keycaps.
- **General:** adjustment amount, modern toggle rows for Windows startup, always-on-top behavior, automatic mini mode, and the panel-position reset.

Select a page in the dark sidebar to change sections. Select **Save changes** to apply the candidate settings, **Cancel** to close without applying them, or **Restore defaults** to repopulate the form with the recommended values before saving. Drag the custom title bar to move the window, or use its close button to cancel.

## Change the interface language

Quick Controls supports English, Vietnamese, Japanese, Simplified Chinese, and French.

1. Open **Settings**.
2. Select **Interface** in the left navigation.
3. Choose a language from **Language**.
4. Select **Save changes**.

The app interface changes after saving. The selected language applies to the panel, Settings, system tray menu, notifications, and on-screen volume or brightness feedback. Quick Controls remembers the choice for the next time it starts.

Language names are displayed in their native form. English is the fallback if a saved language code is unsupported or a localized string cannot be resolved. Developers and translators can read the [localization guide](LOCALIZATION.md) for catalog and verification details.

## Control more than one display

When Quick Controls finds multiple brightness-capable displays, use the display selector in the Brightness card to choose which display to control. Your selection is saved.

Laptop panels normally use Windows WMI brightness controls. Compatible external monitors use DDC/CI. See [Display compatibility](DISPLAY-COMPATIBILITY.md) for setup details and limitations.

## Use the system tray menu

Right-click the Quick Controls icon near the Windows clock to open its menu. The tray menu lets you:

- Open the panel.
- Mute or unmute audio.
- Switch directly between all four panel layouts.
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
