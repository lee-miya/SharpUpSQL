# Exports PowerUpSQL command output as golden snapshots for parity diffing.
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\config\lab.settings.json'),
    [string]$InstancePrimary,
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\snapshots'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Get-LabConfig {
    param([string]$Path)
    if (Test-Path $Path) {
        Get-Content $Path -Raw | ConvertFrom-Json
    } else {
        [PSCustomObject]@{
            PrimaryInstance = 'localhost,1433'
            SaPassword      = 'LabAdmin123!'
        }
    }
}

function Export-Snapshot {
    param(
        [string]$RelativePath,
        [scriptblock]$Command,
        [string]$BaseDir
    )
    $fullPath = Join-Path $BaseDir $RelativePath
    $dir = Split-Path $fullPath -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    if ((Test-Path $fullPath) -and -not $Force) {
        Write-Host "  skip (exists): $RelativePath" -ForegroundColor DarkGray
        return
    }
    Write-Host "  capture: $RelativePath" -ForegroundColor Cyan
    $output = & $Command
    $header = @(
        "# PowerUpSQL golden snapshot",
        "# Captured: $(Get-Date -Format 'o')",
        "# Command: $($Command.ToString().Trim())",
        ""
    )
    ($header + ($output | Out-String).TrimEnd()) | Set-Content -Path $fullPath -Encoding UTF8
}

$config = Get-LabConfig -Path $ConfigPath
$instance = if ($InstancePrimary) { $InstancePrimary } else { $config.PrimaryInstance }

if (-not (Get-Module -ListAvailable -Name PowerUpSQL)) {
    Write-Host 'Installing PowerUpSQL module from PSGallery...' -ForegroundColor Yellow
    Install-Module PowerUpSQL -Scope CurrentUser -Force -AllowClobber
}
Import-Module PowerUpSQL -Force

Write-Host "Exporting golden snapshots for instance: $instance" -ForegroundColor Yellow
Write-Host "Output directory: $OutputDir"

$snapshots = @(
    @{ Path = 'discovery/Get-SQLInstanceLocal.txt';     Cmd = { Get-SQLInstanceLocal | Format-Table -AutoSize | Out-String } },
    @{ Path = 'discovery/Get-SQLInstanceFile.txt';      Cmd = { Get-SQLInstanceFile | Format-Table -AutoSize | Out-String } },
    @{ Path = 'core/Get-SQLConnectionTest.txt';         Cmd = { Get-SQLConnectionTest -Instance $instance | Format-List | Out-String } },
    @{ Path = 'core/Get-SQLQuery.txt';                  Cmd = { Get-SQLQuery -Instance $instance -Query 'SELECT @@VERSION AS Version' | Format-Table -AutoSize | Out-String } },
    @{ Path = 'common/Get-SQLServerInfo.txt';           Cmd = { Get-SQLServerInfo -Instance $instance | Format-List | Out-String } },
    @{ Path = 'common/Get-SQLDatabase.txt';             Cmd = { Get-SQLDatabase -Instance $instance | Format-Table -AutoSize | Out-String } },
    @{ Path = 'common/Get-SQLTable.txt';                Cmd = { Get-SQLTable -Instance $instance -Database LabDB | Format-Table -AutoSize | Out-String } },
    @{ Path = 'common/Get-SQLColumn.txt';               Cmd = { Get-SQLColumn -Instance $instance -Database LabDB -Table Customers | Format-Table -AutoSize | Out-String } },
    @{ Path = 'common/Get-SQLServerLogin.txt';          Cmd = { Get-SQLServerLogin -Instance $instance | Format-Table -AutoSize | Out-String } },
    @{ Path = 'common/Get-SQLServerLink.txt';           Cmd = { Get-SQLServerLink -Instance $instance | Format-Table -AutoSize | Out-String } },
    @{ Path = 'link/Get-SQLServerLinkCrawl.txt';        Cmd = { Get-SQLServerLinkCrawl -Instance $instance | Format-Table -AutoSize | Out-String } },
    @{ Path = 'audit/Invoke-SQLAudit.txt';               Cmd = { Invoke-SQLAudit -Instance $instance | Format-Table -AutoSize | Out-String } },
    @{ Path = 'audit/Invoke-SQLAuditWeakLoginPw.txt';   Cmd = { Invoke-SQLAuditWeakLoginPw -Instance $instance | Format-Table -AutoSize | Out-String } },
    @{ Path = 'enum/Get-SQLStoredProcedure.txt';        Cmd = { Get-SQLStoredProcedure -Instance $instance -Database LabDB | Format-Table -AutoSize | Out-String } },
    @{ Path = 'enum/Get-SQLSysadminCheck.txt';          Cmd = { Get-SQLSysadminCheck -Instance $instance | Format-List | Out-String } }
)

foreach ($snap in $snapshots) {
    Export-Snapshot -RelativePath $snap.Path -Command $snap.Cmd -BaseDir $OutputDir
}

# Write manifest
$manifest = @{
    generatedAt = (Get-Date -Format 'o')
    instance    = $instance
    powerUpSql  = (Get-Module PowerUpSQL).Version.ToString()
    snapshots   = $snapshots.Path
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $OutputDir 'manifest.json') -Encoding UTF8

Write-Host "`nGolden snapshots exported to $OutputDir" -ForegroundColor Green
Write-Host 'Compare SharpUpSQL CLI output against these files during parity testing.'
