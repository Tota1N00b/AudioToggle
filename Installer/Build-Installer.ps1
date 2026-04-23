param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "AudioToggle\\AudioToggle.csproj"
$publishPath = Join-Path $scriptRoot "Output\\portable"
$installerScriptPath = Join-Path $scriptRoot "AudioToggle.iss"

& (Join-Path $scriptRoot "Build-Portable.ps1") -Configuration $Configuration -Runtime $Runtime

[xml]$projectXml = Get-Content $projectPath
$appVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    $appVersion = "1.0.0"
}

$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$candidatePaths =
@(
    (Join-Path $env:LOCALAPPDATA "Programs\\Inno Setup 6\\ISCC.exe"),
    "C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe",
    "C:\\Program Files\\Inno Setup 6\\ISCC.exe"
)

$isccPath =
    if ($isccCommand) {
        $isccCommand.Source
    }
    else {
        $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

if ([string]::IsNullOrWhiteSpace($isccPath)) {
    throw "Inno Setup 6 was not found. Install it, then rerun Installer\\Build-Installer.ps1."
}

& $isccPath "/DMyAppVersion=$appVersion" "/DMyPublishDir=$publishPath" $installerScriptPath

Write-Host "Installer created under $(Join-Path $scriptRoot 'Output\\installer')"
