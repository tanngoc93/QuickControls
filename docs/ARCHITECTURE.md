# Quick Controls Architecture

Quick Controls is intentionally small and uses Windows APIs directly. It has no server component, online account, analytics SDK, or update service.

## Runtime structure

```text
Program
  -> AppController
     -> AudioService
     -> BrightnessService
     -> HotkeyManager
     -> SettingsStore / StartupService
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

The UI uses C# Windows Forms with custom-drawn controls for consistent rounded cards, buttons, sliders, icons, focus states, and DPI scaling. The main form has expanded and compact states. `OsdForm` displays temporary volume or brightness feedback after a hotkey action.

## Local settings and startup

`SettingsStore` serializes settings to `%LOCALAPPDATA%\QuickControls\settings.xml` with a temporary-file replacement flow. `StartupService` manages a per-user entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Settings and hardware commands are handled locally. The application source contains no network client or telemetry integration.

## Installer

The installer embeds the app executable and configuration file, installs under `%LOCALAPPDATA%\Programs\QuickControls`, creates Desktop and Start menu shortcuts, and registers an uninstaller for Windows Installed Apps. Installation runs as the current user without an elevation request.

The installer uses a transaction and rollback snapshots for file, shortcut, startup, and uninstall-registration changes. A cleanup helper removes the running uninstaller after removal completes.
