using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using SharpUpSQL.Commands;

namespace SharpUpSQL.Common
{
    public sealed class SqlServiceLocalResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string ServiceDisplayName { get; set; }
        public string ServiceName { get; set; }
        public string ServicePath { get; set; }
        public string ServiceAccount { get; set; }
        public string ServiceState { get; set; }
        public string ServiceProcessId { get; set; }
    }

    /// <summary>
    /// PowerUpSQL Get-SQLServiceLocal — local WMI service enumeration.
    /// </summary>
    public static class GetSqlServiceLocal
    {
        public const int DefaultWmiTimeOutSeconds = 30;

        public static IEnumerable<SqlServiceLocalResult> Execute(
            string instanceFilter = null,
            bool runOnly = false,
            bool suppressVerbose = false,
            Action<string> verbose = null,
            int wmiTimeOutSeconds = DefaultWmiTimeOutSeconds)
        {
            if (wmiTimeOutSeconds <= 0)
            {
                wmiTimeOutSeconds = DefaultWmiTimeOutSeconds;
            }

            List<SqlServiceLocalResult> results;
            try
            {
                results = EnumerateServices(instanceFilter, runOnly, wmiTimeOutSeconds);
            }
            catch (ManagementException ex)
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose("WMI service enumeration failed: " + ex.Message);
                }

                return new List<SqlServiceLocalResult>();
            }
            catch (Exception ex)
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose("WMI service enumeration failed: " + ex.Message);
                }

                return new List<SqlServiceLocalResult>();
            }

            if (!suppressVerbose && verbose != null)
            {
                verbose(results.Count + " local SQL Server services were found that matched the criteria.");
            }

            return results;
        }

        private static List<SqlServiceLocalResult> EnumerateServices(
            string instanceFilter,
            bool runOnly,
            int wmiTimeOutSeconds)
        {
            var results = new List<SqlServiceLocalResult>();
            var scope = new ManagementScope(@"\\.\root\cimv2");
            var connectionOptions = new ConnectionOptions
            {
                Timeout = TimeSpan.FromSeconds(wmiTimeOutSeconds)
            };
            scope.Options = connectionOptions;
            scope.Connect();

            var query = new ObjectQuery(
                "SELECT DisplayName, PathName, Name, StartName, State, SystemName, ProcessId FROM Win32_Service WHERE DisplayName LIKE 'SQL Server %'");

            using (var searcher = new ManagementObjectSearcher(scope, query))
            {
                searcher.Options.Timeout = TimeSpan.FromSeconds(wmiTimeOutSeconds);

                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    using (obj)
                    {
                        var systemName = obj["SystemName"];
                        var computerName = systemName != null ? systemName.ToString() : Environment.MachineName;
                        var displayNameObj = obj["DisplayName"];
                        var displayName = displayNameObj != null ? displayNameObj.ToString() : string.Empty;
                        var stateObj = obj["State"];
                        var serviceState = stateObj != null ? stateObj.ToString() : string.Empty;
                        var currentInstance = computerName;

                        if (displayName.Contains("("))
                        {
                            var start = displayName.IndexOf('(') + 1;
                            var end = displayName.IndexOf(')', start);
                            if (end > start)
                            {
                                var instanceName = displayName.Substring(start, end - start);
                                currentInstance = computerName + "\\" + instanceName;
                                if (currentInstance.EndsWith("\\MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
                                {
                                    currentInstance = computerName;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(instanceFilter) &&
                            !string.Equals(instanceFilter, currentInstance, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (runOnly && !string.Equals(serviceState, "Running", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var processId = obj["ProcessId"];
                        var processIdString = processId != null && Convert.ToInt32(processId) != 0
                            ? processId.ToString()
                            : string.Empty;

                        var nameObj = obj["Name"];
                        var pathObj = obj["PathName"];
                        var startNameObj = obj["StartName"];

                        results.Add(new SqlServiceLocalResult
                        {
                            ComputerName = computerName,
                            Instance = currentInstance,
                            ServiceDisplayName = displayName,
                            ServiceName = nameObj != null ? nameObj.ToString() : string.Empty,
                            ServicePath = pathObj != null ? pathObj.ToString() : string.Empty,
                            ServiceAccount = startNameObj != null ? startNameObj.ToString() : string.Empty,
                            ServiceState = serviceState,
                            ServiceProcessId = processIdString
                        });
                    }
                }
            }

            return results;
        }
    }
}
