[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginOutput = Join-Path $projectRoot "src\OriPrecisionGrapple.Plugin\bin\$Configuration\net6.0"
$coreOutput = Join-Path $projectRoot "src\OriPrecisionGrapple.Core\bin\$Configuration\netstandard2.1"
$packageRoot = Join-Path $projectRoot 'artifacts\OriPrecisionGrapple\BepInEx\plugins\OriPrecisionGrapple'

$pluginDll = Join-Path $pluginOutput 'OriPrecisionGrapple.dll'
$coreDll = Join-Path $coreOutput 'OriPrecisionGrapple.Core.dll'
if (-not (Test-Path -LiteralPath $pluginDll) -or -not (Test-Path -LiteralPath $coreDll)) {
    throw 'Build outputs are missing. Run scripts/build.ps1 first.'
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination $packageRoot -Force
Copy-Item -LiteralPath $coreDll -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $packageRoot -Force

$packageBytes = (Get-ChildItem $packageRoot -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host "Package ready at $packageRoot ($packageBytes bytes)."
