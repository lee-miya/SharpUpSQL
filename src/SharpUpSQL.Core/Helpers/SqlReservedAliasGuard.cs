using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpUpSQL.Core.Helpers
{
    /// <summary>
    /// Detects T-SQL column aliases that use reserved keywords without bracket quoting.
    /// </summary>
    public static class SqlReservedAliasGuard
    {
        private static readonly HashSet<string> ReservedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CLUSTERED",
            "ROWCOUNT",
            "CATALOG",
            "SERVER",
            "STATUS",
            "NAME",
            "VALUE",
            "TYPE",
            "SAMPLE",
            "OUTPUT",
            "USER",
            "INDEX",
            "KEY",
            "DATABASE",
            "TABLE",
            "VIEW",
            "COLUMN",
            "DEFAULT",
            "PASSWORD",
            "ORDER",
            "GROUP",
            "OPTION",
            "CHECK",
            "CONSTRAINT",
            "PRIMARY",
            "FOREIGN",
            "REFERENCES",
            "TRIGGER",
            "CURSOR",
            "TRANSACTION",
            "PROCEDURE",
            "FUNCTION"
        };

        // Matches "AS Alias" where Alias is not already bracketed and not a data type (AS VARCHAR...).
        private static readonly Regex AliasPattern = new Regex(
            @"(?i)\bAS\s+(?!\[)(?!(?:VARCHAR|NVARCHAR|CHAR|NCHAR|INT|BIGINT|SMALLINT|TINYINT|BIT|DECIMAL|NUMERIC|FLOAT|REAL|DATETIME2?|DATE|TIME|VARBINARY|BINARY|XML|UNIQUEIDENTIFIER|TEXT|NTEXT|IMAGE|MONEY|SMALLMONEY|SQL_VARIANT|HIERARCHYID|GEOGRAPHY|GEOMETRY)\b)([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsReservedAlias(string alias)
        {
            return !string.IsNullOrEmpty(alias) && ReservedAliases.Contains(alias);
        }

        public static string BracketAlias(string alias)
        {
            if (string.IsNullOrEmpty(alias))
            {
                return alias;
            }

            if (alias.StartsWith("[", StringComparison.Ordinal) && alias.EndsWith("]", StringComparison.Ordinal))
            {
                return alias;
            }

            return "[" + alias.Replace("]", "]]") + "]";
        }

        public static IList<SqlAliasViolation> FindViolations(string sql)
        {
            var violations = new List<SqlAliasViolation>();
            if (string.IsNullOrEmpty(sql))
            {
                return violations;
            }

            foreach (Match match in AliasPattern.Matches(sql))
            {
                var alias = match.Groups[1].Value;
                if (IsReservedAlias(alias))
                {
                    violations.Add(new SqlAliasViolation(alias, match.Index));
                }
            }

            return violations;
        }

        public static IList<SqlAliasViolation> FindViolationsInSource(string source, string filePath)
        {
            var violations = new List<SqlAliasViolation>();
            if (string.IsNullOrEmpty(source))
            {
                return violations;
            }

            foreach (Match match in AliasPattern.Matches(source))
            {
                var alias = match.Groups[1].Value;
                if (!IsReservedAlias(alias))
                {
                    continue;
                }

                var line = 1;
                for (var i = 0; i < match.Index; i++)
                {
                    if (source[i] == '\n')
                    {
                        line++;
                    }
                }

                violations.Add(new SqlAliasViolation(alias, match.Index, filePath, line));
            }

            return violations;
        }
    }

    public sealed class SqlAliasViolation
    {
        public SqlAliasViolation(string alias, int index)
        {
            Alias = alias;
            Index = index;
        }

        public SqlAliasViolation(string alias, int index, string filePath, int line)
        {
            Alias = alias;
            Index = index;
            FilePath = filePath;
            Line = line;
        }

        public string Alias { get; private set; }
        public int Index { get; private set; }
        public string FilePath { get; private set; }
        public int Line { get; private set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(FilePath) && Line > 0)
            {
                return FilePath + ":" + Line + " — unbracketed reserved alias AS " + Alias;
            }

            return "Unbracketed reserved alias AS " + Alias + " at index " + Index;
        }
    }
}
