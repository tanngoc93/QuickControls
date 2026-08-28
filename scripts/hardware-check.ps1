param(
    [switch]$VerifyWrites
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appPath = Join-Path $workspaceRoot 'artifacts\QuickControls.exe'
if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) {
    throw 'Build artifacts were not found. Run scripts\build.ps1 first.'
}

$assembly = [System.Reflection.Assembly]::LoadFrom($appPath)

$audioType = $assembly.GetType('QuickControls.Services.AudioService', $true)
$audioService = [System.Activator]::CreateInstance($audioType)
try {
    $audioState = $audioType.GetMethod('GetState').Invoke($audioService, $null)
    $audioWriteSucceeded = $null
    if ($VerifyWrites -and $audioState.Available) {
        $audioWriteSucceeded = $audioType.GetMethod('SetVolume').Invoke(
            $audioService,
            [object[]]@([int]$audioState.Volume))
    }
    [PSCustomObject]@{
        Component = 'Audio'
        Available = $audioState.Available
        CurrentPercent = $audioState.Volume
        Muted = $audioState.Muted
        SameValueWriteSucceeded = $audioWriteSucceeded
    } | Format-List
}
finally {
    $audioType.GetMethod('Dispose').Invoke($audioService, $null) | Out-Null
}

$brightnessType = $assembly.GetType('QuickControls.Services.BrightnessService', $true)
$brightnessService = [System.Activator]::CreateInstance($brightnessType)
try {
    $devices = $brightnessType.GetProperty('Devices').GetValue($brightnessService, $null)
    if ($devices.Count -eq 0) {
        Write-Host 'No supported brightness devices were found.'
    }
    foreach ($device in $devices) {
        [object[]]$readArguments = @([int]0)
        $readSucceeded = $device.GetType().GetMethod('TryGetPercent').Invoke($device, $readArguments)
        $writeSucceeded = $null
        if ($VerifyWrites -and $readSucceeded) {
            $writeSucceeded = $device.GetType().GetMethod('SetPercent').Invoke(
                $device,
                [object[]]@([int]$readArguments[0]))
        }
        [PSCustomObject]@{
            Component = 'Brightness'
            Device = $device.DisplayName
            Available = $readSucceeded
            CurrentPercent = if ($readSucceeded) { $readArguments[0] } else { $null }
            SameValueWriteSucceeded = $writeSucceeded
        } | Format-List
    }
}
finally {
    $brightnessType.GetMethod('Dispose').Invoke($brightnessService, $null) | Out-Null
}

$hardwareType = $assembly.GetType('QuickControls.Services.HardwareMonitorService', $true)
$hardwareService = [System.Activator]::CreateInstance($hardwareType)
try {
    $hardwareType.GetMethod('ReadSnapshot').Invoke($hardwareService, $null) | Out-Null
    Start-Sleep -Milliseconds 1100
    $snapshot = $hardwareType.GetMethod('ReadSnapshot').Invoke($hardwareService, $null)
    foreach ($metricName in @('Cpu', 'Gpu', 'Memory', 'Storage')) {
        $metric = $snapshot.$metricName
        [PSCustomObject]@{
            Component = "Hardware monitor - $metricName"
            Device = $metric.Name
            Present = $metric.Present
            UsagePercent = $metric.UsagePercent
            TemperatureCelsius = $metric.TemperatureCelsius
            TemperatureStatus = if ($null -ne $metric.TemperatureCelsius) { 'Reported' } else { 'Not reported' }
        } | Format-List
    }
}
finally {
    $hardwareType.GetMethod('Dispose').Invoke($hardwareService, $null) | Out-Null
}

$settingsType = $assembly.GetType('QuickControls.Models.AppSettings', $true)
$settings = $settingsType.GetMethod('CreateDefaults').Invoke($null, $null)
$hotkeyType = $assembly.GetType('QuickControls.Services.HotkeyManager', $true)
$runningQuickControls = @(Get-Process -Name 'QuickControls' -ErrorAction SilentlyContinue)
if ($runningQuickControls.Count -gt 0) {
    [PSCustomObject]@{
        Component = 'Global hotkeys'
        Registered = 'Already active'
        Total = 6
        Conflicts = 'Skipped because the installed Quick Controls app is already using its shortcuts.'
    } | Format-List
}
else {
    $hotkeyManager = [System.Activator]::CreateInstance($hotkeyType)
    try {
        $failures = $hotkeyType.GetMethod('RegisterAll').Invoke($hotkeyManager, [object[]]@($settings))
        [PSCustomObject]@{
            Component = 'Global hotkeys'
            Registered = 6 - $failures.Count
            Total = 6
            Conflicts = (($failures | ForEach-Object { $_.ToString() }) -join ', ')
        } | Format-List
    }
    finally {
        $hotkeyType.GetMethod('Dispose').Invoke($hotkeyManager, $null) | Out-Null
    }
}
