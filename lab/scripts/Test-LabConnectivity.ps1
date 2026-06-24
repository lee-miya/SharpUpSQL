# Validates SharpUpSQL lab connectivity and core fixtures.
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\config\lab.settings.json')
)

$ErrorActionPreference = 'Stop'

function Get-LabConfig {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        throw "Lab config not found: $Path. Run Setup-SharpUpSQLLab.ps1 first."
    }
    Get-Content $Path -Raw | ConvertFrom-Json
}

function Invoke-SqlQuery {
    param(
        [string]$Instance,
        [string]$User,
        [string]$Password,
        [string]$Query,
        [string]$SqlCmd
    )
    $args = @('-S', $Instance, '-U', $User, '-P', $Password, '-Q', $Query, '-h', '-1', '-W')
    $out = & $SqlCmd @args 2>&1
    return @{ ExitCode = $LASTEXITCODE; Output = ($out -join "`n") }
}

function Test-Check {
    param([string]$Name, [scriptblock]$Test)
    Write-Host -NoNewline "[$Name] "
    try {
        $result = & $Test
        if ($result) {
            Write-Host 'PASS' -ForegroundColor Green
            return $true
        }
        Write-Host 'FAIL' -ForegroundColor Red
        return $false
    } catch {
        Write-Host "FAIL ($($_.Exception.Message))" -ForegroundColor Red
        return $false
    }
}

$config = Get-LabConfig -Path $ConfigPath
$sqlcmd = $config.SqlCmdPath
if (-not (Get-Command $sqlcmd -ErrorAction SilentlyContinue)) {
    $sqlcmd = 'sqlcmd'
}

$passed = 0
$total = 0

$checks = @(
    @{
        Name = 'PRIMARY sa login'
        Test = {
            $r = Invoke-SqlQuery -Instance $config.PrimaryInstance -User 'sa' -Password $config.SaPassword `
                -Query 'SELECT 1' -SqlCmd $sqlcmd
            $r.ExitCode -eq 0
        }
    },
    @{
        Name = 'LINKED sa login'
        Test = {
            $r = Invoke-SqlQuery -Instance $config.LinkedInstance -User 'sa' -Password $config.SaPassword `
                -Query 'SELECT 1' -SqlCmd $sqlcmd
            $r.ExitCode -eq 0
        }
    },
    @{
        Name = 'Low-priv login (Profile C)'
        Test = {
            $r = Invoke-SqlQuery -Instance $config.PrimaryInstance -User $config.LowPrivLogin `
                -Password $config.LowPrivPassword -Query 'SELECT DB_NAME()' -SqlCmd $sqlcmd
            $r.ExitCode -eq 0
        }
    },
    @{
        Name = 'LabDB exists'
        Test = {
            $r = Invoke-SqlQuery -Instance $config.PrimaryInstance -User 'sa' -Password $config.SaPassword `
                -Query "SELECT name FROM sys.databases WHERE name = '$($config.DatabaseName)'" -SqlCmd $sqlcmd
            $r.ExitCode -eq 0 -and $r.Output -match $config.DatabaseName
        }
    },
    @{
        Name = 'Linked server round-trip'
        Test = {
            $r = Invoke-SqlQuery -Instance $config.PrimaryInstance -User 'sa' -Password $config.SaPassword `
                -Query "SELECT TOP 1 name FROM [$($config.LinkedServerName)].master.sys.databases" -SqlCmd $sqlcmd
            $r.ExitCode -eq 0
        }
    },
    @{
        Name = 'IMPERSONATE fixture'
        Test = {
            $r = Invoke-SqlQuery -Instance $config.PrimaryInstance -User $config.ImpersonateLogin `
                -Password $config.ImpersonatePassword `
                -Query 'SELECT HAS_PERMS_BY_NAME(''sa'', ''LOGIN'', ''IMPERSONATE'')' -SqlCmd $sqlcmd
            $r.ExitCode -eq 0 -and $r.Output -match '1'
        }
    }
)

Write-Host 'SharpUpSQL Lab Connectivity Tests' -ForegroundColor Yellow
foreach ($check in $checks) {
    $total++
    if (Test-Check -Name $check.Name -Test $check.Test) { $passed++ }
}

Write-Host "`nResult: $passed / $total checks passed" -ForegroundColor $(if ($passed -eq $total) { 'Green' } else { 'Yellow' })
if ($passed -lt $total) {
    Write-Host 'Some checks failed. Ensure SQL instances are running and Setup-SharpUpSQLLab.ps1 completed.'
    exit 1
}
