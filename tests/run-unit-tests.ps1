param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$out = Join-Path $root "artifacts\$Configuration"
$coreDll = Join-Path $out 'SharpUpSQL.Core\SharpUpSQL.Core.dll'
$cliExe = Join-Path $out 'SharpUpSQL.Cli\SharpUpSQL.exe'
$testOut = Join-Path $out 'SharpUpSQL.Tests'
$testExe = Join-Path $testOut 'SharpUpSQL.Tests.exe'

if (-not (Test-Path $cliExe)) {
    Write-Host "Building SharpUpSQL ($Configuration)..." -ForegroundColor Yellow
    & (Join-Path $root 'build.ps1') -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$framework = Split-Path $csc -Parent
New-Item -ItemType Directory -Force -Path $testOut | Out-Null

Write-Host 'Compiling SharpUpSQL.Tests...' -ForegroundColor Cyan
$testSources = @(
    (Join-Path $root 'tests\SharpUpSQL.Tests\Properties\AssemblyInfo.cs'),
    (Join-Path $root 'tests\SharpUpSQL.Tests\TestFramework.cs'),
    (Join-Path $root 'tests\SharpUpSQL.Tests\CoreHelperTests.cs'),
    (Join-Path $root 'tests\SharpUpSQL.Tests\SqlQueryStaticTests.cs'),
    (Join-Path $root 'tests\SharpUpSQL.Tests\CommandRegistryTests.cs'),
    (Join-Path $root 'tests\SharpUpSQL.Tests\Program.cs')
)

& $csc /nologo /target:exe /out:$testExe /platform:anycpu `
    "/reference:$framework\System.dll" `
    "/reference:$framework\System.Core.dll" `
    "/reference:$coreDll" `
    @testSources

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item $cliExe $testOut -Force
Copy-Item (Join-Path $out 'SharpUpSQL.Cli\SharpUpSQL.Core.dll') $testOut -Force
Copy-Item (Join-Path $out 'SharpUpSQL.Cli\SharpUpSQL.dll') $testOut -Force

Write-Host 'Running unit tests...' -ForegroundColor Cyan
& $testExe $cliExe
exit $LASTEXITCODE
