[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $projectRoot 'src\OriPrecisionGrapple.Plugin\OriPrecisionGrapple.Plugin.csproj'
$testProject = Join-Path $projectRoot 'tests\OriPrecisionGrapple.Core.Tests\OriPrecisionGrapple.Core.Tests.csproj'
$runtimeTestProject = Join-Path $projectRoot 'tests\OriPrecisionGrapple.Runtime.Tests\OriPrecisionGrapple.Runtime.Tests.csproj'

& (Join-Path $PSScriptRoot 'bootstrap.ps1')
dotnet build $pluginProject -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE."
}

if (-not $SkipTests) {
    dotnet run --project $testProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Core tests failed with exit code $LASTEXITCODE."
    }

    dotnet run --project $runtimeTestProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime integration tests failed with exit code $LASTEXITCODE."
    }
}

& (Join-Path $PSScriptRoot 'package.ps1') -Configuration $Configuration
