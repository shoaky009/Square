param(
    [string]$FlutterCommit = "cb07e449603530815f8caf08dbf05408546cda34",
    [ValidateSet("windows-x64", "windows-arm64", "linux-x64", "linux-arm64")]
    [string]$Platform = "windows-x64",
    [string]$OutputDirectory = "artifacts/impeller-sdk"
)

$ErrorActionPreference = "Stop"

$archiveUrl = "https://storage.googleapis.com/flutter_infra_release/flutter/$FlutterCommit/$Platform/impeller_sdk.zip"
$platformDirectory = Join-Path $OutputDirectory $Platform
$archivePath = Join-Path $platformDirectory "impeller_sdk.zip"
$extractPath = Join-Path $platformDirectory "extracted"

New-Item -ItemType Directory -Path $platformDirectory -Force | Out-Null

Write-Host "Downloading Impeller SDK from $archiveUrl"
Invoke-WebRequest -Uri $archiveUrl -OutFile $archivePath

if (Test-Path -LiteralPath $extractPath) {
    Remove-Item -LiteralPath $extractPath -Recurse -Force
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
Write-Host "Impeller SDK extracted to $extractPath"
