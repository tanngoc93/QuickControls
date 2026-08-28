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
$settingsPreviewPath = Join-Path $artifactRoot 'QuickControls-Settings-Preview.png'

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
$settingsPreviewMethod = $previewType.GetMethod(
    'RenderSettings',
    [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
[object[]]$settingsRenderArguments = @([string]$settingsPreviewPath)
$settingsPreviewMethod.Invoke($null, $settingsRenderArguments) | Out-Null


Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($previewPath)
try {
    if ($image.Width -ne 420 -or $image.Height -ne 452) {
        throw "Unexpected preview dimensions: $($image.Width)x$($image.Height)."
    }
}
finally {
    $image.Dispose()
}

$compactImage = [System.Drawing.Image]::FromFile($compactPreviewPath)
try {
    if ($compactImage.Width -ne 336 -or $compactImage.Height -ne 64) {
        throw "Unexpected compact preview dimensions: $($compactImage.Width)x$($compactImage.Height)."
    }
}
finally {
    $compactImage.Dispose()
}

$settingsImage = [System.Drawing.Image]::FromFile($settingsPreviewPath)
try {
    if ($settingsImage.Width -lt 620 -or $settingsImage.Height -lt 650) {
        throw "Unexpected settings preview dimensions: $($settingsImage.Width)x$($settingsImage.Height)."
    }
}
finally {
    $settingsImage.Dispose()
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
