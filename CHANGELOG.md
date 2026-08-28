# Changelog

All notable user-facing changes to Quick Controls are documented here.

## [Unreleased]

### Added

- Runtime interface languages: English, Vietnamese, Japanese, Simplified Chinese, and French.
- Four panel layouts for different workflows: Full, Horizontal Mini, Vertical Mini, and left/right Edge Dock.
- Direct volume, mute, and brightness actions in both mini layouts.
- Panel layout selection from Settings, the Full panel, and the system tray menu.
- A one-click panel-position reset for recovering a panel after monitor changes.

### Changed

- Rebuilt the visual system with sharper corners, stronger borders, cobalt volume controls, and amber brightness controls.
- Rebuilt Settings around a custom title bar and flat dark sidebar with separate Interface, Keyboard shortcuts, and General pages.
- Replaced the panel-layout dropdown with direct `2 x 2` visual tiles for Full, Horizontal Mini, Vertical Mini, and Edge Dock.
- Added keycap-style shortcut capture and modern toggle rows for startup, always-on-top, and automatic mini behavior.
- Improved DPI handling, screen-edge placement, localized fonts, accessibility names, and UI preview validation.
- Edge Dock now restores its screen-edge tab without stealing focus when Quick Controls starts with Windows.

## [1.0.0] - 2026-08-28

### Added

- Custom global shortcuts for volume, brightness, mute, and panel visibility.
- Master volume and mute control through the Windows Core Audio API.
- Built-in display brightness through Windows WMI.
- Compatible external monitor brightness through DDC/CI.
- Expanded and compact floating control panels.
- Configurable adjustment steps, startup behavior, always-on-top mode, and automatic compact mode.
- System tray menu and on-screen feedback.
- Per-user one-click installer, portable package, uninstall support, and optional code signing.
- Automated build, UI rendering, artifact validation, and hardware inspection scripts.
- English user guide, compatibility guide, troubleshooting guide, build guide, architecture notes, and screenshots.
