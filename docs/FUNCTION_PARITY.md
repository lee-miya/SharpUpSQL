# SharpUpSQL Function Parity Matrix

Tracks parity between [PowerUpSQL v1.105.0](https://github.com/NetSPI/PowerUpSQL) (`PowerUpSQL.psd1` `FunctionsToExport`) and SharpUpSQL. For porting goals, architecture, and upgrade workflow, see [PORTING_PLAN.md](PORTING_PLAN.md).

**Source of truth:** PowerUpSQL module manifest — **108 exported functions** (plan groups ~97 core SQL commands; the five `Get-Domain*` / `Create-SQLFile*` / `Get-SQLAssemblyFile` helpers and threaded variants are included here for complete coverage).

## Status Legend

| Symbol | Meaning |
|--------|---------|
| ⬜| Not started |
| 🟡 | In progress |
| ✅ | Implemented and lab-verified |
| ❌ | Blocked / N/A in lab |
| 🔒 | Requires domain-joined lab (see [lab/README.md](../lab/README.md)) |
| ⚠️ | Implemented; lab test pending |

## Summary

| Category | Count | Implemented | Lab PASS |
|----------|------:|------------:|---------:|
| Discovery | 6 | 6 | ⚠️ |
| Core | 5 | 5 | ⚠️ |
| Common / Enumeration | 35 | 35 | ⚠️ |
| Fuzz | 4 | 0 | — |
| AD Recon | 14 | 14 | 🔒 |
| Audit | 16 | 16 | ⚠️ |
| Attack / Escalation | 11 | 11 | ⚠️ |
| Password Recovery | 4 | 4 | ⚠️ |
| Persistence | 3 | 3 | ⚠️ |
| Helper / External | 5 | 5 | ⚠️ |
| **PowerUpSQL total** | **108** | **104** | **⚠️** |
| **Enhancements (Phase 6)** | **8** | **8** | **⚠️** |

**104 / 108** PowerUpSQL commands are registered in the CLI. The four `Get-SQLFuzz*` commands are not yet exposed (fuzz logic is partially used internally by audit). **Lab PASS** requires [lab/README.md](../lab/README.md) Profile A–C; AD functions need Profile D 🔒.

**Regression (CI / local):** `tests\Run-Regression.ps1 -SkipLab` runs build + unit/registry tests without SQL Server. Full lab validation: `tests\Run-Regression.ps1` after lab setup.

## Lab Environment Mapping

Each function row references which lab profile can validate it:

| Profile | Instance | Purpose |
|---------|----------|---------|
| **A** | `SQL-PRIMARY` (`localhost,1433`) | Sysadmin, weak logins, audit fixtures, OS cmd channels |
| **B** | `SQL-LINKED` (`localhost,1434`) | Linked-server target (multi-hop, RPC) |
| **C** | `SQL-LOWPRIV` (login on A) | Low-privilege + `IMPERSONATE` escalation |
| **D** | Domain controller + AD-linked SQL | AD recon via `OPENQUERY` / ADSI 🔒 |

See [lab/README.md](../lab/README.md) for setup.

---

## Discovery (Phase 1)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 1 | `Get-SQLInstanceFile` | 1 | ✅| ⬜| ⬜| ⬜| Local | Parse `sqlservr.exe` / registry paths |
| 2 | `Get-SQLInstanceLocal` | 1 | ✅| ⬜| ⬜| ⬜| Local | Local services / registry |
| 3 | `Get-SQLInstanceDomain` | 1 | ✅| ⬜| ⬜| ⬜| D 🔒 | SPN / AD DNS; stub OK on standalone |
| 4 | `Get-SQLInstanceScanUDP` | 1 | ✅| ⬜| ⬜| ⬜| A,B | UDP 1434 browser |
| 5 | `Get-SQLInstanceScanUDPThreaded` | 1 | ✅| ⬜| ⬜| ⬜| A,B | `-Threads` batch |
| 6 | `Get-SQLInstanceBroadcast` | 1 | ✅| ⬜| ⬜| ⬜| A,B | UDP broadcast discovery |

## Core (Phase 1)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 7 | `Get-SQLConnectionTest` | 1 | ✅| ⬜| ⬜| ⬜| A,B,C | Win + SQL auth |
| 8 | `Get-SQLConnectionTestThreaded` | 1 | ✅| ⬜| ⬜| ⬜| A,B,C | Pipeline / `-Threads` |
| 9 | `Get-SQLQuery` | 1 | ⚠️ | ⬜| ⬜| ⬜| A | Arbitrary T-SQL |
| 10 | `Get-SQLQueryThreaded` | 1 | ⚠️ | ⬜| ⬜| ⬜| A,B | Multi-instance |
| 11 | `Invoke-SQLOSCmd` | 1/4 | ⚠️ | ⬜| ⬜| ⬜| A | xp_cmdshell baseline |

## Common / Enumeration (Phase 2)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 12 | `Get-SQLAgentJob` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | SQL Agent required |
| 13 | `Get-SQLAuditDatabaseSpec` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 14 | `Get-SQLAuditServerSpec` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 15 | `Get-SQLColumn` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 16 | `Get-SQLColumnSampleData` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Luhn CC check |
| 17 | `Get-SQLColumnSampleDataThreaded` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 18 | `Get-SQLDatabase` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 19 | `Get-SQLDatabaseThreaded` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,B | |
| 20 | `Get-SQLDatabasePriv` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | |
| 21 | `Get-SQLDatabaseRole` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 22 | `Get-SQLDatabaseRoleMember` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 23 | `Get-SQLDatabaseSchema` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 24 | `Get-SQLDatabaseUser` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 25 | `Get-SQLServerConfiguration` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 26 | `Get-SQLServerCredential` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 27 | `Get-SQLServerInfo` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Key acceptance cmd |
| 28 | `Get-SQLServerInfoThreaded` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,B | |
| 29 | `Get-SQLServerLink` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 30 | `Get-SQLServerLinkCrawl` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,B | |
| 31 | `Get-SQLServerLinkData` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,B | |
| 32 | `Get-SQLServerLinkQuery` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,B | |
| 33 | `Get-SQLServerLogin` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 34 | `Get-SQLServerLoginDefaultPw` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | sa / default pw list |
| 35 | `Get-SQLServerPolicy` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Policy-Based Mgmt |
| 36 | `Get-SQLServerPriv` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | |
| 37 | `Get-SQLServerRole` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 38 | `Get-SQLServerRoleMember` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 39 | `Get-SQLServiceAccount` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | Local | WMI / registry |
| 40 | `Get-SQLServiceLocal` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | Local | |
| 41 | `Get-SQLSession` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 42 | `Get-SQLStoredProcedure` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 43 | `Get-SQLStoredProcedureCLR` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | CLR procs fixture |
| 44 | `Get-SQLStoredProcedureSQLi` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Dynamic SQL procs |
| 45 | `Get-SQLStoredProcedureAutoExec` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `startup` procs |
| 46 | `Get-SQLStoredProcedureXp` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | xp_* in procs |
| 47 | `Get-SQLSysadminCheck` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | |
| 48 | `Get-SQLTable` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 49 | `Get-SQLTableTemp` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 50 | `Get-SQLTriggerDdl` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 51 | `Get-SQLTriggerDml` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 52 | `Get-SQLView` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 53 | `Get-SQLLocalAdminCheck` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | Local | |
| 54 | `Get-SQLOleDbProvder` | 2 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Typo preserved for parity |

## Fuzz (Phase 2)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 55 | `Get-SQLFuzzDatabaseName` | 2 | ⬜| ⬜| ⬜| ⬜| A | |
| 56 | `Get-SQLFuzzDomainAccount` | 2 | ⬜| ⬜| ⬜| ⬜| D 🔒 | |
| 57 | `Get-SQLFuzzObjectName` | 2 | ⬜| ⬜| ⬜| ⬜| A | |
| 58 | `Get-SQLFuzzServerLogin` | 2 | ⬜| ⬜| ⬜| ⬜| A | |

## AD Recon (Phase 5)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 59 | `Get-SQLDomainObject` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | OPENQUERY LDAP |
| 60 | `Get-SQLDomainComputer` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 61 | `Get-SQLDomainUser` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 62 | `Get-SQLDomainSubnet` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 63 | `Get-SQLDomainSite` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 64 | `Get-SQLDomainGroup` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 65 | `Get-SQLDomainOu` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 66 | `Get-SQLDomainAccountPolicy` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 67 | `Get-SQLDomainTrust` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 68 | `Get-SQLDomainPasswordsLAPS` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | LAPS attribute |
| 69 | `Get-SQLDomainController` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 70 | `Get-SQLDomainExploitableSystem` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |
| 71 | `Get-SQLDomainGroupMember` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | D 🔒 | |

## Audit (Phase 3)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 72 | `Invoke-SQLAudit` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Aggregates 15 checks |
| 73 | `Invoke-SQLAuditPrivCreateProcedure` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | `-Exploit` |
| 74 | `Invoke-SQLAuditPrivDbChaining` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |
| 75 | `Invoke-SQLAuditPrivImpersonateLogin` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | `-Exploit` |
| 76 | `Invoke-SQLAuditPrivServerLink` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,B | `-Exploit` |
| 77 | `Invoke-SQLAuditPrivTrustworthy` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |
| 78 | `Invoke-SQLAuditPrivXpDirtree` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | `-Exploit` |
| 79 | `Invoke-SQLAuditPrivXpFileexist` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | `-Exploit` |
| 80 | `Invoke-SQLAuditRoleDbDdlAdmin` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | `-Exploit` |
| 81 | `Invoke-SQLAuditRoleDbOwner` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | `-Exploit` |
| 82 | `Invoke-SQLAuditSampleDataByColumn` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | Sensitive data |
| 83 | `Invoke-SQLAuditWeakLoginPw` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |
| 84 | `Invoke-SQLAuditSQLiSpExecuteAs` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 85 | `Invoke-SQLAuditSQLiSpSigned` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | |
| 86 | `Invoke-SQLAuditDefaultLoginPw` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |
| 87 | `Invoke-SQLAuditPrivAutoExecSp` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |

## Attack / Escalation (Phase 3–4)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 88 | `Invoke-SQLDumpInfo` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A | CSV/XML export |
| 89 | `Invoke-SQLEscalatePriv` | 3 | ⚠️ | ⬜ | ⬜ | ⬜ | A,C | Decision tree |
| 90 | `Invoke-SQLImpersonateService` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | Service token |
| 91 | `Invoke-SQLImpersonateServiceCmd` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | |
| 92 | `Invoke-SQLOSCmdCLR` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | CLR enabled |
| 93 | `Invoke-SQLOSCmdCOle` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | Ole Automation |
| 94 | `Invoke-SQLOSCmdPython` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | External script |
| 95 | `Invoke-SQLOSCmdR` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | External script |
| 96 | `Invoke-SQLOSCmdAgentJob` | 4 | ⚠️ | ⬜| ⬜| ⬜| A | Agent job cmd |

## Password Recovery (Phase 5)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 97 | `Get-SQLRecoverPwAutoLogon` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | Local | Registry autologon |
| 98 | `Get-SQLServerPasswordHash` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | A | sysadmin |
| 99 | `Invoke-SQLUncPathInjection` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | A | xp_dirtree / Responder |
| 100 | `Invoke-TokenManipulation` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | Local | Win32 API |

## Persistence (Phase 5)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 101 | `Get-SQLPersistRegRun` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |
| 102 | `Get-SQLPersistRegDebugger` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |
| 103 | `Get-SQLPersistTriggerDDL` | 5 | ⚠️ | ⬜ | ⬜ | ⬜ | A | `-Exploit` |

## Helper / External (Phase 4–5)

| # | Function | Phase | C# | Unit | Lab | Snapshot | Lab Profile | Notes |
|--:|----------|------:|:--:|:----:|:---:|:--------:|-------------|-------|
| 104 | `Create-SQLFileXpDll` | 4 | ✅ | ✅ | ⚠️ | ⬜ | Local | xp_cmdshell DLL template |
| 105 | `Create-SQLFileCLRDll` | 4 | ✅ | ✅ | ⚠️ | ⬜ | Local | CLR shell DLL template |
| 106 | `Get-SQLAssemblyFile` | 4 | ✅ | ✅ | ⚠️ | ⬜ | A | Extract assembly |
| 107 | `Get-DomainObject` | 5 | ✅ | ✅ | 🔒 | ⬜ | D 🔒 | Embedded AD helper |
| 108 | `Get-DomainSpn` | 5 | ✅ | ✅ | 🔒 | ⬜ | D 🔒 | Embedded AD helper |

---

## Minimal Enhancements (Phase 6 — not in PowerUpSQL)

| # | Capability | Priority | C# | Lab | Notes |
|--:|------------|----------|:--:|:---:|-------|
| E1 | NTLM Pass-the-Hash auth | P0 | ✅ | ⚠️ | Raw TDS / NTLM via `-Hash` |
| E2 | Linked-server chain execution | P0 | ✅ | ⚠️ | `Invoke-SQLLinkedChainQuery` |
| E3 | Enable/Disable RPC + RPC OUT | P0 | ✅ | ⚠️ | `Invoke-SQLEnableRpc` / `Invoke-SQLDisableRpc` |
| E4 | JSON pipeline I/O | P1 | ✅ | ✅ | `--stdin json` / `--format json` |
| E5 | Custom TCP port | P1 | ✅ | ⚠️ | `-Port` |
| E6 | Advanced connection string opts | P1 | ✅ | ⚠️ | `-Encrypt`, `-ForceNamedPipe`, `-PacketSize` |
| E7 | Debug / verbose SQL preview | P2 | ✅ | ⚠️ | `-Debug` |
| E8 | Comma-separated multi-host CLI | P2 | ✅ | ⚠️ | `-Instance h1,h2[,port]` |

---

## Golden Snapshot Index

Baseline outputs live under [`lab/snapshots/`](../lab/snapshots/). Regenerate after lab setup:

```powershell
.\lab\scripts\Export-GoldenSnapshots.ps1 -InstancePrimary "localhost,1433"
```

| Snapshot file | Command | Phase |
|---------------|---------|------:|
| `discovery/Get-SQLInstanceLocal.txt` | `Get-SQLInstanceLocal` | 1 |
| `core/Get-SQLConnectionTest.txt` | `Get-SQLConnectionTest -Instance localhost,1433` | 1 |
| `core/Get-SQLServerInfo.txt` | `Get-SQLServerInfo -Instance localhost,1433` | 2 |
| `common/Get-SQLDatabase.txt` | `Get-SQLDatabase -Instance localhost,1433` | 2 |
| `audit/Invoke-SQLAudit.txt` | `Invoke-SQLAudit -Instance localhost,1433` | 3 |
| `link/Get-SQLServerLinkCrawl.txt` | `Get-SQLServerLinkCrawl -Instance localhost,1433` | 2 |

---

## Update Protocol

When implementing a function:

1. Set **C#** to 🟡 while coding, ✅ when merged.
2. Add/update unit test → **Unit** column.
3. Run against lab profile → **Lab** ✅ or document **❌** / **🔒** reason.
4. Diff CLI output vs golden snapshot → **Snapshot** ✅
5. Update summary counts at top of this file.

_Last updated: Phase 7 — regression suite (`tests/Run-Regression.ps1`), README, COMMAND_REFERENCE.md; 104/108 PowerUpSQL CLI commands + 8 enhancements._
