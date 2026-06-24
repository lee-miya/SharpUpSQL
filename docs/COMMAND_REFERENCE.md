# SharpUpSQL Command Reference

PowerUpSQL v1.105.0 ↔ SharpUpSQL CLI command对照表. Command names are **case-insensitive**.

**Status:** ✅ Implemented in CLI · ⬜ Not yet implemented · 🔒 Requires domain-joined lab (Profile D)

## How to run

```text
SharpUpSQL.exe <CommandName> [-Instance <target>] [-Username <u>] [-Password <p>] [options]
```

PowerShell pipeline equivalent:

```powershell
Get-SQLInstanceDomain | Invoke-SQLAudit
```

```text
SharpUpSQL.exe Get-SQLInstanceDomain --format json | SharpUpSQL.exe Invoke-SQLAudit --stdin json
```

---

## Discovery

| PowerUpSQL | SharpUpSQL | Status | Notes |
|------------|------------|--------|-------|
| `Get-SQLInstanceFile` | `Get-SQLInstanceFile` | ✅ | Parse `sqlservr.exe` / registry paths |
| `Get-SQLInstanceLocal` | `Get-SQLInstanceLocal` | ✅ | Local services / registry |
| `Get-SQLInstanceDomain` | `Get-SQLInstanceDomain` | ✅ | SPN / AD DNS |
| `Get-SQLInstanceScanUDP` | `Get-SQLInstanceScanUDP` | ✅ | UDP 1434 browser |
| `Get-SQLInstanceScanUDPThreaded` | `Get-SQLInstanceScanUDPThreaded` | ✅ | `-Threads` batch |
| `Get-SQLInstanceBroadcast` | `Get-SQLInstanceBroadcast` | ✅ | UDP broadcast |

## Core

| PowerUpSQL | SharpUpSQL | Status | Notes |
|------------|------------|--------|-------|
| `Get-SQLConnectionTest` | `Get-SQLConnectionTest` | ✅ | Win + SQL auth; `-Hash` for PtH |
| `Get-SQLConnectionTestThreaded` | `Get-SQLConnectionTestThreaded` | ✅ | Pipeline / `-Threads` |
| `Get-SQLQuery` | `Get-SQLQuery` | ✅ | Arbitrary T-SQL; `-Debug` |
| `Get-SQLQueryThreaded` | `Get-SQLQueryThreaded` | ✅ | Multi-instance |
| `Invoke-SQLOSCmd` | `Invoke-SQLOSCmd` | ✅ | xp_cmdshell baseline |

## Common / Enumeration

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Get-SQLAgentJob` | `Get-SQLAgentJob` | ✅ |
| `Get-SQLAuditDatabaseSpec` | `Get-SQLAuditDatabaseSpec` | ✅ |
| `Get-SQLAuditServerSpec` | `Get-SQLAuditServerSpec` | ✅ |
| `Get-SQLColumn` | `Get-SQLColumn` | ✅ |
| `Get-SQLColumnSampleData` | `Get-SQLColumnSampleData` | ✅ |
| `Get-SQLColumnSampleDataThreaded` | `Get-SQLColumnSampleDataThreaded` | ✅ |
| `Get-SQLDatabase` | `Get-SQLDatabase` | ✅ |
| `Get-SQLDatabaseThreaded` | `Get-SQLDatabaseThreaded` | ✅ |
| `Get-SQLDatabasePriv` | `Get-SQLDatabasePriv` | ✅ |
| `Get-SQLDatabaseRole` | `Get-SQLDatabaseRole` | ✅ |
| `Get-SQLDatabaseRoleMember` | `Get-SQLDatabaseRoleMember` | ✅ |
| `Get-SQLDatabaseSchema` | `Get-SQLDatabaseSchema` | ✅ |
| `Get-SQLDatabaseUser` | `Get-SQLDatabaseUser` | ✅ |
| `Get-SQLServerConfiguration` | `Get-SQLServerConfiguration` | ✅ |
| `Get-SQLServerCredential` | `Get-SQLServerCredential` | ✅ |
| `Get-SQLServerInfo` | `Get-SQLServerInfo` | ✅ |
| `Get-SQLServerInfoThreaded` | `Get-SQLServerInfoThreaded` | ✅ |
| `Get-SQLServerLink` | `Get-SQLServerLink` | ✅ |
| `Get-SQLServerLinkCrawl` | `Get-SQLServerLinkCrawl` | ✅ |
| `Get-SQLServerLinkData` | `Get-SQLServerLinkData` | ✅ |
| `Get-SQLServerLinkQuery` | `Get-SQLServerLinkQuery` | ✅ |
| `Get-SQLServerLogin` | `Get-SQLServerLogin` | ✅ |
| `Get-SQLServerLoginDefaultPw` | `Get-SQLServerLoginDefaultPw` | ✅ |
| `Get-SQLServerPolicy` | `Get-SQLServerPolicy` | ✅ |
| `Get-SQLServerPriv` | `Get-SQLServerPriv` | ✅ |
| `Get-SQLServerRole` | `Get-SQLServerRole` | ✅ |
| `Get-SQLServerRoleMember` | `Get-SQLServerRoleMember` | ✅ |
| `Get-SQLServiceAccount` | `Get-SQLServiceAccount` | ✅ |
| `Get-SQLServiceLocal` | `Get-SQLServiceLocal` | ✅ |
| `Get-SQLSession` | `Get-SQLSession` | ✅ |
| `Get-SQLStoredProcedure` | `Get-SQLStoredProcedure` | ✅ |
| `Get-SQLStoredProcedureCLR` | `Get-SQLStoredProcedureCLR` | ✅ |
| `Get-SQLStoredProcedureSQLi` | `Get-SQLStoredProcedureSQLi` | ✅ |
| `Get-SQLStoredProcedureAutoExec` | `Get-SQLStoredProcedureAutoExec` | ✅ |
| `Get-SQLStoredProcedureXp` | `Get-SQLStoredProcedureXp` | ✅ |
| `Get-SQLSysadminCheck` | `Get-SQLSysadminCheck` | ✅ |
| `Get-SQLTable` | `Get-SQLTable` | ✅ |
| `Get-SQLTableTemp` | `Get-SQLTableTemp` | ✅ |
| `Get-SQLTriggerDdl` | `Get-SQLTriggerDdl` | ✅ |
| `Get-SQLTriggerDml` | `Get-SQLTriggerDml` | ✅ |
| `Get-SQLView` | `Get-SQLView` | ✅ |
| `Get-SQLLocalAdminCheck` | `Get-SQLLocalAdminCheck` | ✅ |
| `Get-SQLOleDbProvder` | `Get-SQLOleDbProvder` | ✅ |

## Fuzz

| PowerUpSQL | SharpUpSQL | Status | Notes |
|------------|------------|--------|-------|
| `Get-SQLFuzzDatabaseName` | — | ⬜ | Planned; internal fuzz used by audit |
| `Get-SQLFuzzDomainAccount` | — | ⬜ | Planned |
| `Get-SQLFuzzObjectName` | — | ⬜ | Planned |
| `Get-SQLFuzzServerLogin` | — | ⬜ | Planned |

## AD Recon (via SQL OPENQUERY / ADSI)

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Get-SQLDomainObject` | `Get-SQLDomainObject` | ✅ 🔒 |
| `Get-SQLDomainComputer` | `Get-SQLDomainComputer` | ✅ 🔒 |
| `Get-SQLDomainUser` | `Get-SQLDomainUser` | ✅ 🔒 |
| `Get-SQLDomainSubnet` | `Get-SQLDomainSubnet` | ✅ 🔒 |
| `Get-SQLDomainSite` | `Get-SQLDomainSite` | ✅ 🔒 |
| `Get-SQLDomainGroup` | `Get-SQLDomainGroup` | ✅ 🔒 |
| `Get-SQLDomainOu` | `Get-SQLDomainOu` | ✅ 🔒 |
| `Get-SQLDomainAccountPolicy` | `Get-SQLDomainAccountPolicy` | ✅ 🔒 |
| `Get-SQLDomainTrust` | `Get-SQLDomainTrust` | ✅ 🔒 |
| `Get-SQLDomainPasswordsLAPS` | `Get-SQLDomainPasswordsLAPS` | ✅ 🔒 |
| `Get-SQLDomainController` | `Get-SQLDomainController` | ✅ 🔒 |
| `Get-SQLDomainExploitableSystem` | `Get-SQLDomainExploitableSystem` | ✅ 🔒 |
| `Get-SQLDomainGroupMember` | `Get-SQLDomainGroupMember` | ✅ 🔒 |

## AD Helpers (direct LDAP — not via SQL)

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Get-DomainObject` | `Get-DomainObject` | ✅ 🔒 |
| `Get-DomainSpn` | `Get-DomainSpn` | ✅ 🔒 |

## Audit

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Invoke-SQLAudit` | `Invoke-SQLAudit` | ✅ |
| `Invoke-SQLAuditPrivCreateProcedure` | `Invoke-SQLAuditPrivCreateProcedure` | ✅ |
| `Invoke-SQLAuditPrivDbChaining` | `Invoke-SQLAuditPrivDbChaining` | ✅ |
| `Invoke-SQLAuditPrivImpersonateLogin` | `Invoke-SQLAuditPrivImpersonateLogin` | ✅ |
| `Invoke-SQLAuditPrivServerLink` | `Invoke-SQLAuditPrivServerLink` | ✅ |
| `Invoke-SQLAuditPrivTrustworthy` | `Invoke-SQLAuditPrivTrustworthy` | ✅ |
| `Invoke-SQLAuditPrivXpDirtree` | `Invoke-SQLAuditPrivXpDirtree` | ✅ |
| `Invoke-SQLAuditPrivXpFileexit` | `Invoke-SQLAuditPrivXpFileexit` | ✅ |
| `Invoke-SQLAuditRoleDbDdlAdmin` | `Invoke-SQLAuditRoleDbDdlAdmin` | ✅ |
| `Invoke-SQLAuditRoleDbOwner` | `Invoke-SQLAuditRoleDbOwner` | ✅ |
| `Invoke-SQLAuditSampleDataByColumn` | `Invoke-SQLAuditSampleDataByColumn` | ✅ |
| `Invoke-SQLAuditWeakLoginPw` | `Invoke-SQLAuditWeakLoginPw` | ✅ |
| `Invoke-SQLAuditSQLiSpExecuteAs` | `Invoke-SQLAuditSQLiSpExecuteAs` | ✅ |
| `Invoke-SQLAuditSQLiSpSigned` | `Invoke-SQLAuditSQLiSpSigned` | ✅ |
| `Invoke-SQLAuditDefaultLoginPw` | `Invoke-SQLAuditDefaultLoginPw` | ✅ |
| `Invoke-SQLAuditPrivAutoExecSp` | `Invoke-SQLAuditPrivAutoExecSp` | ✅ |

## Attack / Escalation

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Invoke-SQLDumpInfo` | `Invoke-SQLDumpInfo` | ✅ |
| `Invoke-SQLEscalatePriv` | `Invoke-SQLEscalatePriv` | ✅ |
| `Invoke-SQLImpersonateService` | `Invoke-SQLImpersonateService` | ✅ |
| `Invoke-SQLImpersonateServiceCmd` | `Invoke-SQLImpersonateServiceCmd` | ✅ |
| `Invoke-SQLOSCmdCLR` | `Invoke-SQLOSCmdCLR` | ✅ |
| `Invoke-SQLOSCmdCOle` | `Invoke-SQLOSCmdCOle` | ✅ |
| `Invoke-SQLOSCmdPython` | `Invoke-SQLOSCmdPython` | ✅ |
| `Invoke-SQLOSCmdR` | `Invoke-SQLOSCmdR` | ✅ |
| `Invoke-SQLOSCmdAgentJob` | `Invoke-SQLOSCmdAgentJob` | ✅ |

## Password Recovery

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Get-SQLRecoverPwAutoLogon` | `Get-SQLRecoverPwAutoLogon` | ✅ |
| `Get-SQLServerPasswordHash` | `Get-SQLServerPasswordHash` | ✅ |
| `Invoke-SQLUncPathInjection` | `Invoke-SQLUncPathInjection` | ✅ |
| `Invoke-TokenManipulation` | `Invoke-TokenManipulation` | ✅ |

## Persistence

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Get-SQLPersistRegRun` | `Get-SQLPersistRegRun` | ✅ |
| `Get-SQLPersistRegDebugger` | `Get-SQLPersistRegDebugger` | ✅ |
| `Get-SQLPersistTriggerDDL` | `Get-SQLPersistTriggerDDL` | ✅ |

## Helper / File generation

| PowerUpSQL | SharpUpSQL | Status |
|------------|------------|--------|
| `Create-SQLFileXpDll` | `Create-SQLFileXpDll` | ✅ |
| `Create-SQLFileCLRDll` | `Create-SQLFileCLRDll` | ✅ |
| `Get-SQLAssemblyFile` | `Get-SQLAssemblyFile` | ✅ |

---

## SharpUpSQL enhancements (not in PowerUpSQL)

| Command | Priority | Description |
|---------|----------|-------------|
| `Invoke-SQLLinkedChainQuery` | P0 | Multi-hop linked-server query (`-LinkPath`, `-Query`, `-ExecAt`) |
| `Invoke-SQLEnableRpc` | P0 | Enable RPC / RPC OUT on linked server |
| `Invoke-SQLDisableRpc` | P0 | Disable RPC on linked server |
| `-Hash` (global) | P0 | NTLM pass-the-hash authentication |
| `--stdin json` / `--format json` | P1 | JSON pipeline I/O |
| `-Port` | P1 | Custom TCP port |
| `-Encrypt`, `-ForceNamedPipe`, `-PacketSize` | P1 | Connection string options |
| `-Debug` | P2 | Verbose SQL preview |
| `-Instance h1,h2` | P2 | Comma-separated multi-host |

---

## Common parameters

Most instance-targeting commands accept:

| Parameter | Description |
|-----------|-------------|
| `-Instance` | `HOST`, `HOST\INSTANCE`, or `HOST,PORT` |
| `-Username` / `-Password` | SQL authentication |
| `-Domain` | Windows domain (with `-Hash` or integrated auth) |
| `-Threads` | Parallel batch size (threaded variants) |
| `-SuppressVerbose` | Quiet mode |
| `-Exploit` | Run escalation path (audit commands) |

See PowerUpSQL wiki for full parameter documentation: [PowerUpSQL Wiki](https://github.com/NetSPI/PowerUpSQL/wiki).

---

## Regression coverage

| Layer | Script | Requires lab |
|-------|--------|--------------|
| Unit + registry parity | `tests\run-unit-tests.ps1` | No |
| Full regression | `tests\Run-Regression.ps1` | Optional |
| Lab command smoke tests | `tests\Run-LabRegression.ps1` | Yes |

Detailed per-function test status: [FUNCTION_PARITY.md](FUNCTION_PARITY.md).
