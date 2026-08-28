# Contributing to Quick Controls

Thank you for helping improve Quick Controls.

## Before making a change

- Keep all source code, file names, documentation, and user-facing text in English.
- Keep the app simple for non-technical Windows users.
- Preserve Windows 10, Windows 11, and .NET Framework 4.0 compatibility.
- Open an issue before a large behavior or UI change so the scope can be discussed.

## Development workflow

1. Create a focused branch.
2. Make the smallest change that solves the problem.
3. Build the app:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
   ```

4. Run the automated checks:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
   ```

5. Inspect the generated UI previews when changing layout or drawing code.
6. Describe the user impact and verification in the pull request.

Do not commit generated files from `artifacts`, `bin`, or `obj`.

## Bug reports

Include:

- Windows version.
- Laptop or monitor model when brightness is involved.
- Connection type, dock, hub, or adapter when an external monitor is involved.
- The shortcut that failed and any application that may use it.
- Clear reproduction steps and screenshots when relevant.
- `%TEMP%\QuickControls-Installer.log` for installer failures.

Never post passwords, access tokens, private certificates, or other sensitive information.
