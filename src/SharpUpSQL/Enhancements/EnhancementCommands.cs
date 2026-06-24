using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.LinkedChain;

namespace SharpUpSQL.Enhancements
{
    public sealed class InvokeSqlLinkedChainQueryCommand : SqlInstanceCommandBase
    {
        public override string Name { get { return "Invoke-SQLLinkedChainQuery"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var useExecAt = GetSwitch(context, "ExecAt");
            var query = GetArg(context, "Query");
            var linkPath = GetArg(context, "LinkPath");

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new System.ArgumentException("Query is required.");
            }

            if (string.IsNullOrWhiteSpace(linkPath))
            {
                throw new System.ArgumentException("LinkPath is required.");
            }

            foreach (var target in ResolveTargets(context))
            {
                var options = BuildConnectionOptions(context);
                var instance = ResolveInstance(target, context);
                if (!string.IsNullOrWhiteSpace(target.Instance))
                {
                    options.Instance = target.Instance;
                }

                var chainQuery = useExecAt
                    ? LinkedChainQueryBuilder.BuildExecAtChain(linkPath, query)
                    : LinkedChainQueryBuilder.BuildOpenQueryChain(linkPath, query);

                foreach (var row in QueryExecutor.ExecuteQuery(
                             options,
                             chainQuery,
                             context.Verbose,
                             suppressVerbose,
                             context.DebugSql))
                {
                    var result = new SqlQueryResult
                    {
                        ComputerName = target.ComputerName,
                        Instance = instance
                    };

                    foreach (var pair in row)
                    {
                        result[pair.Key] = pair.Value;
                    }

                    yield return result;
                }
            }
        }
    }

    public sealed class InvokeSqlEnableRpcCommand : SqlInstanceCommandBase
    {
        public override string Name { get { return "Invoke-SQLEnableRpc"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return RpcCommandHelper.ExecuteRpc(context, true);
        }
    }

    public sealed class InvokeSqlDisableRpcCommand : SqlInstanceCommandBase
    {
        public override string Name { get { return "Invoke-SQLDisableRpc"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return RpcCommandHelper.ExecuteRpc(context, false);
        }
    }

    internal static class RpcCommandHelper
    {
        internal static IEnumerable<object> ExecuteRpc(SharpUpSqlContext context, bool enable)
        {
            var suppressVerbose = context.Switches.Contains("SuppressVerbose");
            var rpcOut = !context.Switches.Contains("NoRpcOut");
            var linkPath = context.Arguments.ContainsKey("LinkPath")
                ? context.Arguments["LinkPath"]
                : null;

            if (string.IsNullOrWhiteSpace(linkPath))
            {
                throw new System.ArgumentException("LinkPath is required.");
            }

            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var options = context.BuildConnectionOptions();
                if (!string.IsNullOrWhiteSpace(target.Instance))
                {
                    options.Instance = target.Instance;
                }

                foreach (var row in RpcManager.SetRpc(
                             options,
                             linkPath,
                             enable,
                             rpcOut,
                             context.Verbose,
                             suppressVerbose,
                             context.DebugSql))
                {
                    yield return new RpcResult
                    {
                        Instance = target.Instance,
                        Link = row["Link"] as string,
                        HopPath = row["HopPath"] as string,
                        Rpc = row["Rpc"] as string,
                        RpcOut = row["RpcOut"] as string
                    };
                }
            }
        }
    }

    public sealed class RpcResult
    {
        public string Instance { get; set; }
        public string Link { get; set; }
        public string HopPath { get; set; }
        public string Rpc { get; set; }
        public string RpcOut { get; set; }
    }
}
