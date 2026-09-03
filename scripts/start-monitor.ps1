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

$runningMonitor = Get-Process -Name 'OriPrecisionGrapple.Monitor' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $monitor } |
    Select-Object -First 1
if ($null -ne $runningMonitor) {
    Write-Host "Monitor is already running (PID $($runningMonitor.Id))."
    return
}

$process = Start-Process -FilePath $monitor -WorkingDirectory (Split-Path -Parent $monitor) -PassThru
Start-Sleep -Milliseconds 750
if ($process.HasExited) {
    throw "Monitor exited during startup with code $($process.ExitCode). Check the Windows Application event log."
}

Write-Host "Monitor started (PID $($process.Id))."
