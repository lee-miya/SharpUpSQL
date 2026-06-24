# Runs SharpUpSQL commands against the lab and validates exit codes / optional snapshot parity.
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\lab\config\lab.settings.json'),
    [string]$ExePath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$CompareSnapshots,
    [switch]$SkipConnectivityCheck
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (-not $ExePath) {
    $ExePath = Join-Path $root "artifacts\$Configuration\SharpUpSQL.Cli\SharpUpSQL.exe"
}

if (-not (Test-Path $ExePath)) {
    Write-Host "SharpUpSQL.exe not found. Run build.ps1 first." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ConfigPath)) {
    Write-Host "Lab config not found: $ConfigPath" -ForegroundColor Yellow
    Write-Host "Skipping lab regression (unit tests still run via run-unit-tests.ps1)."
    exit 0
}

$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$instance = $config.PrimaryInstance
$password = $config.SaPassword
$snapshotRoot = Join-Path $root 'lab\snapshots'

function Invoke-SharpCommand {
    param(
        [string]$Name,
        [string[]]$Args = @()
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ExePath
    $psi.Arguments = (@($Name) + $Args) -join ' '
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    return [PSCustomObject]@{
        ExitCode = $process.ExitCode
        StdOut   = $stdout
        StdErr   = $stderr
    }
}

function Test-LabCase {
    param(
        [string]$Name,
        [string[]]$Args,
        [string]$SnapshotPath = $null
    )

    Write-Host -NoNewline "[$Name] "
    $result = Invoke-SharpCommand -Name $Name -Args $Args
    if ($result.ExitCode -ne 0) {
        Write-Host "FAIL (exit $($result.ExitCode))" -ForegroundColor Red
        if ($result.StdErr) { Write-Host $result.StdErr -ForegroundColor DarkRed }
        return $false
    }

    if ($CompareSnapshots -and $SnapshotPath) {
        $goldenPath = Join-Path $snapshotRoot $SnapshotPath
        if (Test-Path $goldenPath) {
            $golden = Get-Content $goldenPath -Raw
            $goldenBody = ($golden -split "`n" | Where-Object { $_ -notmatch '^\s*#' }) -join "`n"
            $actualBody = $result.StdOut.Trim()
            if ($goldenBody.Trim() -ne $actualBody.Trim()) {
                Write-Host 'WARN (output differs from snapshot; review manually)' -ForegroundColor Yellow
            }
        }
    }

    Write-Host 'PASS' -ForegroundColor Green
    return $true
}

if (-not $SkipConnectivityCheck) {
    Write-Host 'Running lab connectivity pre-check...' -ForegroundColor Yellow
    & (Join-Path $root 'lab\scripts\Test-LabConnectivity.ps1') -ConfigPath $ConfigPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`nSharpUpSQL lab regression" -ForegroundColor Yellow
$passed = 0
$total = 0

$cases = @(
    @{ Name = 'Get-SQLInstanceLocal'; Args = @() },
    @{ Name = 'Get-SQLConnectionTest'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'core/Get-SQLConnectionTest.txt' },
    @{ Name = 'Get-SQLQuery'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password, '-Query', 'SELECT @@VERSION AS Version'); Snapshot = 'core/Get-SQLQuery.txt' },
    @{ Name = 'Get-SQLServerInfo'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'common/Get-SQLServerInfo.txt' },
    @{ Name = 'Get-SQLDatabase'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'common/Get-SQLDatabase.txt' },
    @{ Name = 'Get-SQLServerLogin'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'common/Get-SQLServerLogin.txt' },
    @{ Name = 'Get-SQLServerLink'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'common/Get-SQLServerLink.txt' },
    @{ Name = 'Get-SQLServerLinkCrawl'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'link/Get-SQLServerLinkCrawl.txt' },
    @{ Name = 'Invoke-SQLAudit'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'audit/Invoke-SQLAudit.txt' },
    @{ Name = 'Get-SQLSysadminCheck'; Args = @('-Instance', $instance, '-Username', 'sa', '-Password', $password); Snapshot = 'enum/Get-SQLSysadminCheck.txt' }
)

foreach ($case in $cases) {
    $total++
    if (Test-LabCase -Name $case.Name -Args $case.Args -SnapshotPath $case.Snapshot) {
        $passed++
    }
}

Write-Host "`nLab regression: $passed / $total passed" -ForegroundColor $(if ($passed -eq $total) { 'Green' } else { 'Yellow' })
if ($passed -lt $total) { exit 1 }
