# SharpUpSQL lab setup orchestrator.
# Applies T-SQL fixtures to PRIMARY and LINKED instances.
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\config\lab.settings.json'),
    [switch]$SkipLinkedServer,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$LabRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$SqlDir = Join-Path $LabRoot 'sql'

function Get-LabConfig {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        $example = Join-Path (Split-Path $Path) 'lab.settings.json.example'
        if (Test-Path $example) {
            Write-Warning "Config not found. Copying example to $Path"
            Copy-Item $example $Path
        } else {
            throw "Lab config not found: $Path"
        }
    }
    Get-Content $Path -Raw | ConvertFrom-Json
}

function Invoke-SqlFile {
    param(
        [string]$Instance,
        [string]$SaPassword,
        [string]$FilePath,
        [string]$SqlCmd
    )
    if (-not (Test-Path $FilePath)) {
        throw "SQL file not found: $FilePath"
    }
    Write-Host "  -> $(Split-Path $FilePath -Leaf) on $Instance" -ForegroundColor Cyan
    if ($WhatIf) { return }

    $args = @(
        '-S', $Instance,
        '-U', 'sa',
        '-P', $SaPassword,
        '-b',           # abort on error
        '-i', $FilePath
    )
    & $SqlCmd @args
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed ($LASTEXITCODE) for $FilePath on $Instance"
    }
}

function Initialize-Instance {
    param(
        [string]$Instance,
        [string]$SaPassword,
        [string]$SqlCmd,
        [string[]]$Scripts
    )
    Write-Host "`nInitializing instance: $Instance" -ForegroundColor Green
    foreach ($script in $Scripts) {
        Invoke-SqlFile -Instance $Instance -SaPassword $SaPassword -FilePath $script -SqlCmd $SqlCmd
    }
}

# Resolve sqlcmd
$config = Get-LabConfig -Path $ConfigPath
$sqlcmd = $config.SqlCmdPath
if (-not (Get-Command $sqlcmd -ErrorAction SilentlyContinue)) {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE",
        "${env:ProgramFiles}\Microsoft SQL Server\150\Tools\Binn\SQLCMD.EXE",
        "${env:ProgramFiles(x86)}\Microsoft SQL Server\150\Tools\Binn\SQLCMD.EXE",
        "${env:ProgramFiles}\SqlCmd\sqlcmd.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $sqlcmd = $c; break }
    }
}
if (-not (Get-Command $sqlcmd -ErrorAction SilentlyContinue) -and -not (Test-Path $sqlcmd)) {
    throw @"
sqlcmd not found. Install SQL Server command-line tools:
  winget install Microsoft.Sqlcmd
  winget install Microsoft.SQLServerManagementStudio
Then re-run this script.
"@
}

Write-Host 'SharpUpSQL Lab Setup' -ForegroundColor Yellow
Write-Host "  PRIMARY: $($config.PrimaryInstance)"
Write-Host "  LINKED:  $($config.LinkedInstance)"
Write-Host "  sqlcmd:  $sqlcmd"

$baseScripts = @(
    (Join-Path $SqlDir '01-logins-and-roles.sql'),
    (Join-Path $SqlDir '02-sample-database.sql'),
    (Join-Path $SqlDir '03-audit-fixtures.sql'),
    (Join-Path $SqlDir '05-agent-job.sql')
)

Initialize-Instance -Instance $config.PrimaryInstance -SaPassword $config.SaPassword -SqlCmd $sqlcmd -Scripts $baseScripts
Initialize-Instance -Instance $config.LinkedInstance  -SaPassword $config.SaPassword -SqlCmd $sqlcmd -Scripts $baseScripts

if (-not $SkipLinkedServer) {
    Write-Host "`nConfiguring linked server on PRIMARY" -ForegroundColor Green
    Invoke-SqlFile -Instance $config.PrimaryInstance -SaPassword $config.SaPassword `
        -FilePath (Join-Path $SqlDir '04-linked-server.sql') -SqlCmd $sqlcmd
}

Write-Host "`nLab setup complete. Run Test-LabConnectivity.ps1 to verify." -ForegroundColor Green
