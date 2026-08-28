param(
    [switch]$RequireSigned
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $workspaceRoot 'artifacts'
$appPath = Join-Path $artifactRoot 'QuickControls.exe'
$configPath = Join-Path $artifactRoot 'QuickControls.exe.config'
$setupPath = Join-Path $artifactRoot 'QuickControls-Setup.exe'
$previewPath = Join-Path $artifactRoot 'QuickControls-Preview.png'
$compactPreviewPath = Join-Path $artifactRoot 'QuickControls-Compact-Preview.png'
$verticalPreviewPath = Join-Path $artifactRoot 'QuickControls-Vertical-Preview.png'
$edgePreviewPath = Join-Path $artifactRoot 'QuickControls-Edge-Preview.png'
$settingsPreviewPath = Join-Path $artifactRoot 'QuickControls-Settings-Preview.png'
$shortcutsPreviewPath = Join-Path $artifactRoot 'QuickControls-Shortcuts-Preview.png'
$frenchPreviewPath = Join-Path $artifactRoot 'QuickControls-Settings-French-Preview.png'
$uninstallerPreviewPath = Join-Path $artifactRoot 'QuickControls-Uninstaller-Preview.png'
$uninstallerScaledPreviewPath = Join-Path $artifactRoot 'QuickControls-Uninstaller-150-Preview.png'

$requiredArtifactsExist =
    (Test-Path -LiteralPath $appPath -PathType Leaf) -and
    (Test-Path -LiteralPath $configPath -PathType Leaf) -and
    (Test-Path -LiteralPath $setupPath -PathType Leaf)
$needsBuild = -not $requiredArtifactsExist
if (-not $needsBuild) {
    $latestInput = Get-ChildItem -LiteralPath @(
        (Join-Path $workspaceRoot 'src'),
        (Join-Path $workspaceRoot 'installer'),
        (Join-Path $workspaceRoot 'scripts\build.ps1')) -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $oldestArtifactTime = @(
        (Get-Item -LiteralPath $appPath).LastWriteTimeUtc,
        (Get-Item -LiteralPath $setupPath).LastWriteTimeUtc) |
        Sort-Object |
        Select-Object -First 1
    $needsBuild = $null -ne $latestInput -and $latestInput.LastWriteTimeUtc -gt $oldestArtifactTime
}

if ($needsBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1')
}

$minimumSizes = @{
    $appPath = 50000
    $configPath = 100
    $setupPath = 100000
}
foreach ($entry in $minimumSizes.GetEnumerator()) {
    $file = Get-Item -LiteralPath $entry.Key
    if ($file.Length -lt $entry.Value) {
        throw "Artifact is unexpectedly small: $($file.FullName) ($($file.Length) bytes)."
    }
}

$applicationAssembly = [System.Reflection.Assembly]::LoadFrom($appPath)
$previewType = $applicationAssembly.GetType('QuickControls.UI.PreviewRenderer', $true)
$previewMethod = $previewType.GetMethod(
    'Render',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
[object[]]$renderArguments = @([string]$previewPath, [bool]$false)
$previewMethod.Invoke($null, $renderArguments) | Out-Null
[object[]]$compactRenderArguments = @([string]$compactPreviewPath, [bool]$true)
$previewMethod.Invoke($null, $compactRenderArguments) | Out-Null
$layoutType = $applicationAssembly.GetType('QuickControls.Models.PanelLayoutMode', $true)
$layoutPreviewMethod = $previewType.GetMethod(
    'RenderLayout',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
[object[]]$verticalRenderArguments = @(
    [string]$verticalPreviewPath,
    [System.Enum]::Parse($layoutType, 'VerticalMini'))
$layoutPreviewMethod.Invoke($null, $verticalRenderArguments) | Out-Null
[object[]]$edgeRenderArguments = @(
    [string]$edgePreviewPath,
    [System.Enum]::Parse($layoutType, 'EdgeDock'))
$layoutPreviewMethod.Invoke($null, $edgeRenderArguments) | Out-Null
$settingsPreviewMethod = $previewType.GetMethod(
    'RenderSettings',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
[object[]]$settingsRenderArguments = @([string]$settingsPreviewPath)
$settingsPreviewMethod.Invoke($null, $settingsRenderArguments) | Out-Null
$settingsPagePreviewMethod = $previewType.GetMethod(
    'RenderSettingsPage',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
[object[]]$shortcutsRenderArguments = @([string]$shortcutsPreviewPath, [string]'Shortcuts', [string]'en')
$settingsPagePreviewMethod.Invoke($null, $shortcutsRenderArguments) | Out-Null
[object[]]$frenchRenderArguments = @([string]$frenchPreviewPath, [string]'Interface', [string]'fr')
$settingsPagePreviewMethod.Invoke($null, $frenchRenderArguments) | Out-Null
$choiceDropDownLifecycleMethod = $previewType.GetMethod(
    'ValidateChoiceDropDownLifecycle',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
$choiceDropDownLifecycleMethod.Invoke($null, @()) | Out-Null

$installerAssembly = [System.Reflection.Assembly]::LoadFrom($setupPath)
$installerPreviewType = $installerAssembly.GetType(
    'QuickControls.Installer.InstallerPreviewRenderer',
    $true)
$uninstallerPreviewMethod = $installerPreviewType.GetMethod('RenderUninstaller')
[object[]]$uninstallerPreviewArguments = @([string]$uninstallerPreviewPath)
$uninstallerPreviewMethod.Invoke($null, $uninstallerPreviewArguments) | Out-Null
$uninstallerScaledPreviewMethod = $installerPreviewType.GetMethod('RenderUninstallerAtScale')
[object[]]$uninstallerScaledPreviewArguments = @(
    [string]$uninstallerScaledPreviewPath,
    [single]1.5)
$uninstallerScaledPreviewMethod.Invoke($null, $uninstallerScaledPreviewArguments) | Out-Null
$validateUninstallerLayoutsMethod = $installerPreviewType.GetMethod('ValidateUninstallerLayouts')
$validateUninstallerLayoutsMethod.Invoke($null, @()) | Out-Null

$layoutLanguagePreviewMethod = $previewType.GetMethod(
    'RenderLayoutLanguage',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
$fullLayout = [System.Enum]::Parse($layoutType, 'Full')
$localizedPreviews = @()
foreach ($languageCode in @('en', 'vi', 'ja', 'zh-CN', 'fr')) {
    $safeLanguageCode = $languageCode.Replace('-', '_')
    $interfacePath = Join-Path $artifactRoot "QuickControls-Settings-Interface-$safeLanguageCode-Preview.png"
    [object[]]$interfaceArguments = @([string]$interfacePath, [string]'Interface', [string]$languageCode)
    $settingsPagePreviewMethod.Invoke($null, $interfaceArguments) | Out-Null
    $localizedPreviews += [pscustomobject]@{ Path = $interfacePath; Width = 900; Height = 640 }

    $shortcutLanguagePath = Join-Path $artifactRoot "QuickControls-Settings-Shortcuts-$safeLanguageCode-Preview.png"
    [object[]]$shortcutLanguageArguments = @([string]$shortcutLanguagePath, [string]'Shortcuts', [string]$languageCode)
    $settingsPagePreviewMethod.Invoke($null, $shortcutLanguageArguments) | Out-Null
    $localizedPreviews += [pscustomobject]@{ Path = $shortcutLanguagePath; Width = 900; Height = 640 }

    $generalPath = Join-Path $artifactRoot "QuickControls-Settings-General-$safeLanguageCode-Preview.png"
    [object[]]$generalArguments = @([string]$generalPath, [string]'General', [string]$languageCode)
    $settingsPagePreviewMethod.Invoke($null, $generalArguments) | Out-Null
    $localizedPreviews += [pscustomobject]@{ Path = $generalPath; Width = 900; Height = 640 }

    $panelPath = Join-Path $artifactRoot "QuickControls-Full-$safeLanguageCode-Preview.png"
    [object[]]$panelArguments = @([string]$panelPath, $fullLayout, [string]$languageCode)
    $layoutLanguagePreviewMethod.Invoke($null, $panelArguments) | Out-Null
    $localizedPreviews += [pscustomobject]@{ Path = $panelPath; Width = 440; Height = 456 }
}

$appTextType = $applicationAssembly.GetType('QuickControls.Services.AppText', $true)
$validateCatalogMethod = $appTextType.GetMethod(
    'ValidateCatalog',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
$validateCatalogMethod.Invoke($null, @()) | Out-Null
$languageOptionsProperty = $appTextType.GetProperty(
    'LanguageOptions',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
$languageOptions = $languageOptionsProperty.GetValue($null, $null)
if ($languageOptions.Count -ne 5) {
    throw "Expected 5 supported languages, found $($languageOptions.Count)."
}

$trayType = $applicationAssembly.GetType('QuickControls.Services.TrayService', $true)
$trayService = [System.Activator]::CreateInstance($trayType, @([bool]$false))
try {
    $setLanguageMethod = $appTextType.GetMethod('SetLanguage')
    foreach ($languageCode in @('en', 'vi', 'ja', 'zh-CN', 'fr', 'en')) {
        $setLanguageMethod.Invoke($null, @([string]$languageCode)) | Out-Null
        $trayType.GetMethod('ApplyLanguage').Invoke($trayService, @()) | Out-Null
    }
}
finally {
    if ($null -ne $trayService) {
        $trayType.GetMethod('Dispose').Invoke($trayService, @()) | Out-Null
    }
}


Add-Type -AssemblyName System.Drawing
$localizedPreviews | ForEach-Object {
    $localizedImage = [System.Drawing.Image]::FromFile($_.Path)
    try {
        if ($localizedImage.Width -lt $_.Width -or $localizedImage.Height -lt $_.Height) {
            throw "Unexpected localized preview dimensions: $($_.Path) ($($localizedImage.Width)x$($localizedImage.Height))."
        }
    }
    finally {
        $localizedImage.Dispose()
    }
}

$image = [System.Drawing.Image]::FromFile($previewPath)
try {
    if ($image.Width -ne 440 -or $image.Height -ne 456) {
        throw "Unexpected preview dimensions: $($image.Width)x$($image.Height)."
    }
}
finally {
    $image.Dispose()
}

$compactImage = [System.Drawing.Image]::FromFile($compactPreviewPath)
try {
    if ($compactImage.Width -ne 520 -or $compactImage.Height -ne 72) {
        throw "Unexpected compact preview dimensions: $($compactImage.Width)x$($compactImage.Height)."
    }
}
finally {
    $compactImage.Dispose()
}

$verticalImage = [System.Drawing.Image]::FromFile($verticalPreviewPath)
try {
    if ($verticalImage.Width -ne 136 -or $verticalImage.Height -ne 360) {
        throw "Unexpected vertical preview dimensions: $($verticalImage.Width)x$($verticalImage.Height)."
    }
}
finally {
    $verticalImage.Dispose()
}

$edgeImage = [System.Drawing.Image]::FromFile($edgePreviewPath)
try {
    if ($edgeImage.Width -ne 48 -or $edgeImage.Height -ne 232) {
        throw "Unexpected edge preview dimensions: $($edgeImage.Width)x$($edgeImage.Height)."
    }
}
finally {
    $edgeImage.Dispose()
}

$settingsImage = [System.Drawing.Image]::FromFile($settingsPreviewPath)
try {
    if ($settingsImage.Width -ne 900 -or $settingsImage.Height -ne 640) {
        throw "Unexpected settings preview dimensions: $($settingsImage.Width)x$($settingsImage.Height)."
    }
}
finally {
    $settingsImage.Dispose()
}

$shortcutsImage = [System.Drawing.Image]::FromFile($shortcutsPreviewPath)
try {
    if ($shortcutsImage.Width -ne 900 -or $shortcutsImage.Height -ne 640) {
        throw "Unexpected shortcuts preview dimensions: $($shortcutsImage.Width)x$($shortcutsImage.Height)."
    }
}
finally {
    $shortcutsImage.Dispose()
}

$frenchImage = [System.Drawing.Image]::FromFile($frenchPreviewPath)
try {
    if ($frenchImage.Width -ne 900 -or $frenchImage.Height -ne 640) {
        throw "Unexpected French settings preview dimensions: $($frenchImage.Width)x$($frenchImage.Height)."
    }
}
finally {
    $frenchImage.Dispose()
}

$uninstallerImage = [System.Drawing.Image]::FromFile($uninstallerPreviewPath)
try {
    if ($uninstallerImage.Width -lt 560 -or $uninstallerImage.Height -lt 360) {
        throw "Unexpected uninstaller preview dimensions: $($uninstallerImage.Width)x$($uninstallerImage.Height)."
    }
    $uninstallerPreviewWidth = $uninstallerImage.Width
    $uninstallerPreviewHeight = $uninstallerImage.Height
}
finally {
    $uninstallerImage.Dispose()
}

$uninstallerScaledImage = [System.Drawing.Image]::FromFile($uninstallerScaledPreviewPath)
try {
    if ($uninstallerScaledImage.Width -le $uninstallerPreviewWidth -or
        $uninstallerScaledImage.Height -le $uninstallerPreviewHeight) {
        throw "The 150% uninstaller preview did not scale up: $($uninstallerScaledImage.Width)x$($uninstallerScaledImage.Height)."
    }
}
finally {
    $uninstallerScaledImage.Dispose()
}


$assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($appPath)
if ($assemblyName.Name -ne 'QuickControls') {
    throw "Unexpected application assembly name: $($assemblyName.Name)."
}

$setupHeader = [System.IO.File]::ReadAllBytes($setupPath)
if ($setupHeader.Length -lt 2 -or $setupHeader[0] -ne 0x4D -or $setupHeader[1] -ne 0x5A) {
    throw 'Installer does not contain a valid Windows executable header.'
}

$applicationSignature = Get-AuthenticodeSignature -LiteralPath $appPath
$setupSignature = Get-AuthenticodeSignature -LiteralPath $setupPath
if ($RequireSigned -and
    ($applicationSignature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
     $setupSignature.Status -ne [System.Management.Automation.SignatureStatus]::Valid)) {
    throw "A valid release signature is required. App: $($applicationSignature.Status); setup: $($setupSignature.Status)."
}

Write-Host 'All automated checks passed.' -ForegroundColor Green
Write-Host "Application bytes: $((Get-Item -LiteralPath $appPath).Length)"
Write-Host "Installer bytes:   $((Get-Item -LiteralPath $setupPath).Length)"
Write-Host "App signature:     $($applicationSignature.Status)"
Write-Host "Setup signature:   $($setupSignature.Status)"
Write-Host "Preview:           $previewPath"
Write-Host "Uninstaller UI:    four states passed at simulated 100%, 125%, 150%, 175%, and 200% scaling"
