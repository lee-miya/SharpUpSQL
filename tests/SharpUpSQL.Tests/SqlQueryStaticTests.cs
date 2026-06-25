using System;
using System.Collections.Generic;
using System.IO;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Tests
{
    internal static class SqlQueryStaticTests
    {
        public static TestCase[] Cases()
        {
            return new[]
            {
                new TestCase
                {
                    Name = "SqlReservedAliasGuard flags unbracketed Clustered alias",
                    Body = () =>
                    {
                        var violations = SqlReservedAliasGuard.FindViolations(
                            "SELECT CAST(1 AS VARCHAR(5)) AS Clustered");
                        TestAssert.Equal(1, violations.Count, "Clustered should be flagged");
                        TestAssert.Equal("Clustered", violations[0].Alias, "Alias name");
                    }
                },
                new TestCase
                {
                    Name = "SqlReservedAliasGuard accepts bracketed aliases",
                    Body = () =>
                    {
                        var violations = SqlReservedAliasGuard.FindViolations(
                            "SELECT CAST(1 AS VARCHAR(5)) AS [Clustered], x AS [Name], y AS [RowCount]");
                        TestAssert.Equal(0, violations.Count, "Bracketed aliases should pass");
                    }
                },
                new TestCase
                {
                    Name = "SqlReservedAliasGuard ignores CAST data types",
                    Body = () =>
                    {
                        var violations = SqlReservedAliasGuard.FindViolations(
                            "SELECT CAST(col AS VARCHAR(20)) AS SafeColumn");
                        TestAssert.Equal(0, violations.Count, "CAST AS VARCHAR should not be treated as alias");
                    }
                },
                new TestCase
                {
                    Name = "Source tree has no unbracketed reserved SQL aliases",
                    Body = ScanSourceTreeForReservedAliases
                }
            };
        }

        private static void ScanSourceTreeForReservedAliases()
        {
            var root = FindRepositoryRoot();
            var srcRoot = Path.Combine(root, "src");
            if (!Directory.Exists(srcRoot))
            {
                TestAssert.True(false, "Source directory not found: " + srcRoot);
                return;
            }

            var violations = new List<string>();
            foreach (var file in Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                foreach (var violation in SqlReservedAliasGuard.FindViolationsInSource(source, file))
                {
                    violations.Add(violation.ToString());
                }
            }

            if (violations.Count > 0)
            {
                TestAssert.True(false, "Reserved SQL alias violations:\n" + string.Join("\n", violations));
            }
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "tests")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        }
    }
}
