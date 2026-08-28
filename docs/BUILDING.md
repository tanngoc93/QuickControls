# Build Quick Controls from Source

Quick Controls is a C# Windows Forms application targeting .NET Framework 4.0. Its PowerShell build uses the C# compiler included with Windows, so Visual Studio, the modern .NET SDK, and Inno Setup are not required.

## Requirements

- Windows 10 or Windows 11.
- Windows PowerShell 5.1.
- The Windows .NET Framework 4 compiler at `%WINDIR%\Microsoft.NET\Framework` or `Framework64`.
- Git, if you are cloning the repository.

## Clone and build

```powershell
git clone https://github.com/tanngoc93/QuickControls.git
Set-Location QuickControls
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

The build creates these generated files under `artifacts`:

- `QuickControls.exe` and its configuration file.
- `QuickControls-Setup.exe`, the one-click per-user installer.
- `QuickControls-Portable.zip`.
- The application icon.
- Full-panel, Horizontal Mini, Vertical Mini, Edge Dock, redesigned Settings, Hardware Monitor, and uninstaller UI previews.

The preview files include:

- `QuickControls-Preview.png` — Full panel.
- `QuickControls-Compact-Preview.png` — Horizontal Mini.
- `QuickControls-Vertical-Preview.png` — Vertical Mini.
- `QuickControls-Edge-Preview.png` — Edge Dock.
- `QuickControls-Settings-Preview.png` — Interface page with the direct `2 x 2` layout tiles.
- `QuickControls-Shortcuts-Preview.png` — Keyboard shortcuts page with keycap fields.
- `QuickControls-Hardware-Monitor-Preview.png` — CPU, GPU, memory, storage, and optional temperature graphs at 100% scaling.
- `QuickControls-Uninstaller-Preview.png` — ready-state uninstaller dialog at 100% scaling.
- `QuickControls-Uninstaller-150-Preview.png` — simulated 150% content-and-font scaling regression preview.

Use `-SkipPreview` when preview images are not needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 -SkipPreview
```

## Run automated checks

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

The check validates required artifact sizes, loads the application assembly, renders all four panel layouts and the redesigned Settings pages, verifies expected image dimensions, validates the five-language text catalog, checks the installer executable header, and reports Authenticode signature status. It also renders localized Interface, Keyboard shortcuts, General, Full-panel, and Hardware Monitor previews for English, Vietnamese, Japanese, Simplified Chinese, and French to catch clipping and missing-font regressions. Hardware Monitor receives an additional simulated 150% preview, a runtime-language lifecycle check reuses and repaints one window while verifying translated text, culture-specific time formatting, font selection, accessibility text, and the absence of paint-error glyphs, and a working-area lifecycle check applies small and large synthetic work areas without moving onto another physical display to catch off-screen placement or cumulative scaling. A visibility lifecycle check exercises user close, hidden reuse, reopen, and final application-exit disposal. A real `ModernChoiceBox` lifecycle check opens a test instance of the selector used by language, dock-edge, and adjustment settings; chooses an item; verifies the menu closes without being disposed mid-click; reopens the same menu; and confirms owner disposal. The tray menu is reapplied in every supported language before disposal. Uninstaller checks exercise ready, working, error, and completed states and verify that every visible control stays inside its parent with simulated 100%, 125%, 150%, 175%, or 200% content-and-font scaling. Preview and lifecycle forms cannot activate uninstall actions or modify hardware levels. Native title-bar and checkbox chrome still require a final visual check on a real high-DPI Windows desktop.

Use `-RequireSigned` for a release build that must have valid signatures:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -RequireSigned
```

## Verify localization changes

All runtime strings are compiled into `src\QuickControls\Services\AppText.cs`. When a text key or translation changes, run the complete test script. `AppText.ValidateCatalog()` requires every supported language to contain the same keys as English and to preserve numbered format placeholders such as `{0}`.

Review the generated localized previews as well as the automated result. A catalog can be structurally valid while a translation is still too long for a control or unclear in context. See the [Quick Controls localization guide](LOCALIZATION.md) for language codes, translation rules, font fallbacks, and the checklist for adding a language.

## Check hardware integration

The hardware check reads the current audio and brightness state without changing it. It does not require temperature sensors to be available:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hardware-check.ps1
```

Add `-VerifyWrites` to write the current values back unchanged and verify the write paths:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hardware-check.ps1 -VerifyWrites
```

## Validate Hardware Monitor changes

Hardware Monitor uses live values from the computer running the build, so temperature availability is not a portable pass/fail condition. After building, open Hardware Monitor and verify these behaviors manually:

- CPU, GPU, memory, and storage cards remain usable when one or more readings are unavailable.
- A new point is added about once per second and each graph retains no more than the latest 60 seconds.
- Closing the Hardware Monitor window stops sampling; reopening it starts a fresh in-memory history.
- Missing CPU, GPU, or storage temperature is displayed as **Not reported**, not as `0 degrees` or an invented estimate.
- CPU temperature is not required because standard Windows APIs do not provide a dependable cross-device reading.
- Supported GPU and storage temperatures may appear when the installed Windows drivers expose them.
- The feature works from a normal user account without an administrator prompt.

Test both a computer that reports optional temperature telemetry and a computer or virtual machine that does not when those environments are available. Do not make automated tests depend on a particular sensor name or temperature value.

The public README screenshot is maintained at `docs/images/quick-controls-hardware-monitor.png`. When regenerating it, use representative activity values, keep any unavailable temperature labeled **Not reported**, and confirm the Markdown image reference before committing the image.

## Sign release binaries

Pass an accessible code-signing certificate thumbprint to sign both the app and installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 `
  -CertificateThumbprint "CERTIFICATE_THUMBPRINT" `
  -TimestampServer "TIMESTAMP_SERVER_URL"
```

An ordinary self-signed certificate usually does not satisfy Enterprise Application Control policies. Use a certificate trusted by the target environment.

## Repository hygiene

Do not commit `artifacts`, `bin`, or `obj`. Attach the installer and portable ZIP to a GitHub Release instead of storing generated binaries in Git history.
