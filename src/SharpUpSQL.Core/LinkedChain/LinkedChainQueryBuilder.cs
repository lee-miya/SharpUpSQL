using System;
using System.Text;

namespace SharpUpSQL.Core.LinkedChain
{
    /// <summary>
    /// Builds nested EXEC ... AT [link] queries for multi-hop linked-server execution.
    /// </summary>
    public static class LinkedChainQueryBuilder
    {
        public static string BuildExecAtChain(string linkPath, string innerQuery)
        {
            if (string.IsNullOrWhiteSpace(innerQuery))
            {
                throw new ArgumentException("Query cannot be empty.", "innerQuery");
            }

            if (string.IsNullOrWhiteSpace(linkPath))
            {
                return innerQuery;
            }

            var links = linkPath.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var query = innerQuery;
            for (var i = links.Length - 1; i >= 0; i--)
            {
                var link = EscapeLinkName(links[i].Trim());
                query = "EXEC (" + EscapeSqlLiteral(query) + ") AT " + link;
            }

            return query;
        }

        public static string BuildOpenQueryChain(string linkPath, string innerQuery)
        {
            if (string.IsNullOrWhiteSpace(linkPath))
            {
                return innerQuery;
            }

            var links = linkPath.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var escaped = innerQuery.Replace("'", "''");
            var query = "SELECT * FROM OPENQUERY(" + EscapeLinkName(links[0].Trim()) + ", '" + escaped + "')";

            for (var i = 1; i < links.Length; i++)
            {
                escaped = query.Replace("'", "''");
                query = "SELECT * FROM OPENQUERY(" + EscapeLinkName(links[i].Trim()) + ", '" + escaped + "')";
            }

            return query;
        }

        public static string EscapeLinkName(string linkName)
        {
            if (string.IsNullOrWhiteSpace(linkName))
            {
                throw new ArgumentException("Link name cannot be empty.", "linkName");
            }

            var trimmed = linkName.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                return trimmed;
            }

            if (trimmed.IndexOf('.') >= 0 || trimmed.IndexOf('-') >= 0)
            {
                return "[" + trimmed.Replace("]", "]]") + "]";
            }

            return trimmed;
        }

        private static string EscapeSqlLiteral(string query)
        {
            return "'" + query.Replace("'", "''") + "'";
        }
    }
}
