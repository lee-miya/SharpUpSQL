using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.LinkedChain;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Output;
namespace SharpUpSQL.Common
{
    internal sealed class LinkServerInfo
    {
        public string ServerName { get; set; }
        public string Version { get; set; }
        public string LinkUser { get; set; }
        public string IsSysadmin { get; set; }
    }

    internal static class LinkServerEngine
    {
        internal static IEnumerable<SqlServerLinkCrawlResult> GetSqlServerLinkCrawl(
            SqlConnectionOptions options,
            string instance,
            string customQuery,
            string queryTarget,
            bool export,
            bool export2,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (export2)
            {
                foreach (var row in CrawlExport2(options, instance, customQuery, verbose, suppressVerbose))
                {
                    yield return new SqlServerLinkCrawlResult
                    {
                        Instance = row.LinkInstance,
                        Version = row.LinkVersion,
                        Path = row.LinkPath,
                        Links = row.LinkName,
                        User = row.LinkUser,
                        Sysadmin = row.LinkSysadmin,
                        CustomQuery = row.LinkHops
                    };
                }

                yield break;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in CrawlRecursive(
                         options,
                         instance,
                         instance,
                         string.Empty,
                         customQuery,
                         queryTarget,
                         visited,
                         verbose,
                         suppressVerbose))
            {
                yield return result;
            }
        }

        internal static IEnumerable<SqlQueryResult> GetSqlServerLinkQuery(
            SqlConnectionOptions options,
            string instance,
            string linkPath,
            string query,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var openQuery = BuildOpenQuery(linkPath, query ?? "SELECT @@version AS Version");
            foreach (var row in SqlEnumerationEngine.GetSqlQuery(options, openQuery, instance, verbose, suppressVerbose))
            {
                yield return row;
            }
        }

        internal static LinkServerInfo GetSqlServerLinkData(
            SqlConnectionOptions options,
            string instance,
            string linkPath,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = BuildOpenQuery(
                linkPath,
                "SELECT @@servername AS servername, @@version AS version, SYSTEM_USER AS linkuser, CAST(IS_SRVROLEMEMBER('sysadmin') AS VARCHAR(5)) AS role");

            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            if (rows.Count == 0)
            {
                return null;
            }

            var row = rows[0];
            return new LinkServerInfo
            {
                ServerName = SqlValueFormatter.Format(GetRowValue(row, "servername")),
                Version = SqlValueFormatter.Format(GetRowValue(row, "version")),
                LinkUser = SqlValueFormatter.Format(GetRowValue(row, "linkuser")),
                IsSysadmin = SqlValueFormatter.Format(GetRowValue(row, "role"))
            };
        }

        internal static IEnumerable<string> GetCrawlableLinks(
            SqlConnectionOptions options,
            string instance,
            string linkPath,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = BuildOpenQuery(linkPath, "SELECT srvname FROM master..sysservers WHERE dataaccess = 1");
            var links = new List<string>();

            foreach (var row in QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose))
            {
                var name = SqlValueFormatter.Format(GetRowValue(row, "srvname"));
                if (!string.IsNullOrEmpty(name) &&
                    !string.Equals(name, "(local)", StringComparison.OrdinalIgnoreCase))
                {
                    links.Add(name);
                }
            }

            return links;
        }

        private static IEnumerable<SqlServerLinkCrawlResult> CrawlRecursive(
            SqlConnectionOptions options,
            string rootInstance,
            string currentPath,
            string parentPath,
            string customQuery,
            string queryTarget,
            HashSet<string> visited,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var pathKey = string.IsNullOrEmpty(currentPath) ? rootInstance : currentPath;
            if (!visited.Add(pathKey))
            {
                yield break;
            }

            LinkServerInfo info;
            try
            {
                info = GetSqlServerLinkData(options, rootInstance, currentPath, verbose, suppressVerbose);
            }
            catch
            {
                yield break;
            }

            if (info == null)
            {
                yield break;
            }

            IEnumerable<string> childLinks;
            try
            {
                childLinks = GetCrawlableLinks(options, rootInstance, currentPath, verbose, suppressVerbose).ToList();
            }
            catch
            {
                childLinks = Enumerable.Empty<string>();
            }

            var customResult = string.Empty;
            if (!string.IsNullOrEmpty(customQuery) &&
                (string.IsNullOrEmpty(queryTarget) ||
                 string.Equals(info.ServerName, queryTarget, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var rows = QueryExecutor.ExecuteQuery(
                        options,
                        BuildOpenQuery(currentPath, customQuery),
                        verbose,
                        suppressVerbose);
                    customResult = rows.Count.ToString();
                }
                catch (Exception ex)
                {
                    customResult = ex.Message;
                }
            }

            yield return new SqlServerLinkCrawlResult
            {
                Instance = info.ServerName,
                Version = info.Version,
                Links = string.Join(",", childLinks),
                Path = string.IsNullOrEmpty(parentPath) ? rootInstance : parentPath + " -> " + info.ServerName,
                User = info.LinkUser,
                Sysadmin = info.IsSysadmin,
                CustomQuery = customResult
            };

            foreach (var link in childLinks)
            {
                var nextPath = string.IsNullOrEmpty(currentPath) ? link : currentPath + "," + link;
                foreach (var nested in CrawlRecursive(
                             options,
                             rootInstance,
                             nextPath,
                             string.IsNullOrEmpty(parentPath) ? rootInstance : parentPath + " -> " + info.ServerName,
                             customQuery,
                             queryTarget,
                             visited,
                             verbose,
                             suppressVerbose))
                {
                    yield return nested;
                }
            }
        }

        private static IEnumerable<SqlServerLinkCrawlExport2Result> CrawlExport2(
            SqlConnectionOptions options,
            string instance,
            string customQuery,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var links = SqlEnumerationEngine.GetSqlServerLink(options, instance, null, verbose, suppressVerbose)
                .Where(l => string.Equals(l.DatabaseLinkLocation, "Remote", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var link in links)
            {
                LinkServerInfo info;
                try
                {
                    info = GetSqlServerLinkData(options, instance, link.DatabaseLinkName, verbose, suppressVerbose);
                }
                catch
                {
                    continue;
                }

                if (info == null)
                {
                    continue;
                }

                yield return new SqlServerLinkCrawlExport2Result
                {
                    LinkSrc = instance,
                    LinkName = link.DatabaseLinkName,
                    LinkInstance = info.ServerName,
                    LinkUser = info.LinkUser,
                    LinkSysadmin = info.IsSysadmin,
                    LinkVersion = info.Version,
                    LinkHops = "1",
                    LinkPath = instance + " -> " + info.ServerName
                };
            }
        }

        internal static string BuildOpenQuery(string linkPath, string innerQuery)
        {
            return LinkedChainQueryBuilder.BuildOpenQueryChain(linkPath, innerQuery);
        }

        private static object GetRowValue(Dictionary<string, object> row, string key)
        {
            object value;
            return row.TryGetValue(key, out value) ? value : null;
        }
    }
}
