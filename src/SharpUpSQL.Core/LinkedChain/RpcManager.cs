using System;
using System.Collections.Generic;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Core.LinkedChain
{
    public static class RpcManager
    {
        public static IEnumerable<Dictionary<string, object>> SetRpc(
            SqlConnectionOptions options,
            string linkPath,
            bool enable,
            bool rpcOut,
            VerboseWriter verbose,
            bool suppressVerbose,
            bool debugSql)
        {
            if (string.IsNullOrWhiteSpace(linkPath))
            {
                throw new ArgumentException("LinkPath is required.", "linkPath");
            }

            var links = linkPath.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var results = new List<Dictionary<string, object>>();

            for (var i = 0; i < links.Length; i++)
            {
                var hopPath = string.Join(",", links, 0, i + 1);
                var link = LinkedChainQueryBuilder.EscapeLinkName(links[i].Trim());
                var value = enable ? "true" : "false";
                var rpcQuery = "EXEC sp_serveroption " + link + ", 'rpc', '" + value + "'";
                var rpcOutQuery = "EXEC sp_serveroption " + link + ", 'rpc out', '" + value + "'";

                var contextPath = i == 0 ? string.Empty : string.Join(",", links, 0, i);
                ExecuteHop(options, contextPath, rpcQuery, verbose, suppressVerbose, debugSql);
                if (rpcOut)
                {
                    ExecuteHop(options, contextPath, rpcOutQuery, verbose, suppressVerbose, debugSql);
                }

                results.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Link", links[i].Trim() },
                    { "HopPath", hopPath },
                    { "Rpc", enable ? "Enabled" : "Disabled" },
                    { "RpcOut", rpcOut ? (enable ? "Enabled" : "Disabled") : "Skipped" }
                });
            }

            return results;
        }

        private static void ExecuteHop(
            SqlConnectionOptions options,
            string contextPath,
            string statement,
            VerboseWriter verbose,
            bool suppressVerbose,
            bool debugSql)
        {
            var query = string.IsNullOrEmpty(contextPath)
                ? statement
                : LinkedChainQueryBuilder.BuildExecAtChain(contextPath, statement);

            QueryExecutor.ExecuteNonQuery(options, query, verbose, suppressVerbose, debugSql);
        }
    }
}
