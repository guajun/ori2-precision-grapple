[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$metadataPath = Join-Path $projectRoot 'third_party\BepInEx.version.json'
$vendorRoot = Join-Path $projectRoot 'vendor\bepinex'
$requiredAssembly = Join-Path $vendorRoot 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'
$cacheRoot = Join-Path $projectRoot '.cache'
$minimumFreeAfterBootstrap = 10GB

if (Test-Path -LiteralPath $requiredAssembly) {
    Write-Host "BepInEx SDK is already present at $vendorRoot"
    exit 0
}

$drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($projectRoot).Substring(0, 1))
if ($drive.Free -lt ($minimumFreeAfterBootstrap + 200MB)) {
    throw 'Not enough free space. Bootstrap refuses to reduce the project drive below 10 GiB free.'
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
$archive = Join-Path $cacheRoot $metadata.archive

Write-Host "Downloading $($metadata.name) $($metadata.version)..."
Invoke-WebRequest -Uri $metadata.downloadUrl -OutFile $archive

$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
if ($actualHash -ne $metadata.sha256) {
    throw "BepInEx archive hash mismatch. Expected $($metadata.sha256), got $actualHash."
}

Expand-Archive -LiteralPath $archive -DestinationPath $vendorRoot -Force

$resolvedArchive = [System.IO.Path]::GetFullPath($archive)
$resolvedCache = [System.IO.Path]::GetFullPath($cacheRoot) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedArchive.StartsWith($resolvedCache, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to delete an archive outside the project cache: $resolvedArchive"
}

Remove-Item -LiteralPath $resolvedArchive -Force
Write-Host "BepInEx SDK ready at $vendorRoot"
