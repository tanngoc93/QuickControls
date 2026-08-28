# Quick Controls Architecture

Quick Controls is intentionally small and uses Windows APIs directly. It has no server component, online account, analytics SDK, or update service.

## Runtime structure

```text
Program
  -> AppController
     -> AudioService
     -> BrightnessService
     -> HotkeyManager
     -> SettingsStore / StartupService / AppText
     -> PanelForm / SettingsForm / OsdForm / TrayService
```

`Program` enforces a single running instance and uses named Windows events to show or close the existing instance. `AppController` owns application state and coordinates the services and user interface.

## Audio control

`AudioService` calls the Windows Core Audio endpoint interfaces to read and change the master volume of the default output device and to toggle mute.

## Brightness control

`BrightnessService` discovers two device types:

- Active `WmiMonitorBrightness` devices for built-in laptop or all-in-one displays.
- Physical monitors exposed by the Windows monitor configuration API for DDC/CI-capable external displays.

Each display implements the same `BrightnessDevice` abstraction so the UI can list, read, and update detected displays consistently.

## Global keyboard shortcuts

`HotkeyManager` uses the Win32 `RegisterHotKey` API. Six actions are mapped to configurable key combinations. Shortcut registration failures are surfaced to the user so conflicting combinations can be changed.

## User interface

The UI uses C# Windows Forms with custom-drawn controls for a sharp, high-contrast visual system. Cards and buttons use compact corner radii, crisp borders, vivid accent colors, clear focus states, and DPI-aware sizing. The controls are code-native; the application does not depend on a web renderer or a bundled front-end framework. `OsdForm` displays temporary volume or brightness feedback after a hotkey action.

### Settings window composition

`SettingsForm` uses a borderless form and draws its own compact title bar, close action, footer, and window outline. The title bar supports dragging through the normal Win32 non-client move message. A flat dark sidebar switches among Interface, Keyboard shortcuts, and General pages while keeping the save actions in a stable footer.

The redesigned Settings pages use dedicated controls instead of native WinForms checkboxes and combo boxes:

- `LayoutOptionButton` draws the four panel shapes in a direct `2 x 2` tile selector and exposes selection state to accessibility clients.
- `HotkeyTextBox` captures a modified key combination and draws each saved token as a keycap, with focused and invalid states.
- `ModernToggle` presents startup, always-on-top, and automatic mini behavior as keyboard-operable toggle rows.
- `ModernChoiceBox` provides code-native language, dock-edge, and adjustment-step selection.

Settings edits are made against a cloned `AppSettings` candidate. **Save changes** validates duplicate and invalid shortcuts before the controller applies the candidate; cancelling the window leaves the active settings unchanged.

`PanelForm` contains four code-native layouts that share the same audio and brightness state:

- Full panel
- Horizontal Mini
- Vertical Mini
- Edge Dock

Layout changes reuse one form instead of opening independent controller windows. `PanelPlacement` clamps floating layouts to the selected display's working area and anchors Edge Dock to the left or right working-area boundary. Edge Dock is an ordinary top-level overlay; Quick Controls does not register it as a Windows AppBar and does not reserve shell work area.

The panel layout menu and system tray menu expose the same four layout choices. With automatic mini mode enabled, Full collapses to Horizontal Mini; a temporary expanded view opened from another preferred layout returns to that saved layout.

## Runtime localization

`AppText` provides built-in dictionaries for English, Vietnamese, Japanese, Simplified Chinese, and French. English is the fallback when a key or language code is unavailable. User-visible forms, tray commands, notifications, accessibility names, display labels, and on-screen feedback resolve text at runtime.

The selected language code is stored with the other local preferences and is applied before the controller creates the main UI. Settings applies a language change after the user saves. Font selection prefers Windows UI font families appropriate for Latin, Japanese, and Simplified Chinese text while retaining installed-font fallbacks.

Catalog invariants, format placeholders, font choices, and the workflow for adding another language are documented in the [localization guide](LOCALIZATION.md).

## Local settings and startup

`SettingsStore` serializes settings to `%LOCALAPPDATA%\QuickControls\settings.xml` with a temporary-file replacement flow. Stored preferences include the language, preferred panel layout, left/right dock choice, panel position, keyboard shortcuts, step size, startup behavior, and display selection. The settings version supports migration from the earlier expanded/compact preference. `StartupService` manages a per-user entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Settings and hardware commands are handled locally. The application source contains no network client or telemetry integration.

## Installer

The installer embeds the app executable and configuration file, installs under `%LOCALAPPDATA%\Programs\QuickControls`, creates Desktop and Start menu shortcuts, and registers an uninstaller for Windows Installed Apps. Installation runs as the current user without an elevation request.

The installer uses a transaction and rollback snapshots for file, shortcut, startup, and uninstall-registration changes. A cleanup helper removes the running uninstaller after removal completes.
