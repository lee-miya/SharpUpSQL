# SharpUpSQL Lab Environment

Isolated SQL Server lab for validating SharpUpSQL parity with PowerUpSQL. Supports **standalone Windows** (recommended for .NET Framework 4.8) and optional **Docker** on hosts that have it.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Lab Host (Windows 10/11 or Server 2019+)                       │
│                                                                 │
│  SQL-PRIMARY (Profile A)          SQL-LINKED (Profile B)        │
│  localhost:1433                   localhost:1434                  │
│  sa / LabAdmin123!                sa / LabAdmin123!             │
│  ├─ LabDB (fixtures)              └─ LinkedTargetDB               │
│  ├─ Weak logins (audit)           Linked from PRIMARY             │
│  ├─ IMPERSONATE login (Profile C)                               │
│  └─ Linked server → SQL-LINKED                                    │
│                                                                 │
│  Optional Profile D: Domain-joined DC + SQL for AD recon 🔒      │
└─────────────────────────────────────────────────────────────────┘
```

| Profile | Connection | Login | Tests |
|---------|------------|-------|-------|
| **A** — Primary | `localhost,1433` | `sa` / `LabAdmin123!` | Sysadmin enum, audit, OS cmd, dump |
| **B** — Linked | `localhost,1434` | `sa` / `LabAdmin123!` | Link crawl, RPC, multi-hop |
| **C** — Low priv | `localhost,1433` | `lowpriv_reader` / `WeakPass123!` | Impersonate, DbOwner escalation |
| **D** — Domain 🔒 | Domain SQL instance | Domain admin | 14× `Get-SQLDomain*` functions |

## Prerequisites

- Windows 10/11 or Windows Server 2019+
- [SQL Server 2022 Developer](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (free) **or** Express
- PowerShell 5.1+
- For Profile D: Active Directory lab (e.g. [GOAD](https://github.com/Orange-Cyberdefense/GOAD), [DetectionLab](https://github.com/clong/DetectionLab))

### Tools

| Tool | Purpose |
|------|---------|
| `sqlcmd` | Runs T-SQL init scripts (ships with SSMS / SQL tools) |
| PowerUpSQL module | Golden snapshot baseline |
| Responder / Inveigh (optional) | UNC hash capture tests |

Install SQL command-line tools if missing:

```powershell
# SQL Server 2022 Command Line Utilities (sqlcmd)
winget install Microsoft.Sqlcmd
# Or install SSMS which includes sqlcmd
winget install Microsoft.SQLServerManagementStudio
```

## Quick Start (Native Windows — Two Named Instances)

### 1. Install instances

Install **two named instances** on one machine:

| Instance | TCP Port | Suggested name |
|----------|----------|----------------|
| Primary | 1433 | `MSSQLSERVER` (default) or `PRIMARY` |
| Linked target | 1434 | `LINKED` |

Enable TCP/IP and set fixed ports in **SQL Server Configuration Manager** → *Protocols for &lt;instance&gt;* → TCP/IP → IPAll.

Enable **SQL Server Browser** if using named instances without fixed ports.

### 2. Configure lab

Copy and edit config:

```powershell
Copy-Item .\lab\config\lab.settings.json.example .\lab\config\lab.settings.json
```

Run setup (elevated PowerShell from repo root):

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\lab\scripts\Setup-SharpUpSQLLab.ps1
```

This applies all scripts in `lab/sql/` to PRIMARY and LINKED, creates logins, weak configs, linked server, and sample data.

### 3. Verify

```powershell
.\lab\scripts\Test-LabConnectivity.ps1
```

Expected: all four checks PASS (PRIMARY sa, LINKED sa, lowpriv, linked-server round-trip).

### 4. Capture PowerUpSQL golden snapshots

```powershell
Install-Module PowerUpSQL -Scope CurrentUser -Force
.\lab\scripts\Export-GoldenSnapshots.ps1
```

Outputs go to `lab/snapshots/` for diffing against SharpUpSQL.

## Alternative: Docker (Optional)

If Docker Desktop is available:

```powershell
cd lab
docker compose up -d
# Wait ~30s for SQL Server startup, then:
docker compose exec sql-primary /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "LabAdmin123!" -C -i /scripts/00-master-init.sql
```

See [`docker-compose.yml`](docker-compose.yml). Linux containers use the same T-SQL fixtures; AD recon (Profile D) still requires a Windows domain lab.

## SQL Scripts (execution order)

| Script | Purpose |
|--------|---------|
| [`sql/00-master-init.sql`](sql/00-master-init.sql) | Orchestrator — calls all below |
| [`sql/01-logins-and-roles.sql`](sql/01-logins-and-roles.sql) | sa policy, lowpriv, weak passwords |
| [`sql/02-sample-database.sql`](sql/02-sample-database.sql) | LabDB, tables, sample PII for column audit |
| [`sql/03-audit-fixtures.sql`](sql/03-audit-fixtures.sql) | Trustworthy, chaining, xp probes, signed proc |
| [`sql/04-linked-server.sql`](sql/04-linked-server.sql) | LINKED_SRV → SQL-LINKED |
| [`sql/05-agent-job.sql`](sql/05-agent-job.sql) | Sample SQL Agent job (if Agent running) |

## Lab Credentials (lab use only)

| Login | Password | Role |
|-------|----------|------|
| `sa` | `LabAdmin123!` | sysadmin on A and B |
| `lowpriv_reader` | `WeakPass123!` | public + db_datareader; IMPERSONATE target |
| `impersonate_sa` | `Impersonate1!` | Has IMPERSONATE on `sa` (Profile C) |
| `weak_sa_guess` | `sa` | Weak password for audit tests |

**Do not use these passwords outside an isolated lab.**

## Escalation Scenarios Configured

| Scenario | Fixture location | Validating functions |
|----------|------------------|----------------------|
| IMPERSONATE login | `impersonate_sa` → `sa` | `Invoke-SQLAuditPrivImpersonateLogin`, `Invoke-SQLEscalatePriv` |
| DbOwner low user | `LabDB` role member | `Invoke-SQLAuditRoleDbOwner` |
| Trustworthy + EXECUTE AS | `LabDB` flag | `Invoke-SQLAuditPrivTrustworthy` |
| Db chaining | `LabDB` + cross-db | `Invoke-SQLAuditPrivDbChaining` |
| Linked server priv | `LINKED_SRV` self-credential | `Invoke-SQLAuditPrivServerLink` |
| xp_dirtree / fileexist | GRANT to lowpriv | `Invoke-SQLAuditPrivXpDirtree`, `PrivXpFileexit` |
| Weak / default passwords | `weak_sa_guess`, login list | `Invoke-SQLAuditWeakLoginPw`, `DefaultLoginPw` |

## Profile D — Active Directory Extension

For the 14 `Get-SQLDomain*` functions and `Get-DomainObject` / `Get-DomainSpn`:

1. Join lab host or a VM to an AD domain.
2. Install SQL Server as a domain service account (or use existing domain SQL).
3. Ensure `Ad Hoc Distributed Queries` is enabled for OPENQUERY LDAP:
   ```sql
   EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
   EXEC sp_configure 'Ad Hoc Distributed Queries', 1; RECONFIGURE;
   ```
4. Run domain snapshot commands and store under `lab/snapshots/ad/`.

Mark AD-dependent rows as 🔒 in [`docs/FUNCTION_PARITY.md`](../docs/FUNCTION_PARITY.md) until Profile D is available.

## Troubleshooting

| Issue | Fix |
|-------|-----|
| `sqlcmd` not found | Install SQL tools (see Prerequisites) |
| TCP connection refused | Enable TCP/IP, fixed port, restart instance |
| Linked server login failed | Re-run `04-linked-server.sql`; check SQL-LINKED is on 1434 |
| Agent job script skipped | Start SQL Server Agent service |
| CLR/OLE tests fail | Enable CLR / Ole Automation via `03-audit-fixtures.sql` |

## Security Notice

This lab intentionally contains **vulnerable SQL Server configurations** for offensive-security testing. Run only on isolated networks. Never expose lab instances to the internet or production VLANs.
