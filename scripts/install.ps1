[CmdletBinding()]
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Ori and the Will of the Wisps',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$gameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$expectedExe = Join-Path $gameRoot 'oriwotw.exe'
$vendorRoot = Join-Path $projectRoot 'vendor\bepinex'
$packageRoot = Join-Path $projectRoot 'artifacts\OriPrecisionGrapple\BepInEx\plugins\OriPrecisionGrapple'
$gameBepInEx = Join-Path $gameRoot 'BepInEx'
$gamePluginRoot = Join-Path $gameBepInEx 'plugins\OriPrecisionGrapple'
$configRoot = Join-Path $gameBepInEx 'config'

if (Get-Process -Name 'oriwotw' -ErrorAction SilentlyContinue) {
    throw 'Ori is running. Close the game before installing the mod.'
}

if (-not (Test-Path -LiteralPath $expectedExe -PathType Leaf)) {
    throw "Ori executable not found at $expectedExe"
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}

$existingBepInEx = Test-Path -LiteralPath (Join-Path $gameBepInEx 'core\BepInEx.Unity.IL2CPP.dll')
if (-not $existingBepInEx) {
    foreach ($rootConflict in @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version')) {
        $conflictPath = Join-Path $gameRoot $rootConflict
        if (Test-Path -LiteralPath $conflictPath) {
            throw "Refusing to overwrite an existing loader file: $conflictPath"
        }
    }

    Copy-Item -LiteralPath (Join-Path $vendorRoot 'BepInEx') -Destination $gameRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $vendorRoot 'dotnet') -Destination $gameRoot -Recurse
    foreach ($rootFile in @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt')) {
        Copy-Item -LiteralPath (Join-Path $vendorRoot $rootFile) -Destination $gameRoot
    }
}

New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
New-Item -ItemType Directory -Path $gamePluginRoot -Force | Out-Null
Copy-Item -Path (Join-Path $packageRoot '*') -Destination $gamePluginRoot -Force

Write-Host 'Installed Ori Precision Grapple without launching the game.'
Write-Host "Plugin path: $gamePluginRoot"
Write-Host 'The first game launch must still generate BepInEx interop assemblies and validate the runtime patches.'
