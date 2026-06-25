using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Common
{
    internal static class SqlEnumerationEngine2
    {
        internal static IEnumerable<SqlGenericRowResult> GetSqlDatabasePriv(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var dbFilter = string.IsNullOrEmpty(databaseName)
                ? string.Empty
                : " AND dp.class_desc = 'DATABASE' AND DB_NAME(dp.major_id) LIKE " +
                  SqlValueFormatter.QuoteLiteral("%" + databaseName + "%");

            var query = @"
SELECT
    DB_NAME(dp.major_id) AS [Name],
    pr.name AS [Value],
    dp.permission_name AS [Type],
    dp.state_desc AS Description
FROM sys.database_permissions dp
JOIN sys.database_principals pr ON dp.grantee_principal_id = pr.principal_id
WHERE 1=1" + dbFilter;

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "DatabasePriv",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    Type = SqlValueFormatter.Format(row["Type"]),
                    Description = SqlValueFormatter.Format(row["Description"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlDatabaseRole(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = ResolveDatabases(options, instance, databaseName, verbose);
            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT name AS [Name], type_desc AS [Type], create_date AS CreateDate
FROM sys.database_principals
WHERE type = 'R' AND is_fixed_role = 0";

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = "DatabaseRole",
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Type = SqlValueFormatter.Format(row["Type"]),
                        CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                        Value = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlDatabaseRoleMember(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = ResolveDatabases(options, instance, databaseName, verbose);
            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT
    USER_NAME(rm.member_principal_id) AS [Name],
    USER_NAME(rm.role_principal_id) AS [Value],
    rm.role_principal_id AS [Type]
FROM sys.database_role_members rm";

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = "DatabaseRoleMember",
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Value = SqlValueFormatter.Format(row["Value"]),
                        Type = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlDatabaseSchema(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = ResolveDatabases(options, instance, databaseName, verbose);
            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT
    name AS [Name],
    schema_id AS [Type],
    USER_NAME(principal_id) AS [Value]
FROM sys.schemas";

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = "DatabaseSchema",
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Type = SqlValueFormatter.Format(row["Type"]),
                        Value = SqlValueFormatter.Format(row["Value"]),
                        Extra1 = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlDatabaseUser(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = ResolveDatabases(options, instance, databaseName, verbose);
            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT
    name AS [Name],
    type_desc AS [Type],
    create_date AS CreateDate,
    default_schema_name AS [Value]
FROM sys.database_principals
WHERE type IN ('S','U','G','C','E','X')";

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = "DatabaseUser",
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Type = SqlValueFormatter.Format(row["Type"]),
                        CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                        Value = SqlValueFormatter.Format(row["Value"]),
                        Extra1 = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlServerConfiguration(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    name AS [Name],
    CAST(value AS VARCHAR(50)) AS [Value],
    CAST(value_in_use AS VARCHAR(50)) AS [Type],
    description AS Description
FROM sys.configurations";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "ServerConfiguration",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    Type = SqlValueFormatter.Format(row["Type"]),
                    Description = SqlValueFormatter.Format(row["Description"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlServerCredential(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    name AS [Name],
    credential_identity AS [Value],
    create_date AS CreateDate,
    modify_date AS ModifyDate
FROM sys.credentials";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "ServerCredential",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                    ModifyDate = SqlValueFormatter.Format(row["ModifyDate"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlServerPolicy(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    p.name AS [Name],
    p.description AS Description,
    p.is_enabled AS [Value],
    p.date_created AS CreateDate,
    p.date_modified AS ModifyDate
FROM msdb.dbo.syspolicy_policies p";

            var policyOptions = options.Clone();
            policyOptions.Database = "msdb";

            return SqlEnumerationEngine.GetGenericRows(
                policyOptions,
                instance,
                query,
                "ServerPolicy",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Description = SqlValueFormatter.Format(row["Description"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                    ModifyDate = SqlValueFormatter.Format(row["ModifyDate"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlServerPriv(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    pr.name AS [Name],
    pe.permission_name AS [Type],
    pe.state_desc AS Description,
    pe.class_desc AS [Value]
FROM sys.server_permissions pe
JOIN sys.server_principals pr ON pe.grantee_principal_id = pr.principal_id
WHERE pr.name = SYSTEM_USER";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "ServerPriv",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Type = SqlValueFormatter.Format(row["Type"]),
                    Description = SqlValueFormatter.Format(row["Description"]),
                    Value = SqlValueFormatter.Format(row["Value"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlServerRole(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT name AS [Name], type_desc AS [Type], create_date AS CreateDate
FROM sys.server_principals
WHERE type = 'R' AND is_fixed_role = 1";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "ServerRole",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Type = SqlValueFormatter.Format(row["Type"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlServerRoleMember(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    sp.name AS [Name],
    sp2.name AS [Value],
    sp2.type_desc AS [Type]
FROM sys.server_role_members rm
JOIN sys.server_principals sp ON rm.member_principal_id = sp.principal_id
JOIN sys.server_principals sp2 ON rm.role_principal_id = sp2.principal_id";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "ServerRoleMember",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    Type = SqlValueFormatter.Format(row["Type"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlSession(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    CAST(session_id AS VARCHAR(20)) AS [Name],
    login_name AS [Value],
    host_name AS [Type],
    program_name AS Description,
    status AS Extra1,
    CAST(connect_time AS VARCHAR(30)) AS CreateDate
FROM sys.dm_exec_sessions
WHERE is_user_process = 1";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "Session",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    Type = SqlValueFormatter.Format(row["Type"]),
                    Description = SqlValueFormatter.Format(row["Description"]),
                    Extra1 = SqlValueFormatter.Format(row["Extra1"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlStoredProcedure(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetStoredProcedureQuery(
                options,
                instance,
                databaseName,
                null,
                "StoredProcedure",
                verbose,
                suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlStoredProcedureClr(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetStoredProcedureQuery(
                options,
                instance,
                databaseName,
                " AND p.object_id IN (SELECT object_id FROM sys.assembly_modules)",
                "StoredProcedureCLR",
                verbose,
                suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlStoredProcedureSqli(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetStoredProcedureQuery(
                options,
                instance,
                databaseName,
                " AND (m.definition LIKE '%EXEC(%' OR m.definition LIKE '%EXECUTE(%' OR m.definition LIKE '%sp_executesql%')",
                "StoredProcedureSQLi",
                verbose,
                suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlStoredProcedureAutoExec(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetStoredProcedureQuery(
                options,
                instance,
                databaseName,
                " AND p.is_auto_executed = 1",
                "StoredProcedureAutoExec",
                verbose,
                suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlStoredProcedureXp(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetStoredProcedureQuery(
                options,
                instance,
                databaseName,
                " AND m.definition LIKE '%xp_%'",
                "StoredProcedureXp",
                verbose,
                suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlView(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            bool noDefaults,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = SqlEnumerationEngine.GetSqlDatabase(
                    options,
                    instance,
                    databaseName,
                    noDefaults,
                    true,
                    false,
                    verbose,
                    true)
                .Select(d => d.DatabaseName)
                .Distinct()
                .ToList();

            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT
    SCHEMA_NAME(v.schema_id) AS [Name],
    v.name AS [Value],
    CONVERT(VARCHAR(30), v.create_date, 120) AS CreateDate,
    CONVERT(VARCHAR(30), v.modify_date, 120) AS ModifyDate
FROM sys.views v";

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = "View",
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Value = SqlValueFormatter.Format(row["Value"]),
                        CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                        ModifyDate = SqlValueFormatter.Format(row["ModifyDate"]),
                        Type = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlTableTemp(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var tempOptions = options.Clone();
            tempOptions.Database = "tempdb";

            var query = @"
SELECT
    name AS [Name],
    create_date AS CreateDate,
    'tempdb' AS [Value]
FROM tempdb.sys.tables
WHERE name LIKE '#%'";

            return SqlEnumerationEngine.GetGenericRows(
                tempOptions,
                instance,
                query,
                "TableTemp",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                    Value = SqlValueFormatter.Format(row["Value"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlTriggerDdl(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetTriggerQuery(options, instance, databaseName, " AND t.parent_class_desc = 'DATABASE'", "TriggerDdl", verbose, suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlTriggerDml(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            return GetTriggerQuery(options, instance, databaseName, " AND t.parent_class_desc = 'OBJECT_OR_COLUMN'", "TriggerDml", verbose, suppressVerbose);
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlAuditDatabaseSpec(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    name AS [Name],
    CAST(is_state_enabled AS VARCHAR(5)) AS [Value],
    create_date AS CreateDate,
    modify_date AS ModifyDate
FROM sys.database_audit_specifications";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "AuditDatabaseSpec",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                    ModifyDate = SqlValueFormatter.Format(row["ModifyDate"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlAuditServerSpec(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    name AS [Name],
    CAST(is_state_enabled AS VARCHAR(5)) AS [Value],
    create_date AS CreateDate,
    modify_date AS ModifyDate
FROM sys.server_audit_specifications";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "AuditServerSpec",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                    ModifyDate = SqlValueFormatter.Format(row["ModifyDate"])
                });
        }

        internal static IEnumerable<SqlGenericRowResult> GetSqlOleDbProvider(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    name AS [Name],
    clsid AS [Value],
    CAST(enabled AS VARCHAR(5)) AS [Type]
FROM sys.oledb_providers";

            return SqlEnumerationEngine.GetGenericRows(
                options,
                instance,
                query,
                "OleDbProvider",
                verbose,
                suppressVerbose,
                row => new SqlGenericRowResult
                {
                    Name = SqlValueFormatter.Format(row["Name"]),
                    Value = SqlValueFormatter.Format(row["Value"]),
                    Type = SqlValueFormatter.Format(row["Type"])
                });
        }

        internal static IEnumerable<SqlServerLoginResult> GetSqlServerLoginDefaultPw(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var defaults = new[]
            {
                new { Login = "sa", Password = string.Empty },
                new { Login = "sa", Password = "sa" },
                new { Login = "sa", Password = "password" },
                new { Login = "sa", Password = "Password1" },
                new { Login = "sa", Password = "sql" }
            };

            var results = new List<SqlServerLoginResult>();
            foreach (var entry in defaults)
            {
                var testOptions = options.Clone();
                testOptions.Username = entry.Login;
                testOptions.Password = entry.Password;

                try
                {
                    QueryExecutor.ExecuteScalar(testOptions, "SELECT 1", verbose, true);
                    var result = new SqlServerLoginResult
                    {
                        PrincipalName = entry.Login,
                        PrincipalType = "DefaultPassword",
                        IsLocked = entry.Password
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    results.Add(result);
                }
                catch
                {
                    // Expected for invalid credentials.
                }
            }

            return results;
        }

        private static IEnumerable<SqlGenericRowResult> GetStoredProcedureQuery(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            string extraFilter,
            string category,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = ResolveDatabases(options, instance, databaseName, verbose);
            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT
    SCHEMA_NAME(p.schema_id) AS [Name],
    p.name AS [Value],
    p.type_desc AS [Type],
    CONVERT(VARCHAR(30), p.create_date, 120) AS CreateDate,
    CONVERT(VARCHAR(30), p.modify_date, 120) AS ModifyDate
FROM sys.procedures p
WHERE 1=1" + (extraFilter ?? string.Empty);

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = category,
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Value = SqlValueFormatter.Format(row["Value"]),
                        Type = SqlValueFormatter.Format(row["Type"]),
                        CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                        ModifyDate = SqlValueFormatter.Format(row["ModifyDate"]),
                        Description = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        private static IEnumerable<SqlGenericRowResult> GetTriggerQuery(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            string extraFilter,
            string category,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = ResolveDatabases(options, instance, databaseName, verbose);
            foreach (var db in databases)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = db;

                var query = @"
SELECT
    t.name AS [Name],
    OBJECT_NAME(t.parent_id) AS [Value],
    t.type_desc AS [Type],
    CONVERT(VARCHAR(30), t.create_date, 120) AS CreateDate,
    CONVERT(VARCHAR(30), t.modify_date, 120) AS ModifyDate
FROM sys.triggers t
WHERE 1=1" + extraFilter;

                foreach (var row in SqlEnumerationEngine.EnumerateRows(dbOptions, query, verbose, suppressVerbose))
                {
                    var result = new SqlGenericRowResult
                    {
                        Category = category,
                        Name = SqlValueFormatter.Format(row["Name"]),
                        Value = SqlValueFormatter.Format(row["Value"]),
                        Type = SqlValueFormatter.Format(row["Type"]),
                        CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                        ModifyDate = SqlValueFormatter.Format(row["ModifyDate"]),
                        Description = db
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        private static List<string> ResolveDatabases(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            VerboseWriter verbose)
        {
            return SqlEnumerationEngine.GetSqlDatabase(
                    options,
                    instance,
                    databaseName,
                    false,
                    true,
                    false,
                    verbose,
                    true)
                .Select(d => d.DatabaseName)
                .Distinct()
                .ToList();
        }
    }
}
