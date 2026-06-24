using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Threading;

namespace SharpUpSQL.Common
{
    internal static class CommonCommandHelper
    {
        internal static IEnumerable<object> ForEachTarget(
            SharpUpSqlContext context,
            Func<string, SqlConnectionOptions, bool, IEnumerable<object>> action)
        {
            var suppressVerbose = context.Switches.Contains("SuppressVerbose");
            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var options = context.BuildConnectionOptions();
                var instance = !string.IsNullOrWhiteSpace(target.Instance)
                    ? target.Instance
                    : options.Instance;

                if (!string.IsNullOrWhiteSpace(instance))
                {
                    options.Instance = instance;
                }

                foreach (var item in action(instance, options, suppressVerbose))
                {
                    yield return item;
                }
            }
        }

        internal static IEnumerable<object> ForEachTargetThreaded(
            SharpUpSqlContext context,
            Func<string, SqlConnectionOptions, bool, IEnumerable<object>> action)
        {
            var suppressVerbose = context.Switches.Contains("SuppressVerbose");
            var threads = 5;
            int parsedThreads;
            string threadsValue;
            if (context.Arguments.TryGetValue("Threads", out threadsValue) &&
                int.TryParse(threadsValue, out parsedThreads))
            {
                threads = parsedThreads;
            }

            var targets = InstanceTargetResolver.Resolve(context).ToList();
            var results = ThreadPoolRunner.RunParallelMany(
                targets,
                target =>
                {
                    var options = context.BuildConnectionOptions();
                    var instance = !string.IsNullOrWhiteSpace(target.Instance)
                        ? target.Instance
                        : options.Instance;

                    if (!string.IsNullOrWhiteSpace(instance))
                    {
                        options.Instance = instance;
                    }

                    return action(instance, options, suppressVerbose);
                },
                threads);

            return results.Cast<object>();
        }
    }

    public sealed class GetSqlAgentJobCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLAgentJob"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlAgentJob(
                    options,
                    instance,
                    GetArg(context, "SubSystem"),
                    GetArg(context, "Keyword"),
                    GetSwitch(context, "UsingProxyCredential"),
                    GetArg(context, "ProxyCredential"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlAuditDatabaseSpecCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLAuditDatabaseSpec"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlAuditDatabaseSpec(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlAuditServerSpecCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLAuditServerSpec"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlAuditServerSpec(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlColumnCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLColumn"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlColumn(
                    options,
                    instance,
                    GetArg(context, "DatabaseName"),
                    GetArg(context, "TableName"),
                    GetArg(context, "ColumnName"),
                    GetArg(context, "ColumnNameSearch"),
                    GetSwitch(context, "NoDefaults"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlColumnSampleDataCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLColumnSampleData"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlColumnSampleData(
                    options,
                    instance,
                    GetArg(context, "Keywords") ?? "Password",
                    GetIntArg(context, "SampleSize", 1),
                    GetArg(context, "DatabaseName"),
                    GetSwitch(context, "ValidateCC"),
                    GetSwitch(context, "NoDefaults"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlColumnSampleDataThreadedCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLColumnSampleDataThreaded"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTargetThreaded(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlColumnSampleData(
                    options,
                    instance,
                    GetArg(context, "Keywords") ?? "Password",
                    GetIntArg(context, "SampleSize", 1),
                    GetArg(context, "DatabaseName"),
                    GetSwitch(context, "ValidateCC"),
                    GetSwitch(context, "NoDefaults"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabaseCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabase"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlDatabase(
                    options,
                    instance,
                    GetArg(context, "DatabaseName"),
                    GetSwitch(context, "NoDefaults"),
                    GetSwitch(context, "HasAccess"),
                    GetSwitch(context, "SysAdminOnly"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabaseThreadedCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabaseThreaded"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTargetThreaded(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlDatabase(
                    options,
                    instance,
                    GetArg(context, "DatabaseName"),
                    GetSwitch(context, "NoDefaults"),
                    GetSwitch(context, "HasAccess"),
                    GetSwitch(context, "SysAdminOnly"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabasePrivCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabasePriv"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlDatabasePriv(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabaseRoleCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabaseRole"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlDatabaseRole(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabaseRoleMemberCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabaseRoleMember"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlDatabaseRoleMember(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabaseSchemaCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabaseSchema"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlDatabaseSchema(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlDatabaseUserCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDatabaseUser"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlDatabaseUser(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerConfigurationCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerConfiguration"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerConfiguration(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerCredentialCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerCredential"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerCredential(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerInfoCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerInfo"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlServerInfo(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerInfoThreadedCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerInfoThreaded"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTargetThreaded(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlServerInfo(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerLinkCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerLink"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlServerLink(options, instance, GetArg(context, "DatabaseLinkName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerLinkCrawlCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerLinkCrawl"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                LinkServerEngine.GetSqlServerLinkCrawl(
                    options,
                    instance,
                    GetArg(context, "Query"),
                    GetArg(context, "QueryTarget"),
                    GetSwitch(context, "Export"),
                    GetSwitch(context, "Export2"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerLinkDataCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerLinkData"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
            {
                var info = LinkServerEngine.GetSqlServerLinkData(
                    options,
                    instance,
                    GetArg(context, "LinkPath"),
                    context.Verbose,
                    suppressVerbose);

                if (info == null)
                {
                    return Enumerable.Empty<object>();
                }

                return new object[]
                {
                    new SqlServerLinkCrawlResult
                    {
                        Instance = info.ServerName,
                        Version = info.Version,
                        User = info.LinkUser,
                        Sysadmin = info.IsSysadmin
                    }
                };
            });
        }
    }

    public sealed class GetSqlServerLinkQueryCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerLinkQuery"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                LinkServerEngine.GetSqlServerLinkQuery(
                    options,
                    instance,
                    GetArg(context, "LinkPath"),
                    GetArg(context, "Query"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerLoginCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerLogin"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlServerLogin(options, instance, GetArg(context, "PrincipalName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerLoginDefaultPwCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerLoginDefaultPw"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerLoginDefaultPw(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerPolicyCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerPolicy"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerPolicy(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerPrivCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerPriv"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerPriv(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerRoleCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerRole"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerRole(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerRoleMemberCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerRoleMember"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlServerRoleMember(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServiceAccountCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServiceAccount"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return LocalChecks.GetSqlServiceAccount(
                GetArg(context, "Instance"),
                GetSwitch(context, "RunOnly"),
                msg => WriteVerbose(context, msg)).Cast<object>();
        }
    }

    public sealed class GetSqlServiceLocalCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServiceLocal"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return GetSqlServiceLocal.Execute(
                GetArg(context, "Instance"),
                GetSwitch(context, "RunOnly"),
                GetSwitch(context, "SuppressVerbose"),
                msg => WriteVerbose(context, msg),
                GetIntArg(context, "WmiTimeOut", GetSqlServiceLocal.DefaultWmiTimeOutSeconds)).Cast<object>();
        }
    }

    public sealed class GetSqlSessionCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLSession"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlSession(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlStoredProcedureCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLStoredProcedure"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlStoredProcedure(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlStoredProcedureClrCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLStoredProcedureCLR"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlStoredProcedureClr(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlStoredProcedureSqliCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLStoredProcedureSQLi"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlStoredProcedureSqli(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlStoredProcedureAutoExecCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLStoredProcedureAutoExec"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlStoredProcedureAutoExec(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlStoredProcedureXpCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLStoredProcedureXp"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlStoredProcedureXp(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlSysadminCheckCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLSysadminCheck"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlSysadminCheck(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlTableCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLTable"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine.GetSqlTable(
                    options,
                    instance,
                    GetArg(context, "DatabaseName"),
                    GetArg(context, "TableName"),
                    GetSwitch(context, "NoDefaults"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlTableTempCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLTableTemp"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlTableTemp(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlTriggerDdlCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLTriggerDdl"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlTriggerDdl(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlTriggerDmlCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLTriggerDml"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlTriggerDml(options, instance, GetArg(context, "DatabaseName"), context.Verbose, suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlViewCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLView"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlView(
                    options,
                    instance,
                    GetArg(context, "DatabaseName"),
                    GetSwitch(context, "NoDefaults"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlLocalAdminCheckCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLLocalAdminCheck"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            yield return LocalChecks.GetSqlLocalAdminCheck();
        }
    }

    public sealed class GetSqlOleDbProviderCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLOleDbProvder"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                SqlEnumerationEngine2.GetSqlOleDbProvider(options, instance, context.Verbose, suppressVerbose).Cast<object>());
        }
    }
}
