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



New-Item -ItemType Directory -Force -Path $coreOut, $libOut, $cliOut | Out-Null



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

