[CmdletBinding()]
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Ori and the Will of the Wisps'
)

$ErrorActionPreference = 'Stop'
$gameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$exe = Join-Path $gameRoot 'oriwotw.exe'
$gameAssembly = Join-Path $gameRoot 'GameAssembly.dll'
$metadata = Join-Path $gameRoot 'oriwotw_Data\il2cpp_data\Metadata\global-metadata.dat'

foreach ($requiredFile in @($exe, $gameAssembly, $metadata)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required game file is missing: $requiredFile"
    }
}

$files = Get-FileHash -Algorithm SHA256 -LiteralPath $exe, $gameAssembly, $metadata |
    Select-Object Path, Hash

[PSCustomObject]@{
    GameRoot = $gameRoot
    UnityVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    IsIl2Cpp = Test-Path -LiteralPath (Join-Path $gameRoot 'oriwotw_Data\il2cpp_data')
    BepInExInstalled = Test-Path -LiteralPath (Join-Path $gameRoot 'BepInEx\core\BepInEx.Unity.IL2CPP.dll')
    ModInstalled = Test-Path -LiteralPath (Join-Path $gameRoot 'BepInEx\plugins\OriPrecisionBash\OriPrecisionBash.dll')
    GameRunning = $null -ne (Get-Process -Name 'oriwotw' -ErrorAction SilentlyContinue)
} | Format-List

$files | Format-List
