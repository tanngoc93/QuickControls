# Quick Controls Localization Guide for Windows

Quick Controls includes runtime translations for its Windows volume controls, brightness controls, global shortcut settings, system tray menu, notifications, and on-screen feedback. Language resources are compiled into the application, so users do not need to download a language pack or edit a configuration file.

> This guide describes localization on the current `main` branch. The five-language interface is listed under [Unreleased](../CHANGELOG.md#unreleased) until it is included in a versioned release.

## Supported interface languages

| Language | Stored code | Formatting culture | Preferred Windows UI font |
| --- | --- | --- | --- |
| English | `en` | `en-US` | Segoe UI |
| Vietnamese | `vi` | `vi-VN` | Segoe UI |
| Japanese | `ja` | `ja-JP` | Yu Gothic UI, then Meiryo UI |
| Simplified Chinese | `zh-CN` | `zh-CN` | Microsoft YaHei UI |
| French | `fr` | `fr-FR` | Segoe UI |

The language selector displays each language name in its native form. English is the default and fallback language.

## How a user changes language

1. Open **Settings**.
2. Select **Interface** in the flat dark sidebar.
3. Choose a language from **Language**.
4. Select **Save changes**.

The saved choice applies to the control panel, redesigned Settings window, system tray commands, About window, notifications, accessibility names, display labels, and volume or brightness on-screen display. Quick Controls stores the language code in `%LOCALAPPDATA%\QuickControls\settings.xml` and applies it before creating the main UI the next time it starts.

## Runtime text architecture

[AppText.cs](../src/QuickControls/Services/AppText.cs) is the single runtime text catalog. It contains:

- The supported language list and native display names.
- Language-code normalization and culture selection.
- An English source dictionary and one dictionary for every translation.
- English fallback behavior for an unavailable code or key.
- Font-family selection for Latin, Japanese, and Simplified Chinese text.
- Catalog validation for missing keys, unknown keys, empty values, and mismatched format placeholders.

Use `AppText.Get("Key.Name")` for fixed text and `AppText.Format("Key.Name", value)` for text with numbered placeholders. User-visible text should not be introduced directly in a form, tray menu, notification, or accessibility property when a catalog key can be used.

## Translation rules

Follow these rules when changing or adding interface text:

1. Treat the English dictionary as the canonical key set.
2. Add the same key to every supported language dictionary in the same change.
3. Preserve numbered placeholders exactly. If English contains `{0}`, every translation must contain `{0}`.
4. Keep product names, shortcut key names, and Windows feature names consistent with the surrounding interface.
5. Translate meaning and action, not English word order. Prefer short, direct labels that fit at 100%, 125%, 150%, and 200% display scaling.
6. Check punctuation, capitalization, and accelerator-independent wording in the actual control where the text appears.
7. Do not add instructions or technical claims to a translation that are absent from the English source.

The catalog validator compares placeholder indexes, so reordering `{0}` and `{1}` is allowed when the translated grammar requires it. Removing a placeholder, introducing a new one, or leaving a translation blank fails validation.

## Add another language

To add a new Quick Controls interface language:

1. Add the normalized language code to `NormalizeLanguageCode` in [AppText.cs](../src/QuickControls/Services/AppText.cs).
2. Add an `AppLanguageOption` with its stored code, native display name, and English name.
3. Map the language to an appropriate specific culture and Windows font fallback.
4. Add a complete translation dictionary containing every English key.
5. Register the dictionary in `CreateCatalog`.
6. Update the expected language count and localized preview list in [scripts/test.ps1](../scripts/test.ps1).
7. Update the user guide, README, changelog, and this language table.
8. Build, run the full automated checks, and review every generated localized preview.

Do not advertise a language as supported until its entire catalog passes validation and the panel, Settings pages, tray menu, and notifications have been reviewed in context.

## Build and test localization

Build all normal artifacts and preview images:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Run the complete validation suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

The tests call `AppText.ValidateCatalog()`, require the expected language count, initialize the localized tray service, and render localized Interface, Keyboard shortcuts, General, and Full-panel previews. The normal build also renders canonical English Interface and Keyboard shortcuts screenshots for project documentation.

## Manual localization checklist

Review at least the following areas in each language:

- Full, Horizontal Mini, Vertical Mini, and Edge Dock panel modes.
- Interface, Keyboard shortcuts, and General Settings pages.
- Custom title bar, flat dark sidebar, layout tile captions, keycaps, toggle rows, footer actions, and error text.
- Volume, mute, brightness, display selection, and unavailable-device states.
- System tray menu, Edge Dock menu, About window, startup notifications, and exit confirmation.
- On-screen display labels and percentage formatting.
- Keyboard navigation, focus outlines, accessible names, truncation, clipping, and fallback fonts.

Test on a small working area and with Windows display scaling above 100%. A screenshot that looks correct in English can still clip in French or use an unsuitable fallback font in Japanese or Simplified Chinese.

## Common localization failures

### The application falls back to English

The stored language code may not normalize to a supported code. Confirm the value in `settings.xml`, the `NormalizeLanguageCode` mapping, and the `CreateCatalog` registration.

### The application fails during catalog initialization

Run `scripts\test.ps1` and inspect the reported key. A translation dictionary is probably missing an English key, contains an unknown key, has an empty value, or uses different numbered placeholders.

### Text is clipped or difficult to read

Shorten the translation without changing its meaning, then review the generated preview and the live app at multiple display-scaling values. Do not reduce the shared font size for one language unless the layout cannot be corrected safely.

### A system tray item does not change language

Confirm the item resolves its text through `AppText` when `TrayService.ApplyLanguage()` runs. Avoid caching a user-visible English literal in a control that survives a language change.
