using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using SharpUpSQL.Commands;

namespace SharpUpSQL.Common
{
    public sealed class SqlServiceAccountResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string ServiceName { get; set; }
        public string ServiceAccount { get; set; }
        public string ServiceState { get; set; }
    }

    public sealed class SqlLocalAdminCheckResult
    {
        public string ComputerName { get; set; }
        public string CurrentUser { get; set; }
        public string IsLocalAdmin { get; set; }
    }

    internal static class LocalChecks
    {
        internal static IEnumerable<SqlServiceAccountResult> GetSqlServiceAccount(
            string instanceFilter,
            bool runOnly,
            Action<string> verbose)
        {
            return GetSqlServiceLocal.Execute(instanceFilter, runOnly, true, verbose)
                .Select(s => new SqlServiceAccountResult
                {
                    ComputerName = s.ComputerName,
                    Instance = s.Instance,
                    ServiceName = s.ServiceName,
                    ServiceAccount = s.ServiceAccount,
                    ServiceState = s.ServiceState
                });
        }

        internal static SqlLocalAdminCheckResult GetSqlLocalAdminCheck()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

            return new SqlLocalAdminCheckResult
            {
                ComputerName = Environment.MachineName,
                CurrentUser = identity.Name,
                IsLocalAdmin = isAdmin ? "True" : "False"
            };
        }
    }
}
