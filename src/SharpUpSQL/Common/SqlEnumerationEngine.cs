using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Common
{
    internal static class SqlEnumerationEngine
    {
        internal static IEnumerable<SqlQueryResult> GetSqlQuery(
            SqlConnectionOptions options,
            string query,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            foreach (var row in rows)
            {
                var result = new SqlQueryResult();
                SqlValueFormatter.StampInstance(result, instance);
                foreach (var pair in row)
                {
                    result[pair.Key] = pair.Value;
                }

                yield return result;
            }
        }

        internal static IEnumerable<SqlServerInfoResult> GetSqlServerInfo(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    DEFAULT_DOMAIN() AS DomainName,
    CAST(SERVERPROPERTY('processid') AS VARCHAR(20)) AS ServiceProcessID,
    CAST(SERVERPROPERTY('InstanceName') AS VARCHAR(100)) AS ServiceName,
    CAST(SERVERPROPERTY('productversion') AS VARCHAR(100)) AS SQLServerVersionNumber,
    CAST(SERVERPROPERTY('ProductMajorVersion') AS VARCHAR(10)) AS SQLServerMajorVersion,
    CAST(SERVERPROPERTY('Edition') AS VARCHAR(200)) AS SQLServerEdition,
    CAST(SERVERPROPERTY('ProductLevel') AS VARCHAR(50)) AS SQLServerServicePack,
    CAST(SERVERPROPERTY('IsClustered') AS VARCHAR(5)) AS [Clustered],
    CAST(SERVERPROPERTY('IsHadrEnabled') AS VARCHAR(5)) AS HadrEnabled,
    SYSTEM_USER AS Currentlogin,
    CAST(IS_SRVROLEMEMBER('sysadmin') AS VARCHAR(5)) AS IsSysadmin,
    (SELECT CAST(COUNT(*) AS VARCHAR(20)) FROM sys.dm_exec_sessions WHERE is_user_process = 1) AS ActiveSessions,
    CAST(SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS VARCHAR(100)) AS ComputerNamePhysical,
    CAST(SERVERPROPERTY('MachineName') AS VARCHAR(100)) AS MachineName";

            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            foreach (var row in rows)
            {
                var result = new SqlServerInfoResult
                {
                    DomainName = SqlValueFormatter.Format(row.ContainsKey("DomainName") ? row["DomainName"] : null),
                    ServiceProcessID = SqlValueFormatter.Format(row.ContainsKey("ServiceProcessID") ? row["ServiceProcessID"] : null),
                    ServiceName = SqlValueFormatter.Format(row.ContainsKey("ServiceName") ? row["ServiceName"] : null),
                    SQLServerVersionNumber = SqlValueFormatter.Format(row.ContainsKey("SQLServerVersionNumber") ? row["SQLServerVersionNumber"] : null),
                    SQLServerMajorVersion = SqlValueFormatter.Format(row.ContainsKey("SQLServerMajorVersion") ? row["SQLServerMajorVersion"] : null),
                    SQLServerEdition = SqlValueFormatter.Format(row.ContainsKey("SQLServerEdition") ? row["SQLServerEdition"] : null),
                    SQLServerServicePack = SqlValueFormatter.Format(row.ContainsKey("SQLServerServicePack") ? row["SQLServerServicePack"] : null),
                    Clustered = SqlValueFormatter.Format(row.ContainsKey("Clustered") ? row["Clustered"] : null),
                    Currentlogin = SqlValueFormatter.Format(row.ContainsKey("Currentlogin") ? row["Currentlogin"] : null),
                    IsSysadmin = SqlValueFormatter.Format(row.ContainsKey("IsSysadmin") ? row["IsSysadmin"] : null),
                    ActiveSessions = SqlValueFormatter.Format(row.ContainsKey("ActiveSessions") ? row["ActiveSessions"] : null),
                    AuthenticationMode = "Unknown",
                    ForcedEncryption = "Unknown",
                    ServiceAccount = string.Empty,
                    OSArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit",
                    OsMachineType = string.Empty,
                    OSVersionName = string.Empty,
                    OsVersionNumber = Environment.OSVersion.Version.ToString()
                };

                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlDatabaseResult> GetSqlDatabase(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            bool noDefaults,
            bool hasAccess,
            bool sysAdminOnly,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var filters = new StringBuilder();
            if (!string.IsNullOrEmpty(databaseName))
            {
                filters.Append(" AND a.name LIKE ").Append(SqlValueFormatter.QuoteLiteral("%" + databaseName + "%"));
            }

            if (noDefaults)
            {
                filters.Append(" AND a.name NOT IN ('master','tempdb','msdb','model')");
            }

            if (hasAccess)
            {
                filters.Append(" AND HAS_DBACCESS(a.name) = 1");
            }

            if (sysAdminOnly)
            {
                filters.Append(" AND IS_SRVROLEMEMBER('sysadmin', SUSER_SNAME(a.owner_sid)) = 1");
            }

            var query = @"
SELECT
    CAST(a.database_id AS VARCHAR(20)) AS DatabaseId,
    a.name AS DatabaseName,
    SUSER_SNAME(a.owner_sid) AS DatabaseOwner,
    CAST(IS_SRVROLEMEMBER('sysadmin', SUSER_SNAME(a.owner_sid)) AS VARCHAR(5)) AS OwnerIsSysadmin,
    CAST(a.is_trustworthy_on AS VARCHAR(5)) AS is_trustworthy_on,
    CAST(a.is_db_chaining_on AS VARCHAR(5)) AS is_db_chaining_on,
    CAST(a.is_broker_enabled AS VARCHAR(5)) AS is_broker_enabled,
    CAST(a.is_encrypted AS VARCHAR(5)) AS is_encrypted,
    CAST(a.is_read_only AS VARCHAR(5)) AS is_read_only,
    CONVERT(VARCHAR(30), a.create_date, 120) AS create_date,
    a.recovery_model_desc,
    b.filename AS FileName,
    CAST((SELECT SUM(size)*8./1024 FROM sys.master_files WHERE database_id = a.database_id) AS VARCHAR(20)) AS DbSizeMb,
    CAST(HAS_DBACCESS(a.name) AS VARCHAR(5)) AS has_dbaccess
FROM sys.databases a
INNER JOIN sys.sysdatabases b ON a.database_id = b.dbid
WHERE 1=1" + filters + @"
ORDER BY a.database_id";

            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            foreach (var row in rows)
            {
                var result = new SqlDatabaseResult
                {
                    DatabaseId = SqlValueFormatter.Format(row["DatabaseId"]),
                    DatabaseName = SqlValueFormatter.Format(row["DatabaseName"]),
                    DatabaseOwner = SqlValueFormatter.Format(row["DatabaseOwner"]),
                    OwnerIsSysadmin = SqlValueFormatter.Format(row["OwnerIsSysadmin"]),
                    is_trustworthy_on = SqlValueFormatter.Format(row["is_trustworthy_on"]),
                    is_db_chaining_on = SqlValueFormatter.Format(row["is_db_chaining_on"]),
                    is_broker_enabled = SqlValueFormatter.Format(row["is_broker_enabled"]),
                    is_encrypted = SqlValueFormatter.Format(row["is_encrypted"]),
                    is_read_only = SqlValueFormatter.Format(row["is_read_only"]),
                    create_date = SqlValueFormatter.Format(row["create_date"]),
                    recovery_model_desc = SqlValueFormatter.Format(row["recovery_model_desc"]),
                    FileName = SqlValueFormatter.Format(row["FileName"]),
                    DbSizeMb = SqlValueFormatter.Format(row["DbSizeMb"]),
                    has_dbaccess = SqlValueFormatter.Format(row["has_dbaccess"])
                };
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlServerLinkResult> GetSqlServerLink(
            SqlConnectionOptions options,
            string instance,
            string databaseLinkName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var filter = string.IsNullOrEmpty(databaseLinkName)
                ? string.Empty
                : " WHERE a.name LIKE " + SqlValueFormatter.QuoteLiteral("%" + databaseLinkName + "%");

            var query = @"
SELECT
    CAST(a.server_id AS VARCHAR(20)) AS DatabaseLinkId,
    a.name AS DatabaseLinkName,
    CASE a.server_id WHEN 0 THEN 'Local' ELSE 'Remote' END AS DatabaseLinkLocation,
    a.product AS Product,
    a.provider AS Provider,
    a.catalog AS [Catalog],
    CASE b.uses_self_credential WHEN 1 THEN 'Uses Self Credentials' ELSE c.name END AS LocalLogin,
    b.remote_name AS RemoteLoginName,
    CAST(a.is_rpc_out_enabled AS VARCHAR(5)) AS is_rpc_out_enabled,
    CAST(a.is_data_access_enabled AS VARCHAR(5)) AS is_data_access_enabled,
    CONVERT(VARCHAR(30), a.modify_date, 120) AS modify_date
FROM master.sys.servers a
LEFT JOIN master.sys.linked_logins b ON a.server_id = b.server_id
LEFT JOIN master.sys.server_principals c ON c.principal_id = b.local_principal_id" + filter;

            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            foreach (var row in rows)
            {
                var result = new SqlServerLinkResult
                {
                    DatabaseLinkId = SqlValueFormatter.Format(row["DatabaseLinkId"]),
                    DatabaseLinkName = SqlValueFormatter.Format(row["DatabaseLinkName"]),
                    DatabaseLinkLocation = SqlValueFormatter.Format(row["DatabaseLinkLocation"]),
                    Product = SqlValueFormatter.Format(row["Product"]),
                    Provider = SqlValueFormatter.Format(row["Provider"]),
                    Catalog = SqlValueFormatter.Format(row["Catalog"]),
                    LocalLogin = SqlValueFormatter.Format(row["LocalLogin"]),
                    RemoteLoginName = SqlValueFormatter.Format(row["RemoteLoginName"]),
                    is_rpc_out_enabled = SqlValueFormatter.Format(row["is_rpc_out_enabled"]),
                    is_data_access_enabled = SqlValueFormatter.Format(row["is_data_access_enabled"]),
                    modify_date = SqlValueFormatter.Format(row["modify_date"])
                };
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlServerLoginResult> GetSqlServerLogin(
            SqlConnectionOptions options,
            string instance,
            string principalName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var filter = string.IsNullOrEmpty(principalName)
                ? string.Empty
                : " AND name LIKE " + SqlValueFormatter.QuoteLiteral("%" + principalName + "%");

            var query = @"
USE master;
SELECT
    CAST(principal_id AS VARCHAR(20)) AS PrincipalId,
    name AS PrincipalName,
    sid AS PrincipalSid,
    type_desc AS PrincipalType,
    CONVERT(VARCHAR(30), create_date, 120) AS CreateDate,
    CAST(LOGINPROPERTY(name, 'IsLocked') AS VARCHAR(5)) AS IsLocked
FROM sys.server_principals
WHERE type IN ('S','U','C')" + filter;

            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            foreach (var row in rows)
            {
                var result = new SqlServerLoginResult
                {
                    PrincipalId = SqlValueFormatter.Format(row["PrincipalId"]),
                    PrincipalName = SqlValueFormatter.Format(row["PrincipalName"]),
                    PrincipalSid = SqlValueFormatter.Format(row["PrincipalSid"]),
                    PrincipalType = SqlValueFormatter.Format(row["PrincipalType"]),
                    CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                    IsLocked = SqlValueFormatter.Format(row["IsLocked"])
                };
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlSysadminCheckResult> GetSqlSysadminCheck(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
SELECT
    SYSTEM_USER AS CurrentLogin,
    CAST(IS_SRVROLEMEMBER('sysadmin') AS VARCHAR(5)) AS IsSysadmin";

            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            foreach (var row in rows)
            {
                var result = new SqlSysadminCheckResult
                {
                    CurrentLogin = SqlValueFormatter.Format(row["CurrentLogin"]),
                    IsSysadmin = SqlValueFormatter.Format(row["IsSysadmin"])
                };
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlTableResult> GetSqlTable(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            string tableName,
            bool noDefaults,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = GetSqlDatabase(
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

                var tableFilter = SqlValueFormatter.LikeFilter("t.TABLE_NAME", tableName);
                var query = string.Format(@"
USE [{0}];
SELECT
    t.TABLE_CATALOG AS DatabaseName,
    t.TABLE_SCHEMA AS SchemaName,
    t.TABLE_NAME AS TableName,
    CASE
        WHEN t.TABLE_NAME LIKE '##%%' THEN 'GlobalTempTable'
        WHEN t.TABLE_NAME LIKE '#%%' THEN 'LocalTempTable'
        WHEN t.TABLE_TYPE = 'VIEW' THEN 'View'
        ELSE 'BASE TABLE'
    END AS TableType,
    CAST(s.is_ms_shipped AS VARCHAR(5)) AS is_ms_shipped,
    CAST(s.is_published AS VARCHAR(5)) AS is_published,
    CAST(s.is_schema_published AS VARCHAR(5)) AS is_schema_published,
    CONVERT(VARCHAR(30), s.create_date, 120) AS create_date,
    CONVERT(VARCHAR(30), s.modify_date, 120) AS modified_date
FROM [{0}].INFORMATION_SCHEMA.TABLES t
INNER JOIN sys.tables st ON st.name = t.TABLE_NAME AND SCHEMA_NAME(st.schema_id) = t.TABLE_SCHEMA
INNER JOIN sys.objects s ON s.object_id = st.object_id
WHERE t.TABLE_TYPE = 'BASE TABLE'{1}
ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME",
                    db.Replace("]", "]]"),
                    tableFilter);

                foreach (var row in QueryExecutor.ExecuteQuery(dbOptions, query, verbose, true))
                {
                    var result = new SqlTableResult
                    {
                        DatabaseName = SqlValueFormatter.Format(row["DatabaseName"]),
                        SchemaName = SqlValueFormatter.Format(row["SchemaName"]),
                        TableName = SqlValueFormatter.Format(row["TableName"]),
                        TableType = SqlValueFormatter.Format(row["TableType"]),
                        is_ms_shipped = SqlValueFormatter.Format(row["is_ms_shipped"]),
                        is_published = SqlValueFormatter.Format(row["is_published"]),
                        is_schema_published = SqlValueFormatter.Format(row["is_schema_published"]),
                        create_date = SqlValueFormatter.Format(row["create_date"]),
                        modified_date = SqlValueFormatter.Format(row["modified_date"])
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlColumnResult> GetSqlColumn(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            string tableName,
            string columnName,
            string columnNameSearch,
            bool noDefaults,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var databases = GetSqlDatabase(
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

                var filters = new StringBuilder();
                filters.Append(SqlValueFormatter.LikeFilter("t.TABLE_NAME", tableName));
                if (!string.IsNullOrEmpty(columnName))
                {
                    filters.Append(SqlValueFormatter.LikeFilter("c.COLUMN_NAME", columnName));
                }

                if (!string.IsNullOrEmpty(columnNameSearch))
                {
                    var terms = columnNameSearch.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (terms.Length > 0)
                    {
                        filters.Append(" AND (");
                        for (var i = 0; i < terms.Length; i++)
                        {
                            if (i > 0)
                            {
                                filters.Append(" OR ");
                            }

                            filters.Append("c.COLUMN_NAME LIKE ")
                                .Append(SqlValueFormatter.QuoteLiteral("%" + terms[i].Trim() + "%"));
                        }

                        filters.Append(")");
                    }
                }

                var query = string.Format(@"
USE [{0}];
SELECT
    t.TABLE_CATALOG AS DatabaseName,
    t.TABLE_SCHEMA AS SchemaName,
    t.TABLE_NAME AS TableName,
    CASE
        WHEN t.TABLE_NAME LIKE '##%%' THEN 'GlobalTempTable'
        WHEN t.TABLE_NAME LIKE '#%%' THEN 'LocalTempTable'
        ELSE 'BASE TABLE'
    END AS TableType,
    c.COLUMN_NAME AS ColumnName,
    c.DATA_TYPE AS ColumnDataType,
    CAST(c.CHARACTER_MAXIMUM_LENGTH AS VARCHAR(20)) AS ColumnMaxLength,
    CAST(st.is_ms_shipped AS VARCHAR(5)) AS is_ms_shipped,
    CAST(st.is_published AS VARCHAR(5)) AS is_published,
    CAST(st.is_schema_published AS VARCHAR(5)) AS is_schema_published,
    CONVERT(VARCHAR(30), st.create_date, 120) AS create_date,
    CONVERT(VARCHAR(30), st.modify_date, 120) AS modified_date
FROM [{0}].INFORMATION_SCHEMA.TABLES t
INNER JOIN sys.tables st ON st.name = t.TABLE_NAME AND SCHEMA_NAME(st.schema_id) = t.TABLE_SCHEMA
INNER JOIN [{0}].INFORMATION_SCHEMA.COLUMNS c
    ON c.TABLE_CATALOG = t.TABLE_CATALOG AND c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
WHERE t.TABLE_TYPE = 'BASE TABLE'{1}
ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION",
                    db.Replace("]", "]]"),
                    filters);

                foreach (var row in QueryExecutor.ExecuteQuery(dbOptions, query, verbose, true))
                {
                    var result = new SqlColumnResult
                    {
                        DatabaseName = SqlValueFormatter.Format(row["DatabaseName"]),
                        SchemaName = SqlValueFormatter.Format(row["SchemaName"]),
                        TableName = SqlValueFormatter.Format(row["TableName"]),
                        TableType = SqlValueFormatter.Format(row["TableType"]),
                        ColumnName = SqlValueFormatter.Format(row["ColumnName"]),
                        ColumnDataType = SqlValueFormatter.Format(row["ColumnDataType"]),
                        ColumnMaxLength = SqlValueFormatter.Format(row["ColumnMaxLength"]),
                        is_ms_shipped = SqlValueFormatter.Format(row["is_ms_shipped"]),
                        is_published = SqlValueFormatter.Format(row["is_published"]),
                        is_schema_published = SqlValueFormatter.Format(row["is_schema_published"]),
                        create_date = SqlValueFormatter.Format(row["create_date"]),
                        modified_date = SqlValueFormatter.Format(row["modified_date"])
                    };
                    SqlValueFormatter.StampInstance(result, instance);
                    yield return result;
                }
            }
        }

        internal static IEnumerable<SqlColumnSampleDataResult> GetSqlColumnSampleData(
            SqlConnectionOptions options,
            string instance,
            string keywords,
            int sampleSize,
            string databaseName,
            bool validateCc,
            bool noDefaults,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var columns = GetSqlColumn(
                options,
                instance,
                databaseName,
                null,
                null,
                keywords ?? "Password",
                noDefaults,
                verbose,
                suppressVerbose).ToList();

            foreach (var col in columns)
            {
                var dbOptions = options.Clone();
                dbOptions.Database = col.DatabaseName;

                var sampleQuery = string.Format(@"
USE {0};
SELECT TOP {1} CAST({2} AS VARCHAR(8000)) AS [Sample]
FROM {3}.{4}
WHERE {2} IS NOT NULL",
                    SqlValueFormatter.BracketIdentifier(col.DatabaseName),
                    sampleSize <= 0 ? 1 : sampleSize,
                    SqlValueFormatter.BracketIdentifier(col.ColumnName),
                    SqlValueFormatter.BracketIdentifier(col.SchemaName),
                    SqlValueFormatter.BracketIdentifier(col.TableName));

                var countQuery = string.Format(@"
USE {0};
SELECT CAST(COUNT({1}) AS VARCHAR(20)) AS [RowCount]
FROM {2}.{3}
WHERE {1} IS NOT NULL",
                    SqlValueFormatter.BracketIdentifier(col.DatabaseName),
                    SqlValueFormatter.BracketIdentifier(col.ColumnName),
                    SqlValueFormatter.BracketIdentifier(col.SchemaName),
                    SqlValueFormatter.BracketIdentifier(col.TableName));

                var sampleRows = QueryExecutor.ExecuteQuery(dbOptions, sampleQuery, verbose, true);
                var countRows = QueryExecutor.ExecuteQuery(dbOptions, countQuery, verbose, true);
                var sample = sampleRows.Count > 0 ? SqlValueFormatter.Format(sampleRows[0]["Sample"]) : string.Empty;
                var rowCount = countRows.Count > 0 ? SqlValueFormatter.Format(countRows[0]["RowCount"]) : "0";

                var result = new SqlColumnSampleDataResult
                {
                    Database = col.DatabaseName,
                    Schema = col.SchemaName,
                    Table = col.TableName,
                    Column = col.ColumnName,
                    Sample = sample,
                    RowCount = rowCount,
                    IsCC = validateCc && LuhnHelper.IsValidCreditCard(sample) ? "True" : "False"
                };
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlAgentJobResult> GetSqlAgentJob(
            SqlConnectionOptions options,
            string instance,
            string subSystem,
            string keyword,
            bool usingProxyCredential,
            string proxyCredential,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var filters = new StringBuilder();
            if (!string.IsNullOrEmpty(subSystem))
            {
                filters.Append(" AND steps.subsystem LIKE ")
                    .Append(SqlValueFormatter.QuoteLiteral("%" + subSystem + "%"));
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                filters.Append(" AND (job.name LIKE ")
                    .Append(SqlValueFormatter.QuoteLiteral("%" + keyword + "%"))
                    .Append(" OR steps.command LIKE ")
                    .Append(SqlValueFormatter.QuoteLiteral("%" + keyword + "%"))
                    .Append(")");
            }

            if (usingProxyCredential)
            {
                filters.Append(" AND steps.proxy_id <> 0");
            }

            if (!string.IsNullOrEmpty(proxyCredential))
            {
                filters.Append(" AND proxies.name LIKE ")
                    .Append(SqlValueFormatter.QuoteLiteral("%" + proxyCredential + "%"));
            }

            var query = @"
SELECT
    steps.database_name AS DatabaseName,
    CAST(job.job_id AS VARCHAR(50)) AS Job_Id,
    job.name AS Job_Name,
    job.description AS Job_Description,
    SUSER_SNAME(job.owner_sid) AS Job_Owner,
    CAST(steps.proxy_id AS VARCHAR(20)) AS Proxy_Id,
    proxies.name AS Proxy_Credential,
    CONVERT(VARCHAR(30), job.date_created, 120) AS Date_Created,
    CAST(steps.last_run_date AS VARCHAR(20)) AS Last_Run_Date,
    CAST(job.enabled AS VARCHAR(5)) AS Enabled,
    steps.server AS [Server],
    steps.step_name AS Step_Name,
    steps.subsystem AS SubSystem,
    steps.command AS Command
FROM msdb.dbo.sysjobs job
INNER JOIN msdb.dbo.sysjobsteps steps ON job.job_id = steps.job_id
LEFT JOIN msdb.dbo.sysproxies proxies ON steps.proxy_id = proxies.proxy_id
WHERE 1=1" + filters;

            var jobOptions = options.Clone();
            jobOptions.Database = "msdb";

            foreach (var row in QueryExecutor.ExecuteQuery(jobOptions, query, verbose, suppressVerbose))
            {
                var result = new SqlAgentJobResult
                {
                    DatabaseName = SqlValueFormatter.Format(row["DatabaseName"]),
                    Job_Id = SqlValueFormatter.Format(row["Job_Id"]),
                    Job_Name = SqlValueFormatter.Format(row["Job_Name"]),
                    Job_Description = SqlValueFormatter.Format(row["Job_Description"]),
                    Job_Owner = SqlValueFormatter.Format(row["Job_Owner"]),
                    Proxy_Id = SqlValueFormatter.Format(row["Proxy_Id"]),
                    Proxy_Credential = SqlValueFormatter.Format(row["Proxy_Credential"]),
                    Date_Created = SqlValueFormatter.Format(row["Date_Created"]),
                    Last_Run_Date = SqlValueFormatter.Format(row["Last_Run_Date"]),
                    Enabled = SqlValueFormatter.Format(row["Enabled"]),
                    Server = SqlValueFormatter.Format(row["Server"]),
                    Step_Name = SqlValueFormatter.Format(row["Step_Name"]),
                    SubSystem = SqlValueFormatter.Format(row["SubSystem"]),
                    Command = SqlValueFormatter.Format(row["Command"])
                };
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }

        internal static IEnumerable<SqlGenericRowResult> GetGenericRows(
            SqlConnectionOptions options,
            string instance,
            string query,
            string category,
            VerboseWriter verbose,
            bool suppressVerbose,
            Func<Dictionary<string, object>, SqlGenericRowResult> map)
        {
            foreach (var row in QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose))
            {
                var result = map(row);
                result.Category = category;
                SqlValueFormatter.StampInstance(result, instance);
                yield return result;
            }
        }
    }
}
