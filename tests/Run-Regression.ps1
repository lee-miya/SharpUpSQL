# Full regression entry point: build, unit tests, optional lab tests.
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipLab,
    [switch]$CompareSnapshots
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host '=== SharpUpSQL Regression Suite ===' -ForegroundColor Cyan
Write-Host ''

Write-Host '[1/3] Build' -ForegroundColor Yellow
& (Join-Path $root 'build.ps1') -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host '[2/3] Unit tests' -ForegroundColor Yellow
& (Join-Path $PSScriptRoot 'run-unit-tests.ps1') -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipLab) {
    Write-Host ''
    Write-Host '[3/3] Lab regression' -ForegroundColor Yellow
    $labArgs = @{
        Configuration = $Configuration
    }
    if ($CompareSnapshots) { $labArgs['CompareSnapshots'] = $true }
    & (Join-Path $PSScriptRoot 'Run-LabRegression.ps1') @labArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host ''
    Write-Host '[3/3] Lab regression skipped (-SkipLab)' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host 'Regression suite completed successfully.' -ForegroundColor Green
