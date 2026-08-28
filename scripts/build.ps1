param(
    [switch]$SkipPreview,
    [string]$CertificateThumbprint,
    [string]$TimestampServer
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $workspaceRoot 'src\QuickControls'
$installerRoot = Join-Path $workspaceRoot 'installer'
$artifactRoot = Join-Path $workspaceRoot 'artifacts'
$appOutput = Join-Path $artifactRoot 'QuickControls.exe'
$appConfigOutput = Join-Path $artifactRoot 'QuickControls.exe.config'
$setupOutput = Join-Path $artifactRoot 'QuickControls-Setup.exe'
$portableOutput = Join-Path $artifactRoot 'QuickControls-Portable.zip'
$iconOutput = Join-Path $artifactRoot 'QuickControls.ico'
$previewOutput = Join-Path $artifactRoot 'QuickControls-Preview.png'
$compactPreviewOutput = Join-Path $artifactRoot 'QuickControls-Compact-Preview.png'
$verticalPreviewOutput = Join-Path $artifactRoot 'QuickControls-Vertical-Preview.png'
$edgePreviewOutput = Join-Path $artifactRoot 'QuickControls-Edge-Preview.png'
$settingsPreviewOutput = Join-Path $artifactRoot 'QuickControls-Settings-Preview.png'
$shortcutsPreviewOutput = Join-Path $artifactRoot 'QuickControls-Shortcuts-Preview.png'
$hardwarePreviewOutput = Join-Path $artifactRoot 'QuickControls-Hardware-Monitor-Preview.png'
$uninstallerPreviewOutput = Join-Path $artifactRoot 'QuickControls-Uninstaller-Preview.png'
$uninstallerScaledPreviewOutput = Join-Path $artifactRoot 'QuickControls-Uninstaller-150-Preview.png'

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $compiler) {
    throw 'The built-in Windows C# compiler was not found.'
}

$frameworkDirectory = Split-Path -Parent $compiler
$references = @(
    (Join-Path $frameworkDirectory 'Accessibility.dll'),
    (Join-Path $frameworkDirectory 'System.dll'),
    (Join-Path $frameworkDirectory 'System.Core.dll'),
    (Join-Path $frameworkDirectory 'System.Drawing.dll'),
    (Join-Path $frameworkDirectory 'System.Management.dll'),
    (Join-Path $frameworkDirectory 'System.Windows.Forms.dll'),
    (Join-Path $frameworkDirectory 'System.Xml.dll')
)
foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) {
        throw "Required framework assembly was not found: $reference"
    }
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

Add-Type -AssemblyName System.Drawing
$iconBitmap = [System.Drawing.Bitmap]::new(64, 64)
$iconGraphics = [System.Drawing.Graphics]::FromImage($iconBitmap)
try {
    $iconGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $iconGraphics.Clear([System.Drawing.Color]::Transparent)
    $iconBounds = [System.Drawing.Rectangle]::new(2, 2, 60, 60)
    $iconBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $iconBounds,
        [System.Drawing.Color]::FromArgb(21, 94, 239),
        [System.Drawing.Color]::FromArgb(46, 144, 250),
        45.0)
    try {
        $iconGraphics.FillEllipse($iconBrush, $iconBounds)
    }
    finally {
        $iconBrush.Dispose()
    }
    $iconPen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, [float]4.5)
    try {
        $iconGraphics.DrawLine($iconPen, 17, 27, 25, 27)
        $iconGraphics.DrawLine($iconPen, 25, 27, 36, 18)
        $iconGraphics.DrawLine($iconPen, 36, 18, 36, 46)
        $iconGraphics.DrawLine($iconPen, 36, 46, 25, 37)
        $iconGraphics.DrawLine($iconPen, 25, 37, 17, 37)
        $iconGraphics.DrawArc($iconPen, 34, 23, 16, 19, -55, 110)
    }
    finally {
        $iconPen.Dispose()
    }
    $pngStream = [System.IO.MemoryStream]::new()
    try {
        $iconBitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        [byte[]]$pngBytes = $pngStream.ToArray()
    }
    finally {
        $pngStream.Dispose()
    }
    $iconStream = [System.IO.File]::Create($iconOutput)
    $iconWriter = [System.IO.BinaryWriter]::new($iconStream)
    try {
        $iconWriter.Write([uint16]0)
        $iconWriter.Write([uint16]1)
        $iconWriter.Write([uint16]1)
        $iconWriter.Write([byte]64)
        $iconWriter.Write([byte]64)
        $iconWriter.Write([byte]0)
        $iconWriter.Write([byte]0)
        $iconWriter.Write([uint16]1)
        $iconWriter.Write([uint16]32)
        $iconWriter.Write([uint32]$pngBytes.Length)
        $iconWriter.Write([uint32]22)
        $iconWriter.Write($pngBytes)
    }
    finally {
        $iconWriter.Dispose()
    }
}
finally {
    $iconGraphics.Dispose()
    $iconBitmap.Dispose()
}

Write-Host 'Building QuickControls application...'
$appSources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }
$appArguments = @(
    '/nologo',
    '/codepage:65001',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug:pdbonly',
    ('/out:' + $appOutput),
    ('/win32icon:' + $iconOutput),
    ('/win32manifest:' + (Join-Path $sourceRoot 'app.manifest'))
)
foreach ($reference in $references) {
    $appArguments += '/reference:' + $reference
}
$appArguments += $appSources
& $compiler $appArguments
if ($LASTEXITCODE -ne 0) {
    throw "Application compilation failed with exit code $LASTEXITCODE."
}
Copy-Item -LiteralPath (Join-Path $sourceRoot 'App.config') -Destination $appConfigOutput -Force

$signingCertificate = $null
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $normalizedThumbprint = $CertificateThumbprint.Replace(' ', '')
    $certificatePath = 'Cert:\CurrentUser\My\' + $normalizedThumbprint
    $signingCertificate = Get-Item -LiteralPath $certificatePath -ErrorAction Stop
    if (-not $signingCertificate.HasPrivateKey) {
        throw 'The selected signing certificate does not have an accessible private key.'
    }
    $signatureParameters = @{
        FilePath = $appOutput
        Certificate = $signingCertificate
        HashAlgorithm = 'SHA256'
    }
    if (-not [string]::IsNullOrWhiteSpace($TimestampServer)) {
        $signatureParameters.TimestampServer = $TimestampServer
    }
    $applicationSignature = Set-AuthenticodeSignature @signatureParameters
    if ($applicationSignature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Application signing failed: $($applicationSignature.StatusMessage)"
    }
}

Write-Host 'Building one-click installer...'
$installerSources = Get-ChildItem -LiteralPath $installerRoot -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }
$installerReferences = @(
    (Join-Path $frameworkDirectory 'System.dll'),
    (Join-Path $frameworkDirectory 'System.Core.dll'),
    (Join-Path $frameworkDirectory 'System.Drawing.dll'),
    (Join-Path $frameworkDirectory 'System.Windows.Forms.dll')
)
$installerArguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug:pdbonly',
    ('/out:' + $setupOutput),
    ('/win32icon:' + $iconOutput),
    ('/win32manifest:' + (Join-Path $installerRoot 'app.manifest')),
    ('/resource:' + $appOutput + ',QuickControls.Payload.exe'),
    ('/resource:' + $appConfigOutput + ',QuickControls.Payload.config')
)
foreach ($reference in $installerReferences) {
    $installerArguments += '/reference:' + $reference
}
$installerArguments += $installerSources
& $compiler $installerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

if ($null -ne $signingCertificate) {
    $setupSignatureParameters = @{
        FilePath = $setupOutput
        Certificate = $signingCertificate
        HashAlgorithm = 'SHA256'
    }
    if (-not [string]::IsNullOrWhiteSpace($TimestampServer)) {
        $setupSignatureParameters.TimestampServer = $TimestampServer
    }
    $setupSignature = Set-AuthenticodeSignature @setupSignatureParameters
    if ($setupSignature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Installer signing failed: $($setupSignature.StatusMessage)"
    }
}

if (Test-Path -LiteralPath $portableOutput -PathType Leaf) {
    Remove-Item -LiteralPath $portableOutput -Force
}
Compress-Archive -LiteralPath @($appOutput, $appConfigOutput) -DestinationPath $portableOutput -CompressionLevel Optimal

if (-not $SkipPreview) {
    Write-Host 'Rendering UI preview...'
    $applicationAssembly = [System.Reflection.Assembly]::LoadFrom($appOutput)
    $previewType = $applicationAssembly.GetType('QuickControls.UI.PreviewRenderer', $true)
    $previewMethod = $previewType.GetMethod(
        'Render',
        [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
    [object[]]$renderArguments = @([string]$previewOutput, [bool]$false)
    $previewMethod.Invoke($null, $renderArguments) | Out-Null
    [object[]]$compactRenderArguments = @([string]$compactPreviewOutput, [bool]$true)
    $previewMethod.Invoke($null, $compactRenderArguments) | Out-Null
    $layoutType = $applicationAssembly.GetType('QuickControls.Models.PanelLayoutMode', $true)
    $layoutPreviewMethod = $previewType.GetMethod(
        'RenderLayout',
        [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
    [object[]]$verticalRenderArguments = @(
        [string]$verticalPreviewOutput,
        [System.Enum]::Parse($layoutType, 'VerticalMini'))
    $layoutPreviewMethod.Invoke($null, $verticalRenderArguments) | Out-Null
    [object[]]$edgeRenderArguments = @(
        [string]$edgePreviewOutput,
        [System.Enum]::Parse($layoutType, 'EdgeDock'))
    $layoutPreviewMethod.Invoke($null, $edgeRenderArguments) | Out-Null
    $settingsPreviewMethod = $previewType.GetMethod(
        'RenderSettings',
        [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
    [object[]]$settingsRenderArguments = @([string]$settingsPreviewOutput)
    $settingsPreviewMethod.Invoke($null, $settingsRenderArguments) | Out-Null
    $settingsPagePreviewMethod = $previewType.GetMethod(
        'RenderSettingsPage',
        [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
    [object[]]$shortcutsRenderArguments = @([string]$shortcutsPreviewOutput, [string]'Shortcuts', [string]'en')
    $settingsPagePreviewMethod.Invoke($null, $shortcutsRenderArguments) | Out-Null
    $hardwarePreviewMethod = $previewType.GetMethod(
        'RenderHardwareMonitor',
        [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
    [object[]]$hardwareRenderArguments = @([string]$hardwarePreviewOutput)
    $hardwarePreviewMethod.Invoke($null, $hardwareRenderArguments) | Out-Null

    Write-Host 'Rendering uninstaller UI previews...'
    $installerAssembly = [System.Reflection.Assembly]::LoadFrom($setupOutput)
    $installerPreviewType = $installerAssembly.GetType(
        'QuickControls.Installer.InstallerPreviewRenderer',
        $true)
    $uninstallerPreviewMethod = $installerPreviewType.GetMethod('RenderUninstaller')
    [object[]]$uninstallerPreviewArguments = @([string]$uninstallerPreviewOutput)
    $uninstallerPreviewMethod.Invoke($null, $uninstallerPreviewArguments) | Out-Null
    $uninstallerScaledPreviewMethod = $installerPreviewType.GetMethod('RenderUninstallerAtScale')
    [object[]]$uninstallerScaledPreviewArguments = @(
        [string]$uninstallerScaledPreviewOutput,
        [single]1.5)
    $uninstallerScaledPreviewMethod.Invoke($null, $uninstallerScaledPreviewArguments) | Out-Null
}

$setupHash = (Get-FileHash -LiteralPath $setupOutput -Algorithm SHA256).Hash
Write-Host ''
Write-Host 'Build completed successfully.' -ForegroundColor Green
Write-Host "Application: $appOutput"
Write-Host "Installer:   $setupOutput"
Write-Host "Portable:    $portableOutput"
if (-not $SkipPreview) {
    Write-Host "Preview:     $previewOutput"
    Write-Host "Compact UI:  $compactPreviewOutput"
    Write-Host "Vertical UI: $verticalPreviewOutput"
    Write-Host "Edge UI:     $edgePreviewOutput"
    Write-Host "Settings UI: $settingsPreviewOutput"
    Write-Host "Shortcuts UI:$shortcutsPreviewOutput"
    Write-Host "Hardware UI: $hardwarePreviewOutput"
    Write-Host "Uninstall UI: $uninstallerPreviewOutput"
    Write-Host "Uninstall 150%: $uninstallerScaledPreviewOutput"
}
Write-Host "Setup SHA256: $setupHash"
if ($null -ne $signingCertificate) { Write-Host "Signed by:    $($signingCertificate.Subject)" }
