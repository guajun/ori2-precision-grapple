[CmdletBinding()]
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Ori and the Will of the Wisps'
)

$ErrorActionPreference = 'Stop'
$gameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$pluginRoot = Join-Path $gameRoot 'BepInEx\plugins\OriPrecisionGrapple'

if (Get-Process -Name 'oriwotw' -ErrorAction SilentlyContinue) {
    throw 'Ori is running. Close the game before uninstalling the mod.'
}

if (-not (Test-Path -LiteralPath $pluginRoot -PathType Container)) {
    Write-Host 'Ori Precision Grapple is not installed.'
    exit 0
}

$backupRoot = Join-Path $gameRoot 'OriPrecisionGrapple.backup'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = Join-Path $backupRoot "uninstalled-$timestamp"
Move-Item -LiteralPath $pluginRoot -Destination $backupPath

Write-Host "Plugin moved to $backupPath"
Write-Host 'BepInEx was intentionally left installed.'
