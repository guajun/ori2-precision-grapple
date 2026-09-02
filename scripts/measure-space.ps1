[CmdletBinding()]
param()

$projectRoot = Split-Path -Parent $PSScriptRoot
$driveName = [System.IO.Path]::GetPathRoot($projectRoot).Substring(0, 1)
$drive = Get-PSDrive -Name $driveName
$projectBytes = (Get-ChildItem -LiteralPath $projectRoot -Recurse -File -ErrorAction SilentlyContinue |
    Measure-Object Length -Sum).Sum

[PSCustomObject]@{
    ProjectRoot = $projectRoot
    ProjectMiB = [math]::Round($projectBytes / 1MB, 2)
    DriveFreeGiB = [math]::Round($drive.Free / 1GB, 2)
    ReserveGiB = 10
    AboveReserveGiB = [math]::Round(($drive.Free - 10GB) / 1GB, 2)
} | Format-List
