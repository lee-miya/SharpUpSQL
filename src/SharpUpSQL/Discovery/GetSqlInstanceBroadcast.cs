using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Domain;

namespace SharpUpSQL.Discovery
{
    public sealed class SqlInstanceBroadcastResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string IsClustered { get; set; }
        public string Version { get; set; }
    }

    public sealed class SqlInstanceDomainResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string DomainAccountSid { get; set; }
        public string DomainAccount { get; set; }
        public string DomainAccountCn { get; set; }
        public string Service { get; set; }
        public string Spn { get; set; }
        public string LastLogon { get; set; }
        public string Description { get; set; }
        public string IPAddress { get; set; }
    }

    public sealed class GetSqlInstanceBroadcastCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLInstanceBroadcast"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var udpPing = GetSwitch(context, "UDPPing");
            WriteVerbose(context, "Attempting to identify SQL Server instances on the broadcast domain.");

            var table = new List<SqlInstanceBroadcastResult>();
            try
            {
                var instances = SqlDataSourceEnumerator.Instance.GetDataSources();
                foreach (DataRow row in instances.Rows)
                {
                    var serverNameObj = row["ServerName"];
                    var serverName = serverNameObj != null ? serverNameObj.ToString() : string.Empty;
                    var instanceNameObj = row["InstanceName"];
                    var instanceName = instanceNameObj != null ? instanceNameObj.ToString() : string.Empty;
                    var fullInstance = string.IsNullOrEmpty(instanceName)
                        ? serverName
                        : serverName + "\\" + instanceName;

                    var isClusteredObj = row["IsClustered"];
                    var versionObj = row["Version"];

                    table.Add(new SqlInstanceBroadcastResult
                    {
                        ComputerName = serverName,
                        Instance = fullInstance,
                        IsClustered = isClusteredObj != null ? isClusteredObj.ToString() : string.Empty,
                        Version = versionObj != null ? versionObj.ToString() : string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(" Operation Failed.");
                Console.Error.WriteLine(" Error: " + ex.Message);
                yield break;
            }

            WriteVerbose(context, table.Count + " SQL Server instances were found.");

            if (udpPing)
            {
                WriteVerbose(context, "Performing UDP ping against " + table.Count + " SQL Server instances.");
                var udpTimeOut = GetIntArg(context, "UDPTimeOut", 2);
                foreach (var item in table)
                {
                    foreach (var scanResult in SqlUdpScanner.Scan(item.ComputerName, udpTimeOut, true))
                    {
                        yield return scanResult;
                    }
                }

                yield break;
            }

            foreach (var item in table)
            {
                yield return item;
            }
        }
    }

    public sealed class GetSqlInstanceDomainCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLInstanceDomain"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            WriteVerbose(context, "Grabbing SPNs from the domain for SQL Servers (MSSQL*)...");
            WriteVerbose(context, "Parsing SQL Server instances from SPNs...");

            var includeIp = GetSwitch(context, "IncludeIP");
            var checkMgmt = GetSwitch(context, "CheckMgmt");
            var udpTimeOut = GetIntArg(context, "UDPTimeOut", 3);

            var spnResults = GetDomainSpn.Execute(
                "MSSQL*",
                GetArg(context, "DomainController"),
                GetArg(context, "Username"),
                GetArg(context, "Password"),
                GetArg(context, "ComputerName"),
                GetArg(context, "DomainAccount"),
                true,
                msg => WriteVerbose(context, msg))
                .Where(s => s.Service != null && s.Service.StartsWith("MSSQL", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var domainInstances = spnResults
                .Select(spn => MapSpnToInstance(spn, includeIp))
                .ToList();

            if (checkMgmt)
            {
                WriteVerbose(context, "Grabbing SPNs from the domain for Servers managing SQL Server clusters (MSServerClusterMgmtAPI)...");
                WriteVerbose(context, "Performing a UDP scan of management servers to obtain managed SQL Server instances...");

                var mgmtServers = GetDomainSpn.Execute(
                        "MSServerClusterMgmtAPI",
                        GetArg(context, "DomainController"),
                        GetArg(context, "Username"),
                        GetArg(context, "Password"),
                        GetArg(context, "ComputerName"),
                        GetArg(context, "DomainAccount"),
                        true)
                    .Where(s => s.ComputerName != null && s.ComputerName.Contains("."))
                    .Select(s => s.ComputerName)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                var udpResults = new List<SqlInstanceDomainResult>();
                foreach (var server in mgmtServers)
                {
                    foreach (var scan in SqlUdpScanner.Scan(server, udpTimeOut, true))
                    {
                        udpResults.Add(new SqlInstanceDomainResult
                        {
                            ComputerName = scan.ComputerName,
                            Instance = scan.Instance
                        });
                    }
                }

                WriteVerbose(context, "Parsing SQL Server instances from the UDP scan...");
                var merged = udpResults
                    .Select(r => new { r.ComputerName, r.Instance })
                    .Concat(domainInstances.Select(r => new { r.ComputerName, r.Instance }))
                    .Distinct()
                    .OrderBy(r => r.ComputerName)
                    .ThenBy(r => r.Instance)
                    .ToList();

                WriteVerbose(context, merged.Count + " instances were found.");
                foreach (var item in merged)
                {
                    yield return new SqlInstanceDomainResult
                    {
                        ComputerName = item.ComputerName,
                        Instance = item.Instance
                    };
                }

                yield break;
            }

            WriteVerbose(context, domainInstances.Count + " instances were found.");
            foreach (var item in domainInstances)
            {
                yield return item;
            }
        }

        private static SqlInstanceDomainResult MapSpnToInstance(DomainSpnResult spn, bool includeIp)
        {
            var spnServerInstance = spn.Spn;
            if (spnServerInstance.StartsWith("MSSQLSvc/", StringComparison.OrdinalIgnoreCase))
            {
                var instancePart = string.Empty;
                var slashParts = spn.Spn.Split('/');
                if (slashParts.Length > 1)
                {
                    var hostPart = slashParts[1];
                    var colonParts = hostPart.Split(':');
                    instancePart = colonParts.Length > 1 ? colonParts[1] : colonParts[0];
                }

                int portNumber;
                if (int.TryParse(instancePart, out portNumber))
                {
                    spnServerInstance = spn.Spn.Replace(":", ",");
                }
                else
                {
                    spnServerInstance = spn.Spn.Replace(":", "\\");
                }

                spnServerInstance = spnServerInstance.Replace("MSSQLSvc/", string.Empty);
            }

            var result = new SqlInstanceDomainResult
            {
                ComputerName = spn.ComputerName,
                Instance = spnServerInstance,
                DomainAccountSid = spn.UserSid,
                DomainAccount = spn.User,
                DomainAccountCn = spn.UserCn,
                Service = spn.Service,
                Spn = spn.Spn,
                LastLogon = spn.LastLogon,
                Description = spn.Description
            };

            if (includeIp)
            {
                try
                {
                    var addresses = System.Net.Dns.GetHostAddresses(spn.ComputerName);
                    result.IPAddress = addresses.Length > 1
                        ? string.Join(", ", addresses.Select(a => a.ToString()))
                        : addresses[0].ToString();
                }
                catch
                {
                    result.IPAddress = "0.0.0.0";
                }
            }

            return result;
        }
    }
}
