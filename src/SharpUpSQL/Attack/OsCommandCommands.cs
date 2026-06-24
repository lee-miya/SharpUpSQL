using System;
using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Output;
using SharpUpSQL.Core.Threading;

namespace SharpUpSQL.Attack
{
    public abstract class OsCommandCommandBase : SharpUpSqlCommandBase
    {
        protected abstract OsCommandChannel Channel { get; }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var command = GetArg(context, "Command");
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command parameter is required.");
            }

            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var rawResults = GetSwitch(context, "RawResults");
            var threads = GetIntArg(context, "Threads", 1);
            var targets = ResolveTargets(context).ToList();
            var options = context.BuildConnectionOptions();

            var results = ThreadPoolRunner.RunParallel(
                targets,
                target => RunTarget(context, target, options, command, suppressVerbose),
                threads);

            foreach (var result in results)
            {
                if (rawResults)
                {
                    yield return result.CommandResults;
                }
                else
                {
                    yield return result;
                }
            }
        }

        private SqlOsCommandResult RunTarget(
            SharpUpSqlContext context,
            PipelineObject target,
            Core.Auth.SqlConnectionOptions options,
            string command,
            bool suppressVerbose)
        {
            var instance = ResolveInstance(target, context) ?? options.Instance;
            var requestOptions = options.Clone();
            if (!string.IsNullOrWhiteSpace(instance))
            {
                requestOptions.Instance = instance;
            }

            var db = GetArg(context, "Database");
            if (!string.IsNullOrWhiteSpace(db))
            {
                requestOptions.Database = db;
            }

            return OsCommandEngine.Execute(new OsCommandRequest
            {
                Options = requestOptions,
                Instance = instance,
                Command = command,
                Channel = Channel,
                Verbose = context.Verbose,
                SuppressVerbose = suppressVerbose
            });
        }

        protected IEnumerable<PipelineObject> ResolveTargets(SharpUpSqlContext context)
        {
            return InstanceTargetResolver.Resolve(context);
        }

        protected string ResolveInstance(PipelineObject target, SharpUpSqlContext context)
        {
            if (!string.IsNullOrWhiteSpace(target.Instance))
            {
                return target.Instance;
            }

            string instance;
            if (context.Arguments.TryGetValue("Instance", out instance) && !string.IsNullOrWhiteSpace(instance))
            {
                return instance;
            }

            return null;
        }
    }

    public sealed class InvokeSqlOsCmdCommand : OsCommandCommandBase
    {
        public override string Name { get { return "Invoke-SQLOSCmd"; } }
        protected override OsCommandChannel Channel { get { return OsCommandChannel.XpCmdshell; } }
    }

    public sealed class InvokeSqlOsCmdClrCommand : OsCommandCommandBase
    {
        public override string Name { get { return "Invoke-SQLOSCmdCLR"; } }
        protected override OsCommandChannel Channel { get { return OsCommandChannel.Clr; } }
    }

    public sealed class InvokeSqlOsCmdCOleCommand : OsCommandCommandBase
    {
        public override string Name { get { return "Invoke-SQLOSCmdCOle"; } }
        protected override OsCommandChannel Channel { get { return OsCommandChannel.Ole; } }
    }

    public sealed class InvokeSqlOsCmdPythonCommand : OsCommandCommandBase
    {
        public override string Name { get { return "Invoke-SQLOSCmdPython"; } }
        protected override OsCommandChannel Channel { get { return OsCommandChannel.Python; } }
    }

    public sealed class InvokeSqlOsCmdRCommand : OsCommandCommandBase
    {
        public override string Name { get { return "Invoke-SQLOSCmdR"; } }
        protected override OsCommandChannel Channel { get { return OsCommandChannel.R; } }
    }
}
