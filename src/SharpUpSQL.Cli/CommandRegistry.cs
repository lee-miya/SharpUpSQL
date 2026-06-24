using System;
using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.AdRecon;
using SharpUpSQL.Attack;
using SharpUpSQL.Audit;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core;
using SharpUpSQL.Discovery;
using SharpUpSQL.Domain;
using SharpUpSQL.Enhancements;
using SharpUpSQL.PasswordRecovery;
using SharpUpSQL.Persistence;

namespace SharpUpSQL.Cli
{
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, ISharpUpSqlCommand> Commands =
            new Dictionary<string, ISharpUpSqlCommand>(StringComparer.OrdinalIgnoreCase);

        static CommandRegistry()
        {
            Register(
                new GetSqlConnectionTestCommand(),
                new GetSqlConnectionTestThreadedCommand(),
                new GetSqlQueryCommand(),
                new GetSqlQueryThreadedCommand(),
                new GetSqlInstanceFileCommand(),
                new GetSqlInstanceLocalCommand(),
                new GetSqlInstanceDomainCommand(),
                new GetSqlInstanceScanUdpCommand(),
                new GetSqlInstanceScanUdpThreadedCommand(),
                new GetSqlInstanceBroadcastCommand(),
                new GetSqlAgentJobCommand(),
                new GetSqlAuditDatabaseSpecCommand(),
                new GetSqlAuditServerSpecCommand(),
                new GetSqlColumnCommand(),
                new GetSqlColumnSampleDataCommand(),
                new GetSqlColumnSampleDataThreadedCommand(),
                new GetSqlDatabaseCommand(),
                new GetSqlDatabaseThreadedCommand(),
                new GetSqlDatabasePrivCommand(),
                new GetSqlDatabaseRoleCommand(),
                new GetSqlDatabaseRoleMemberCommand(),
                new GetSqlDatabaseSchemaCommand(),
                new GetSqlDatabaseUserCommand(),
                new GetSqlServerConfigurationCommand(),
                new GetSqlServerCredentialCommand(),
                new GetSqlServerInfoCommand(),
                new GetSqlServerInfoThreadedCommand(),
                new GetSqlServerLinkCommand(),
                new GetSqlServerLinkCrawlCommand(),
                new GetSqlServerLinkDataCommand(),
                new GetSqlServerLinkQueryCommand(),
                new GetSqlServerLoginCommand(),
                new GetSqlServerLoginDefaultPwCommand(),
                new GetSqlServerPolicyCommand(),
                new GetSqlServerPrivCommand(),
                new GetSqlServerRoleCommand(),
                new GetSqlServerRoleMemberCommand(),
                new GetSqlServiceAccountCommand(),
                new GetSqlServiceLocalCommand(),
                new GetSqlSessionCommand(),
                new GetSqlStoredProcedureCommand(),
                new GetSqlStoredProcedureClrCommand(),
                new GetSqlStoredProcedureSqliCommand(),
                new GetSqlStoredProcedureAutoExecCommand(),
                new GetSqlStoredProcedureXpCommand(),
                new GetSqlSysadminCheckCommand(),
                new GetSqlTableCommand(),
                new GetSqlTableTempCommand(),
                new GetSqlTriggerDdlCommand(),
                new GetSqlTriggerDmlCommand(),
                new GetSqlViewCommand(),
                new GetSqlLocalAdminCheckCommand(),
                new GetSqlOleDbProviderCommand(),
                new InvokeSqlDumpInfoCommand(),
                new InvokeSqlAuditCommand(),
                new InvokeSqlAuditDefaultLoginPwCommand(),
                new InvokeSqlAuditWeakLoginPwCommand(),
                new InvokeSqlAuditPrivImpersonateLoginCommand(),
                new InvokeSqlAuditPrivServerLinkCommand(),
                new InvokeSqlAuditPrivTrustworthyCommand(),
                new InvokeSqlAuditPrivDbChainingCommand(),
                new InvokeSqlAuditPrivCreateProcedureCommand(),
                new InvokeSqlAuditPrivXpDirtreeCommand(),
                new InvokeSqlAuditPrivXpFileexistCommand(),
                new InvokeSqlAuditRoleDbDdlAdminCommand(),
                new InvokeSqlAuditRoleDbOwnerCommand(),
                new InvokeSqlAuditSampleDataByColumnCommand(),
                new InvokeSqlAuditSqliSpExecuteAsCommand(),
                new InvokeSqlAuditSqliSpSignedCommand(),
                new InvokeSqlAuditPrivAutoExecSpCommand(),
                new InvokeSqlEscalatePrivCommand(),
                new InvokeSqlOsCmdCommand(),
                new InvokeSqlOsCmdClrCommand(),
                new InvokeSqlOsCmdCOleCommand(),
                new InvokeSqlOsCmdPythonCommand(),
                new InvokeSqlOsCmdRCommand(),
                new InvokeSqlOsCmdAgentJobCommand(),
                new InvokeSqlImpersonateServiceCommand(),
                new InvokeSqlImpersonateServiceCmdCommand(),
                new CreateSqlFileXpDllCommand(),
                new CreateSqlFileClrDllCommand(),
                new GetSqlAssemblyFileCommand(),
                new GetSqlDomainObjectCommand(),
                new GetSqlDomainUserCommand(),
                new GetSqlDomainComputerCommand(),
                new GetSqlDomainSubnetCommand(),
                new GetSqlDomainSiteCommand(),
                new GetSqlDomainGroupCommand(),
                new GetSqlDomainOuCommand(),
                new GetSqlDomainAccountPolicyCommand(),
                new GetSqlDomainTrustCommand(),
                new GetSqlDomainPasswordsLapsCommand(),
                new GetSqlDomainControllerCommand(),
                new GetSqlDomainExploitableSystemCommand(),
                new GetSqlDomainGroupMemberCommand(),
                new GetSqlPersistRegRunCommand(),
                new GetSqlPersistRegDebuggerCommand(),
                new GetSqlPersistTriggerDdlCommand(),
                new GetSqlRecoverPwAutoLogonCommand(),
                new GetSqlServerPasswordHashCommand(),
                new InvokeSqlUncPathInjectionCommand(),
                new InvokeTokenManipulationCommand(),
                new InvokeSqlLinkedChainQueryCommand(),
                new InvokeSqlEnableRpcCommand(),
                new InvokeSqlDisableRpcCommand(),
                new GetDomainObjectCommand(),
                new GetDomainSpnCommand());
        }

        public static bool TryGet(string name, out ISharpUpSqlCommand command)
        {
            return Commands.TryGetValue(NormalizeName(name), out command);
        }

        public static IEnumerable<string> GetCommandNames()
        {
            return Commands.Keys.OrderBy(k => k);
        }

        private static void Register(params ISharpUpSqlCommand[] commands)
        {
            foreach (var command in commands)
            {
                Commands[NormalizeName(command.Name)] = command;
            }
        }

        private static string NormalizeName(string name)
        {
            return name != null ? name.Trim() : string.Empty;
        }
    }
}
