param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "artifacts\$Configuration"
$coreOut = Join-Path $out "SharpUpSQL.Core"
$libOut = Join-Path $out "SharpUpSQL"
$cliOut = Join-Path $out "SharpUpSQL.Cli"
$solution = Join-Path $root "SharpUpSQL.sln"
$fodyTargets = Join-Path $root "packages\Fody.6.8.2\build\Fody.targets"

New-Item -ItemType Directory -Force -Path $coreOut, $libOut, $cliOut | Out-Null

function Get-ModernMsBuild {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
        if ($found) {
            return @($found)[0]
        }
    }

    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Invoke-NuGetRestore {
    param([string]$SolutionPath)

    $nuget = Join-Path $root "tools\nuget.exe"
    if (-not (Test-Path $nuget)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $nuget) | Out-Null
        Write-Host "Downloading nuget.exe..."
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget
    }

    & $nuget restore $SolutionPath -ConfigFile (Join-Path $root "nuget.config") -NonInteractive
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed."
    }
}

function Publish-MsBuildArtifacts {
    param([string]$Config)

    $cliBin = Join-Path $root "src\SharpUpSQL.Cli\bin\$Config"
    $coreBin = Join-Path $root "src\SharpUpSQL.Core\bin\$Config"
    $libBin = Join-Path $root "src\SharpUpSQL\bin\$Config"
    $builtExe = Join-Path $cliBin "SharpUpSQL.Cli.exe"

    if (-not (Test-Path $builtExe)) {
        throw "MSBuild did not produce $builtExe"
    }

    Copy-Item $builtExe $cliOut -Force
    Copy-Item $builtExe (Join-Path $cliOut "SharpUpSQL.exe") -Force

    if (Test-Path (Join-Path $coreBin "SharpUpSQL.Core.dll")) {
        Copy-Item (Join-Path $coreBin "SharpUpSQL.Core.dll") $coreOut -Force
    }

    if (Test-Path (Join-Path $libBin "SharpUpSQL.dll")) {
        Copy-Item (Join-Path $libBin "SharpUpSQL.dll") $libOut -Force
    }

    Write-Host "Build complete (Costura): $cliOut\SharpUpSQL.exe"
    Write-Host "Run regression: .\tests\Run-Regression.ps1 -SkipLab"
}

$msbuild = Get-ModernMsBuild
if ($msbuild) {
    if (-not (Test-Path $fodyTargets)) {
        Write-Host "Restoring NuGet packages (Fody / Costura.Fody)..."
        Invoke-NuGetRestore $solution
    }

    if (Test-Path $fodyTargets) {
        Write-Host "Building solution with MSBuild and Costura.Fody..."
        & $msbuild $solution /p:Configuration=$Configuration /t:Build /v:m /nologo
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Publish-MsBuildArtifacts -Config $Configuration
        exit 0
    }

    Write-Host "Fody packages not found after restore; falling back to csc build." -ForegroundColor Yellow
}
else {
    Write-Host "Modern MSBuild not found; falling back to csc build (no DLL embedding)." -ForegroundColor Yellow
}

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$framework = Split-Path $csc -Parent

$coreRefs = @(
    "/reference:$framework\System.dll",
    "/reference:$framework\System.Core.dll",
    "/reference:$framework\System.Data.dll",
    "/reference:$framework\System.Net.dll"
)

$libRefs = @(
    "/reference:$framework\System.dll",
    "/reference:$framework\System.Core.dll",
    "/reference:$framework\System.Data.dll",
    "/reference:$framework\System.DirectoryServices.dll",
    "/reference:$framework\System.Management.dll",
    "/reference:$framework\System.Security.dll",
    "/reference:$framework\System.Xml.dll"
)

Write-Host "Building SharpUpSQL.Core..."
$coreSources = @(
    "$root\src\SharpUpSQL.Core\Properties\AssemblyInfo.cs",
    "$root\src\SharpUpSQL.Core\Auth\SqlConnectionOptions.cs",
    "$root\src\SharpUpSQL.Core\Auth\SqlConnectionFactory.cs",
    "$root\src\SharpUpSQL.Core\Auth\NtlmHelper.cs",
    "$root\src\SharpUpSQL.Core\Auth\PthTdsClient.cs",
    "$root\src\SharpUpSQL.Core\Execution\ConnectionTestResult.cs",
    "$root\src\SharpUpSQL.Core\Execution\ConnectionTester.cs",
    "$root\src\SharpUpSQL.Core\Execution\QueryExecutor.cs",
    "$root\src\SharpUpSQL.Core\Helpers\InstanceHelper.cs",
    "$root\src\SharpUpSQL.Core\Helpers\InstanceParser.cs",
    "$root\src\SharpUpSQL.Core\Helpers\LuhnHelper.cs",
    "$root\src\SharpUpSQL.Core\Helpers\ExceptionFormatter.cs",
    "$root\src\SharpUpSQL.Core\Helpers\SqlReservedAliasGuard.cs",
    "$root\src\SharpUpSQL.Core\Helpers\ServerAddressHelper.cs",
    "$root\src\SharpUpSQL.Core\Helpers\SubnetHelper.cs",
    "$root\src\SharpUpSQL.Core\LinkedChain\LinkedChainQueryBuilder.cs",
    "$root\src\SharpUpSQL.Core\LinkedChain\RpcManager.cs",
    "$root\src\SharpUpSQL.Core\Output\VerboseWriter.cs",
    "$root\src\SharpUpSQL.Core\Output\PipelineObject.cs",
    "$root\src\SharpUpSQL.Core\Output\JsonPipeline.cs",
    "$root\src\SharpUpSQL.Core\Threading\ThreadPoolRunner.cs"
)

& $csc /nologo /target:library /out:"$coreOut\SharpUpSQL.Core.dll" /platform:anycpu @coreRefs @coreSources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building SharpUpSQL..."
$libSources = @(
    "$root\src\SharpUpSQL\Properties\AssemblyInfo.cs",
    "$root\src\SharpUpSQL\Attack\InvokeSqlDumpInfo.cs",
    "$root\src\SharpUpSQL\Attack\OsCommandResults.cs",
    "$root\src\SharpUpSQL\Attack\OsCommandEngine.cs",
    "$root\src\SharpUpSQL\Attack\OsCommandCommands.cs",
    "$root\src\SharpUpSQL\Attack\ClrDllGenerator.cs",
    "$root\src\SharpUpSQL\Attack\AgentJobEngine.cs",
    "$root\src\SharpUpSQL\Attack\XpDllGenerator.cs",
    "$root\src\SharpUpSQL\Attack\XpDllTemplateData.cs",
    "$root\src\SharpUpSQL\Attack\TokenManipulationHelper.cs",
    "$root\src\SharpUpSQL\Attack\ServiceImpersonationCommands.cs",
    "$root\src\SharpUpSQL\Attack\AttackHelperCommands.cs",
    "$root\src\SharpUpSQL\Audit\AuditCommands.cs",
    "$root\src\SharpUpSQL\Audit\AuditContext.cs",
    "$root\src\SharpUpSQL\Audit\AuditEngine.cs",
    "$root\src\SharpUpSQL\Audit\DefaultPasswordCatalog.cs",
    "$root\src\SharpUpSQL\Audit\SqlAuditResult.cs",
    "$root\src\SharpUpSQL\Commands\ISharpUpSqlCommand.cs",
    "$root\src\SharpUpSQL\Commands\InstanceTargetResolver.cs",
    "$root\src\SharpUpSQL\Commands\SharpUpSqlContext.cs",
    "$root\src\SharpUpSQL\Commands\SqlInstanceCommandBase.cs",
    "$root\src\SharpUpSQL\Common\CommonCommands.cs",
    "$root\src\SharpUpSQL\Common\CommonResults.cs",
    "$root\src\SharpUpSQL\Common\GetSqlServiceLocal.cs",
    "$root\src\SharpUpSQL\Common\LinkServerEngine.cs",
    "$root\src\SharpUpSQL\Common\LocalChecks.cs",
    "$root\src\SharpUpSQL\Common\SqlEnumerationEngine.cs",
    "$root\src\SharpUpSQL\Common\SqlEnumerationEngine2.cs",
    "$root\src\SharpUpSQL\Core\GetSqlConnectionTest.cs",
    "$root\src\SharpUpSQL\Core\GetSqlConnectionTestThreaded.cs",
    "$root\src\SharpUpSQL\Core\GetSqlQuery.cs",
    "$root\src\SharpUpSQL\Core\GetSqlQueryThreaded.cs",
    "$root\src\SharpUpSQL\Discovery\GetSqlInstanceBroadcast.cs",
    "$root\src\SharpUpSQL\Discovery\GetSqlInstanceFile.cs",
    "$root\src\SharpUpSQL\Discovery\GetSqlInstanceLocal.cs",
    "$root\src\SharpUpSQL\Discovery\SqlUdpScanner.cs",
    "$root\src\SharpUpSQL\Domain\GetDomainObject.cs",
    "$root\src\SharpUpSQL\Domain\GetDomainSpn.cs",
    "$root\src\SharpUpSQL\Domain\DomainCommands.cs",
    "$root\src\SharpUpSQL\AdRecon\DomainReconEngine.cs",
    "$root\src\SharpUpSQL\AdRecon\DomainReconResults.cs",
    "$root\src\SharpUpSQL\AdRecon\DomainReconCommands.cs",
    "$root\src\SharpUpSQL\AdRecon\ExploitCatalog.cs",
    "$root\src\SharpUpSQL\Enhancements\EnhancementCommands.cs",
    "$root\src\SharpUpSQL\Persistence\PersistenceEngine.cs",
    "$root\src\SharpUpSQL\Persistence\PersistenceCommands.cs",
    "$root\src\SharpUpSQL\PasswordRecovery\PasswordRecoveryEngine.cs",
    "$root\src\SharpUpSQL\PasswordRecovery\PasswordRecoveryCommands.cs"
)

& $csc /nologo /target:library /out:"$libOut\SharpUpSQL.dll" /platform:anycpu "/reference:$coreOut\SharpUpSQL.Core.dll" @libRefs @libSources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building SharpUpSQL.Cli..."
$cliSources = @(
    "$root\src\SharpUpSQL.Cli\Properties\AssemblyInfo.cs",
    "$root\src\SharpUpSQL.Cli\CliArgumentParser.cs",
    "$root\src\SharpUpSQL.Cli\CommandRegistry.cs",
    "$root\src\SharpUpSQL.Cli\Program.cs",
    "$root\src\SharpUpSQL.Cli\ResultFormatter.cs"
)

& $csc /nologo /target:exe /out:"$cliOut\SharpUpSQL.Cli.exe" /platform:anycpu "/reference:$coreOut\SharpUpSQL.Core.dll" "/reference:$libOut\SharpUpSQL.dll" "/reference:$framework\System.dll" "/reference:$framework\System.Core.dll" @cliSources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item "$libOut\SharpUpSQL.dll" $cliOut -Force
Copy-Item "$coreOut\SharpUpSQL.Core.dll" $cliOut -Force
Copy-Item "$cliOut\SharpUpSQL.Cli.exe" "$cliOut\SharpUpSQL.exe" -Force

Write-Host "Build complete: $cliOut\SharpUpSQL.exe"
Write-Host "Run regression: .\tests\Run-Regression.ps1 -SkipLab"
