using System;
using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Audit
{
    internal static class DefaultPasswordCatalog
    {
        internal sealed class DefaultCredentialResult
        {
            public string Instance { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string IsSysadmin { get; set; }
        }

        private static readonly Tuple<string, string, string>[] Entries =
        {
            Tuple.Create("ACS", "ej", "ej"),
            Tuple.Create("ACT7", "sa", "sage"),
            Tuple.Create("AOM2", "admin", "ca_admin"),
            Tuple.Create("ARIS", "ARIS9", "*ARIS!1dm9n#"),
            Tuple.Create("AutodeskVault", "sa", "AutodeskVault@26200"),
            Tuple.Create("BOSCHSQL", "sa", "RPSsql12345"),
            Tuple.Create("BPASERVER9", "sa", "AutoMateBPA9"),
            Tuple.Create("CDRDICOM", "sa", "CDRDicom50!"),
            Tuple.Create("CODEPAL", "sa", "Cod3p@l"),
            Tuple.Create("CODEPAL08", "sa", "Cod3p@l"),
            Tuple.Create("CounterPoint", "sa", "CounterPoint8"),
            Tuple.Create("CSSQL05", "ELNAdmin", "ELNAdmin"),
            Tuple.Create("CSSQL05", "sa", "CambridgeSoft_SA"),
            Tuple.Create("CADSQL", "CADSQLAdminUser", "Cr41g1sth3M4n!"),
            Tuple.Create("DHLEASYSHIP", "sa", "DHLadmin@1"),
            Tuple.Create("DPM", "admin", "ca_admin"),
            Tuple.Create("DVTEL", "sa", string.Empty),
            Tuple.Create("EASYSHIP", "sa", "DHLadmin@1"),
            Tuple.Create("ECC", "sa", "Webgility2011"),
            Tuple.Create("ECOPYDB", "e+C0py2007_@x", "e+C0py2007_@x"),
            Tuple.Create("ECOPYDB", "sa", "ecopy"),
            Tuple.Create("Emerson2012", "sa", "42Emerson42Eme"),
            Tuple.Create("HDPS", "sa", "sa"),
            Tuple.Create("HPDSS", "sa", "Hpdsdb000001"),
            Tuple.Create("HPDSS", "sa", "hpdss"),
            Tuple.Create("INSERTGT", "msi", "keyboa5"),
            Tuple.Create("INSERTGT", "sa", string.Empty),
            Tuple.Create("INTRAVET", "sa", "Webster#1"),
            Tuple.Create("MYMOVIES", "sa", "t9AranuHA7"),
            Tuple.Create("PCAMERICA", "sa", "pcAmer1ca"),
            Tuple.Create("PCAMERICA", "sa", "PCAmerica"),
            Tuple.Create("PRISM", "sa", "SecurityMaster08"),
            Tuple.Create("RMSQLDATA", "Super", "Orange"),
            Tuple.Create("RTCLOCAL", "sa", "mypassword"),
            Tuple.Create("RBAT", "sa", "34TJ4@#$"),
            Tuple.Create("RIT", "sa", "34TJ4@#$"),
            Tuple.Create("RCO", "sa", "34TJ4@#$"),
            Tuple.Create("REDBEAM", "sa", "34TJ4@#$"),
            Tuple.Create("SALESLOGIX", "sa", "SLXMaster"),
            Tuple.Create("SIDEXIS_SQL", "sa", "2BeChanged"),
            Tuple.Create("SQL2K5", "ovsd", "ovsd"),
            Tuple.Create("SQLEXPRESS", "admin", "ca_admin"),
            Tuple.Create("STANDARDDEV2014", "test", "test"),
            Tuple.Create("TEW_SQLEXPRESS", "tew", "tew"),
            Tuple.Create("vocollect", "vocollect", "vocollect"),
            Tuple.Create("VSDOTNET", "sa", string.Empty),
            Tuple.Create("VSQL", "sa", "111"),
            Tuple.Create("CASEWISE", "sa", string.Empty),
            Tuple.Create("VANTAGE", "sa", "vantage12!"),
            Tuple.Create("BCM", "bcmdbuser", "Bcmuser@06"),
            Tuple.Create("BCM", "bcmdbuser", "Numara@06"),
            Tuple.Create("DEXIS_DATA", "sa", "dexis"),
            Tuple.Create("DEXIS_DATA", "dexis", "dexis"),
            Tuple.Create("SMTKINGDOM", "SMTKINGDOM", "$ei$micMicro"),
            Tuple.Create("RE7_MS", "Supervisor", "Supervisor"),
            Tuple.Create("RE7_MS", "Admin", "Admin"),
            Tuple.Create("OHD", "sa", "ohdusa@123"),
            Tuple.Create("UPC", "serviceadmin", "Password.0"),
            Tuple.Create("Hirsh", "Velocity", "i5X9FG42"),
            Tuple.Create("Hirsh", "sa", "i5X9FG42"),
            Tuple.Create("SPSQL", "sa", "SecurityMaster08"),
            Tuple.Create("CAREWARE", "sa", string.Empty)
        };

        internal static IEnumerable<DefaultCredentialResult> TestInstance(AuditContext context)
        {
            var namedInstance = GetNamedInstance(context.Instance);
            if (string.IsNullOrEmpty(namedInstance))
            {
                yield break;
            }

            var matches = Entries
                .Where(e => string.Equals(e.Item1, namedInstance, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                yield break;
            }

            foreach (var match in matches)
            {
                var testOptions = context.Options.Clone();
                testOptions.Username = match.Item2;
                testOptions.Password = match.Item3;

                try
                {
                    QueryExecutor.ExecuteScalar(testOptions, "SELECT 1", context.Verbose, true);
                }
                catch
                {
                    continue;
                }

                var sysadmin = AuditEngine.IsLoginSysadmin(testOptions, match.Item2, match.Item3)
                    ? "Yes"
                    : "No";
                yield return new DefaultCredentialResult
                {
                    Instance = context.Instance,
                    Username = match.Item2,
                    Password = match.Item3,
                    IsSysadmin = sysadmin
                };
            }
        }

        private static string GetNamedInstance(string instance)
        {
            if (string.IsNullOrEmpty(instance))
            {
                return null;
            }

            var parts = instance.Split(new[] { '\\' }, 2);
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                return null;
            }

            var named = parts[1];
            var comma = named.IndexOf(',');
            if (comma >= 0)
            {
                named = named.Substring(0, comma);
            }

            return named;
        }
    }
}
