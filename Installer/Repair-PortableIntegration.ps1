param(
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"

function Get-StartupShortcutTarget {
    $startupFolder = [Environment]::GetFolderPath("Startup")
    $shortcutPath = Join-Path $startupFolder "Audio Toggle.lnk"

    if (-not (Test-Path $shortcutPath)) {
        return $null
    }

    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        return $shortcut.TargetPath
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
}

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Get-StartupShortcutTarget
}

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    throw "Could not determine the portable AudioToggle.exe path. Pass -ExecutablePath explicitly."
}

if (-not (Test-Path $ExecutablePath)) {
    throw "AudioToggle.exe was not found at: $ExecutablePath"
}

$resolvedPath = (Resolve-Path $ExecutablePath).Path
Start-Process -FilePath $resolvedPath -ArgumentList "--register-shell-integration" -Wait

Write-Host "Portable shell integration refreshed for $resolvedPath"
