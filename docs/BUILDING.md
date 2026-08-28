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
- Main-panel, compact-panel, and Settings UI previews.

Use `-SkipPreview` when preview images are not needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 -SkipPreview
```

## Run automated checks

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

The check validates required artifact sizes, loads the application assembly, renders all three UI states, verifies expected image dimensions, checks the installer executable header, and reports Authenticode signature status.

Use `-RequireSigned` for a release build that must have valid signatures:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -RequireSigned
```

## Check hardware integration

The hardware check reads the current audio and brightness state without changing it:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hardware-check.ps1
```

Add `-VerifyWrites` to write the current values back unchanged and verify the write paths:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hardware-check.ps1 -VerifyWrites
```

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
