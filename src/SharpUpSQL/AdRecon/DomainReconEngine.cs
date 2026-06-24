using System;
using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.AdRecon
{
    internal sealed class DomainReconRequest
    {
        internal SqlConnectionOptions Options { get; set; }
        internal string Instance { get; set; }
        internal string LinkUsername { get; set; }
        internal string LinkPassword { get; set; }
        internal bool UseAdHoc { get; set; }
        internal string TargetDomain { get; set; }
        internal string LdapPath { get; set; }
        internal string LdapFilter { get; set; }
        internal string LdapFields { get; set; }
        internal VerboseWriter Verbose { get; set; }
        internal bool SuppressVerbose { get; set; }
    }

    internal static class DomainReconEngine
    {
        private static readonly Random Random = new Random();

        internal static IEnumerable<SqlQueryResult> GetSqlDomainObject(DomainReconRequest request)
        {
            var instance = string.IsNullOrWhiteSpace(request.Instance)
                ? Environment.MachineName
                : request.Instance;

            var options = request.Options.Clone();
            options.Instance = instance;

            var test = ConnectionTester.Test(instance, null, null, options, request.Verbose, request.SuppressVerbose);
            if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Connection Failed.");
                }

                yield break;
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Connection Success.");
            }

            var serverInfo = SqlEnumerationEngine.GetSqlServerInfo(options, instance, request.Verbose, true).FirstOrDefault();
            if (serverInfo == null)
            {
                yield break;
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Login: " + serverInfo.Currentlogin);
                request.Verbose.Write(instance + " : Domain: " + serverInfo.DomainName);
                request.Verbose.Write(instance + " : Version: SQL Server " + serverInfo.SQLServerMajorVersion + " " +
                                      serverInfo.SQLServerEdition + " (" + serverInfo.SQLServerVersionNumber + ")");
            }

            if (!string.Equals(serverInfo.IsSysadmin, "1", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(serverInfo.IsSysadmin, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Sysadmin: No");
                    request.Verbose.Write(instance + " : This command requires sysadmin privileges. Exiting.");
                }

                yield break;
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Sysadmin: Yes");
            }

            var currentLogin = serverInfo.Currentlogin ?? string.Empty;
            if (currentLogin.IndexOf('\\') < 0)
            {
                if (!request.UseAdHoc && string.IsNullOrEmpty(request.LinkPassword))
                {
                    request.Verbose.Write(instance + " : A SQL Login with sysadmin privileges cannot execute ASDI queries through a linked server by itself.");
                    request.Verbose.Write(instance + " : Try one of the following:");
                    request.Verbose.Write(instance + " :  - Run the command again with the -UseAdHoc flag ");
                    request.Verbose.Write(instance + " :  - Run the command again and provide -LinkUser and -LinkPassword");
                    yield break;
                }
            }

            var ldapPath = request.LdapPath;
            if (string.IsNullOrWhiteSpace(ldapPath))
            {
                ldapPath = !string.IsNullOrWhiteSpace(request.TargetDomain)
                    ? request.TargetDomain
                    : serverInfo.DomainName;
            }

            if (!IsAdsiProviderEnabled(options, instance, request.Verbose, request.SuppressVerbose))
            {
                yield break;
            }

            if (request.UseAdHoc)
            {
                if (!request.SuppressVerbose)
                {
                    if (currentLogin.IndexOf('\\') >= 0)
                    {
                        request.Verbose.Write(instance + " : Executing in AdHoc mode using OpenRowSet as '" + currentLogin + "'.");
                    }
                    else if (string.IsNullOrEmpty(request.LinkUsername))
                    {
                        request.Verbose.Write(instance + " : Executing in AdHoc mode using OpenRowSet as the SQL Server service account.");
                    }
                    else
                    {
                        request.Verbose.Write(instance + " : Executing in AdHoc mode using OpenRowSet as '" + request.LinkUsername + "'.");
                    }
                }
            }
            else if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Executing in Link mode using OpenQuery.");
            }

            string linkName = null;
            int originalShowAdv = 0;
            int originalAdHoc = 0;

            try
            {
                if (!request.UseAdHoc)
                {
                    linkName = GenerateLinkName();
                    if (!request.SuppressVerbose)
                    {
                        request.Verbose.Write(instance + " : Creating ADSI SQL Server link named " + linkName + ".");
                    }

                    CreateAdsiLink(options, instance, linkName, request);
                    AssociateAdsiLinkLogin(options, instance, linkName, request, currentLogin);
                }
                else
                {
                    originalShowAdv = GetConfigValue(options, instance, "show advanced options", request);
                    originalAdHoc = GetConfigValue(options, instance, "Ad Hoc Distributed Queries", request);
                    EnableAdHocQueries(options, instance, request, originalShowAdv, originalAdHoc);
                }

                var query = BuildLdapQuery(request, ldapPath, linkName);
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : LDAP query against logon server using ADSI OLEDB started...");
                }

                foreach (var row in SqlEnumerationEngine.GetSqlQuery(options, query, instance, request.Verbose, request.SuppressVerbose))
                {
                    yield return row;
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : LDAP query against logon server using ADSI OLEDB complete.");
                }
            }
            finally
            {
                if (!request.UseAdHoc && !string.IsNullOrEmpty(linkName))
                {
                    if (!request.SuppressVerbose)
                    {
                        request.Verbose.Write(instance + " : Removing ADSI SQL Server link named " + linkName);
                    }

                    var removeQuery = "EXEC master.dbo.sp_dropserver @server=N'" + EscapeSql(linkName) +
                                      "', @droplogins='droplogins'";
                    SqlEnumerationEngine.GetSqlQuery(options, removeQuery, instance, request.Verbose, true).ToList();
                }

                if (request.UseAdHoc)
                {
                    if (!request.SuppressVerbose)
                    {
                        request.Verbose.Write(instance + " : Restoring AdHoc settings if needed.");
                    }

                    RestoreAdHocQueries(options, instance, request, originalShowAdv, originalAdHoc);
                }
            }
        }

        internal static string ResolveDomainDistinguishedName(DomainReconRequest request, string domain)
        {
            var dnRequest = CloneForLookup(request);
            dnRequest.LdapPath = domain;
            dnRequest.LdapFilter = "(name=" + domain + ")";
            dnRequest.LdapFields = "distinguishedname";

            var row = GetSqlDomainObject(dnRequest).FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            object value;
            if (row.Columns.TryGetValue("distinguishedname", out value))
            {
                return SqlValueFormatter.Format(value);
            }

            return null;
        }

        internal static string BuildUserStateFilter(string userState)
        {
            if (string.IsNullOrWhiteSpace(userState) || string.Equals(userState, "All", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            switch (userState)
            {
                case "Enabled":
                    return "(!userAccountControl:1.2.840.113556.1.4.803:=2)";
                case "Disabled":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=2)";
                case "Locked":
                    return "(sAMAccountType=805306368)(lockoutTime>0)";
                case "PwNeverExpires":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=65536)";
                case "PwNotRequired":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=32)";
                case "PwStoredRevEnc":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=128)";
                case "PreAuthNotRequired":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=4194304)";
                case "SmartCardRequired":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=262144)";
                case "TrustedForDelegation":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=524288)";
                case "TrustedToAuthForDelegation":
                    return "(userAccountControl:1.2.840.113556.1.4.803:=16777216)";
                default:
                    return string.Empty;
            }
        }

        internal static string BuildPwLastSetFilter(int pwLastSetDays)
        {
            if (pwLastSetDays <= 0)
            {
                return string.Empty;
            }

            var timestamp = DateTime.Now.AddDays(-pwLastSetDays).ToFileTime();
            return "(!pwdLastSet>=" + timestamp + ")";
        }

        internal static string ResolveTrustDirection(string value)
        {
            int direction;
            if (!int.TryParse(value, out direction))
            {
                return value;
            }

            switch (direction)
            {
                case 0: return "Disabled";
                case 1: return "Inbound";
                case 2: return "Outbound";
                case 3: return "Bidirectional";
                default: return value;
            }
        }

        internal static string ResolveTrustAttribute(string value)
        {
            int attribute;
            if (!int.TryParse(value, out attribute))
            {
                return value;
            }

            switch (attribute)
            {
                case 0x001: return "non_transitive";
                case 0x002: return "uplevel_only";
                case 0x004: return "quarantined_domain";
                case 0x008: return "forest_transitive";
                case 0x010: return "cross_organization";
                case 0x020: return "within_forest";
                case 0x040: return "treat_as_external";
                case 0x080: return "trust_uses_rc4_encryption";
                case 0x100: return "trust_uses_aes_keys";
                default: return value.ToString();
            }
        }

        internal static string ResolveTrustType(string value)
        {
            int trustType;
            if (!int.TryParse(value, out trustType))
            {
                return value;
            }

            switch (trustType)
            {
                case 1: return "Downlevel Trust (Windows NT domain external)";
                case 2: return "Uplevel Trust (Active Directory domain - parent-child, root domain, shortcut, external, or forest)";
                case 3: return "MIT (non-Windows Kerberos version 5 realm)";
                case 4: return "DCE (Theoretical trust type - DCE refers to Open Group's Distributed Computing)";
                default: return value;
            }
        }

        internal static string FormatAccountPolicyDuration(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            long value;
            if (!long.TryParse(raw.Replace("-", string.Empty), out value))
            {
                return raw;
            }

            return (value / (60 * 10000000L)).ToString();
        }

        internal static string FormatAccountPolicyAge(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            long value;
            if (!long.TryParse(raw.Replace("-", string.Empty), out value))
            {
                return raw;
            }

            var minutes = value / (60 * 10000000L);
            return Math.Floor((decimal)minutes / 60m / 24m).ToString();
        }

        private static DomainReconRequest CloneForLookup(DomainReconRequest request)
        {
            return new DomainReconRequest
            {
                Options = request.Options,
                Instance = request.Instance,
                LinkUsername = request.LinkUsername,
                LinkPassword = request.LinkPassword,
                UseAdHoc = request.UseAdHoc,
                TargetDomain = request.TargetDomain,
                Verbose = request.Verbose,
                SuppressVerbose = true
            };
        }

        private static bool IsAdsiProviderEnabled(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var query = @"
DECLARE @AllowInProcess int
SET @AllowInProcess = 0
EXEC sys.xp_instance_regread
    N'HKEY_LOCAL_MACHINE',
    N'SOFTWARE\Microsoft\MSSQLServer\Providers\ADsDSOObject',
    N'AllowInProcess',
    @AllowInProcess OUTPUT
SELECT @AllowInProcess AS AllowInProcess";

            var row = SqlEnumerationEngine.GetSqlQuery(options, query, instance, verbose, true).FirstOrDefault();
            var enabled = row != null &&
                          string.Equals(SqlValueFormatter.Format(row["AllowInProcess"]), "1", StringComparison.OrdinalIgnoreCase);

            if (!suppressVerbose)
            {
                verbose.Write(instance + " : ADsDSOObject provider allowed to run in process: " + (enabled ? "Yes" : "No"));
            }

            if (!enabled)
            {
                verbose.Write(instance + " : The ADsDSOObject provider is not allowed to run in process. Stopping operation.");
            }

            return enabled;
        }

        private static void CreateAdsiLink(
            SqlConnectionOptions options,
            string instance,
            string linkName,
            DomainReconRequest request)
        {
            var query = @"
IF (SELECT count(*) FROM master..sysservers WHERE srvname = '" + EscapeSql(linkName) + @"') = 0
    EXEC master.dbo.sp_addlinkedserver @server = N'" + EscapeSql(linkName) + @"',
        @srvproduct=N'Active Directory Service Interfaces',
        @provider=N'ADSDSOObject',
        @datasrc=N'adsdatasource'
ELSE
    SELECT 'The target SQL Server link already exists.'";

            SqlEnumerationEngine.GetSqlQuery(options, query, instance, request.Verbose, true).ToList();
        }

        private static void AssociateAdsiLinkLogin(
            SqlConnectionOptions options,
            string instance,
            string linkName,
            DomainReconRequest request,
            string currentLogin)
        {
            string query;
            if (!string.IsNullOrEmpty(request.LinkUsername) && !string.IsNullOrEmpty(request.LinkPassword))
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Associating login '" + request.LinkUsername +
                                          "' with ADSI SQL Server link named " + linkName + ".");
                }

                query = "EXEC sp_addlinkedsrvlogin @rmtsrvname=N'" + EscapeSql(linkName) +
                        "',@useself=N'False',@locallogin=NULL,@rmtuser=N'" + EscapeSql(request.LinkUsername) +
                        "',@rmtpassword=N'" + EscapeSql(request.LinkPassword) + "'";
            }
            else
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Associating '" + currentLogin +
                                          "' with ADSI SQL Server link named " + linkName + ".");
                }

                query = "EXEC sp_addlinkedsrvlogin @rmtsrvname=N'" + EscapeSql(linkName) +
                        "',@useself=N'True',@locallogin=NULL,@rmtuser=NULL,@rmtpassword=NULL";
            }

            SqlEnumerationEngine.GetSqlQuery(options, query, instance, request.Verbose, true).ToList();
        }

        private static int GetConfigValue(
            SqlConnectionOptions options,
            string instance,
            string name,
            DomainReconRequest request)
        {
            var query = "SELECT value_in_use FROM master.sys.configurations WHERE name like '" + EscapeSql(name) + "'";
            var row = SqlEnumerationEngine.GetSqlQuery(options, query, instance, request.Verbose, true).FirstOrDefault();
            int value;
            return row != null && int.TryParse(SqlValueFormatter.Format(row["value_in_use"]), out value) ? value : 0;
        }

        private static void EnableAdHocQueries(
            SqlConnectionOptions options,
            string instance,
            DomainReconRequest request,
            int originalShowAdv,
            int originalAdHoc)
        {
            if (originalShowAdv == 0)
            {
                SqlEnumerationEngine.GetSqlQuery(options,
                    "sp_configure 'Show Advanced Options',1;RECONFIGURE",
                    instance,
                    request.Verbose,
                    true).ToList();

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabling 'Show Advanced Options'");
                }
            }
            else if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : 'Show Advanced Options' is already enabled");
            }

            if (originalAdHoc == 0)
            {
                SqlEnumerationEngine.GetSqlQuery(options,
                    "sp_configure 'Ad Hoc Distributed Queries',1;RECONFIGURE",
                    instance,
                    request.Verbose,
                    true).ToList();

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabling 'Ad Hoc Distributed Queries'");
                }
            }
            else if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : 'Ad Hoc Distributed Queries' are already enabled");
            }
        }

        private static void RestoreAdHocQueries(
            SqlConnectionOptions options,
            string instance,
            DomainReconRequest request,
            int originalShowAdv,
            int originalAdHoc)
        {
            SqlEnumerationEngine.GetSqlQuery(options,
                "sp_configure 'Ad Hoc Distributed Queries'," + originalAdHoc + ";RECONFIGURE",
                instance,
                request.Verbose,
                true).ToList();

            SqlEnumerationEngine.GetSqlQuery(options,
                "sp_configure 'Show Advanced Options'," + originalShowAdv + ";RECONFIGURE",
                instance,
                request.Verbose,
                true).ToList();
        }

        private static string BuildLdapQuery(DomainReconRequest request, string ldapPath, string linkName)
        {
            var ldapFilter = request.LdapFilter ?? string.Empty;
            var ldapFields = request.LdapFields ?? string.Empty;

            if (request.UseAdHoc)
            {
                var adHocAuth = !string.IsNullOrEmpty(request.LinkUsername) && !string.IsNullOrEmpty(request.LinkPassword)
                    ? "User ID=" + request.LinkUsername + "; Password=" + request.LinkPassword + ";"
                    : "adsdatasource";

                return "SELECT * FROM OPENROWSET('ADSDSOOBJECT','" + EscapeSql(adHocAuth) + "','<LDAP://" +
                       ldapPath + ";" + ldapFilter + ";" + ldapFields + ";subtree>')";
            }

            return "SELECT * FROM OpenQuery(" + linkName + ",'<LDAP://" + ldapPath + ";" + ldapFilter + ";" +
                   ldapFields + ";subtree>')";
        }

        private static string GenerateLinkName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Range(0, 8).Select(_ => chars[Random.Next(chars.Length)]).ToArray());
        }

        private static string EscapeSql(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }
    }
}
