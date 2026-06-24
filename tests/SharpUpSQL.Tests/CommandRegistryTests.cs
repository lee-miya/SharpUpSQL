using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SharpUpSQL.Tests
{
    internal static class CommandRegistryTests
    {
        private static readonly string[] PowerUpSqlCommands =
        {
            "Get-SQLInstanceFile",
            "Get-SQLInstanceLocal",
            "Get-SQLInstanceDomain",
            "Get-SQLInstanceScanUDP",
            "Get-SQLInstanceScanUDPThreaded",
            "Get-SQLInstanceBroadcast",
            "Get-SQLConnectionTest",
            "Get-SQLConnectionTestThreaded",
            "Get-SQLQuery",
            "Get-SQLQueryThreaded",
            "Invoke-SQLOSCmd",
            "Get-SQLAgentJob",
            "Get-SQLAuditDatabaseSpec",
            "Get-SQLAuditServerSpec",
            "Get-SQLColumn",
            "Get-SQLColumnSampleData",
            "Get-SQLColumnSampleDataThreaded",
            "Get-SQLDatabase",
            "Get-SQLDatabaseThreaded",
            "Get-SQLDatabasePriv",
            "Get-SQLDatabaseRole",
            "Get-SQLDatabaseRoleMember",
            "Get-SQLDatabaseSchema",
            "Get-SQLDatabaseUser",
            "Get-SQLServerConfiguration",
            "Get-SQLServerCredential",
            "Get-SQLServerInfo",
            "Get-SQLServerInfoThreaded",
            "Get-SQLServerLink",
            "Get-SQLServerLinkCrawl",
            "Get-SQLServerLinkData",
            "Get-SQLServerLinkQuery",
            "Get-SQLServerLogin",
            "Get-SQLServerLoginDefaultPw",
            "Get-SQLServerPolicy",
            "Get-SQLServerPriv",
            "Get-SQLServerRole",
            "Get-SQLServerRoleMember",
            "Get-SQLServiceAccount",
            "Get-SQLServiceLocal",
            "Get-SQLSession",
            "Get-SQLStoredProcedure",
            "Get-SQLStoredProcedureCLR",
            "Get-SQLStoredProcedureSQLi",
            "Get-SQLStoredProcedureAutoExec",
            "Get-SQLStoredProcedureXp",
            "Get-SQLSysadminCheck",
            "Get-SQLTable",
            "Get-SQLTableTemp",
            "Get-SQLTriggerDdl",
            "Get-SQLTriggerDml",
            "Get-SQLView",
            "Get-SQLLocalAdminCheck",
            "Get-SQLOleDbProvder",
            "Get-SQLFuzzDatabaseName",
            "Get-SQLFuzzDomainAccount",
            "Get-SQLFuzzObjectName",
            "Get-SQLFuzzServerLogin",
            "Get-SQLDomainObject",
            "Get-SQLDomainComputer",
            "Get-SQLDomainUser",
            "Get-SQLDomainSubnet",
            "Get-SQLDomainSite",
            "Get-SQLDomainGroup",
            "Get-SQLDomainOu",
            "Get-SQLDomainAccountPolicy",
            "Get-SQLDomainTrust",
            "Get-SQLDomainPasswordsLAPS",
            "Get-SQLDomainController",
            "Get-SQLDomainExploitableSystem",
            "Get-SQLDomainGroupMember",
            "Invoke-SQLAudit",
            "Invoke-SQLAuditPrivCreateProcedure",
            "Invoke-SQLAuditPrivDbChaining",
            "Invoke-SQLAuditPrivImpersonateLogin",
            "Invoke-SQLAuditPrivServerLink",
            "Invoke-SQLAuditPrivTrustworthy",
            "Invoke-SQLAuditPrivXpDirtree",
            "Invoke-SQLAuditPrivXpFileexist",
            "Invoke-SQLAuditRoleDbDdlAdmin",
            "Invoke-SQLAuditRoleDbOwner",
            "Invoke-SQLAuditSampleDataByColumn",
            "Invoke-SQLAuditWeakLoginPw",
            "Invoke-SQLAuditSQLiSpExecuteAs",
            "Invoke-SQLAuditSQLiSpSigned",
            "Invoke-SQLAuditDefaultLoginPw",
            "Invoke-SQLAuditPrivAutoExecSp",
            "Invoke-SQLDumpInfo",
            "Invoke-SQLEscalatePriv",
            "Invoke-SQLImpersonateService",
            "Invoke-SQLImpersonateServiceCmd",
            "Invoke-SQLOSCmdCLR",
            "Invoke-SQLOSCmdCOle",
            "Invoke-SQLOSCmdPython",
            "Invoke-SQLOSCmdR",
            "Invoke-SQLOSCmdAgentJob",
            "Get-SQLRecoverPwAutoLogon",
            "Get-SQLServerPasswordHash",
            "Invoke-SQLUncPathInjection",
            "Invoke-TokenManipulation",
            "Get-SQLPersistRegRun",
            "Get-SQLPersistRegDebugger",
            "Get-SQLPersistTriggerDDL",
            "Create-SQLFileXpDll",
            "Create-SQLFileCLRDll",
            "Get-SQLAssemblyFile",
            "Get-DomainObject",
            "Get-DomainSpn"
        };

        private static readonly string[] EnhancementCommands =
        {
            "Invoke-SQLLinkedChainQuery",
            "Invoke-SQLEnableRpc",
            "Invoke-SQLDisableRpc"
        };

        public static TestCase[] Cases(string sharpUpSqlExe)
        {
            return new[]
            {
                new TestCase
                {
                    Name = "SharpUpSQL.exe is present",
                    Body = () => TestAssert.True(
                        File.Exists(sharpUpSqlExe),
                        "Executable not found: " + sharpUpSqlExe)
                },
                new TestCase
                {
                    Name = "CLI exposes all PowerUpSQL commands except known gaps",
                    Body = () =>
                    {
                        var registered = GetRegisteredCommands(sharpUpSqlExe);
                        var knownGaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Get-SQLFuzzDatabaseName",
                            "Get-SQLFuzzDomainAccount",
                            "Get-SQLFuzzObjectName",
                            "Get-SQLFuzzServerLogin"
                        };

                        var missing = PowerUpSqlCommands
                            .Where(name => !knownGaps.Contains(name))
                            .Where(name => !registered.Contains(name))
                            .OrderBy(name => name)
                            .ToList();

                        TestAssert.True(
                            missing.Count == 0,
                            "Missing commands: " + string.Join(", ", missing));
                    }
                },
                new TestCase
                {
                    Name = "CLI exposes Phase 6 enhancement commands",
                    Body = () =>
                    {
                        var registered = GetRegisteredCommands(sharpUpSqlExe);
                        var missing = EnhancementCommands
                            .Where(name => !registered.Contains(name))
                            .ToList();

                        TestAssert.True(
                            missing.Count == 0,
                            "Missing enhancement commands: " + string.Join(", ", missing));
                    }
                },
                new TestCase
                {
                    Name = "CLI command count matches manifest",
                    Body = () =>
                    {
                        var registered = GetRegisteredCommands(sharpUpSqlExe);
                        var expected = PowerUpSqlCommands.Length - 4 + EnhancementCommands.Length;
                        TestAssert.Equal(expected, registered.Count, "Registered command count");
                    }
                }
            };
        }

        private static HashSet<string> GetRegisteredCommands(string sharpUpSqlExe)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = sharpUpSqlExe,
                Arguments = "__invalid_command__",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var capture = false;
                foreach (var line in stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Available commands:", StringComparison.OrdinalIgnoreCase))
                    {
                        capture = true;
                        continue;
                    }

                    if (capture && line.Trim().Length > 0)
                    {
                        commands.Add(line.Trim());
                    }
                }

                return commands;
            }
        }
    }
}
