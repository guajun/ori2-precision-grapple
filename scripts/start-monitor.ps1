[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$monitor = Join-Path $projectRoot 'artifacts\OriPrecisionGrapple\Monitor\OriPrecisionGrapple.Monitor.exe'

if (-not (Test-Path -LiteralPath $monitor -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}

if (-not (Test-Path -LiteralPath $monitor -PathType Leaf)) {
    throw "Monitor executable was not built: $monitor"
}

Start-Process -FilePath $monitor -WorkingDirectory (Split-Path -Parent $monitor)
