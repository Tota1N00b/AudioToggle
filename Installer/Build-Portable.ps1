param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "AudioToggle\\AudioToggle.csproj"
$outputPath = Join-Path $scriptRoot "Output\\portable"
$zipPath = Join-Path $scriptRoot "Output\\AudioToggle-portable.zip"

if (Test-Path $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $outputPath

Write-Host "Portable build created at $outputPath"

Compress-Archive -Path (Join-Path $outputPath "*") -DestinationPath $zipPath

Write-Host "Portable ZIP created at $zipPath"
