using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Threading;

namespace SharpUpSQL.Discovery
{
    public sealed class SqlUdpScanResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string InstanceName { get; set; }
        public string ServerIP { get; set; }
        public string TCPPort { get; set; }
        public string BaseVersion { get; set; }
        public string IsClustered { get; set; }
    }

    public static class SqlUdpScanner
    {
        public static IEnumerable<SqlUdpScanResult> Scan(
            string computerName,
            int udpTimeOutSeconds = 2,
            bool suppressVerbose = false,
            Action<string> verbose = null)
        {
            if (string.IsNullOrWhiteSpace(computerName))
            {
                yield break;
            }

            if (!suppressVerbose && verbose != null)
            {
                verbose(" - " + computerName + " - UDP Scan Start.");
            }

            List<SqlUdpScanResult> results;
            try
            {
                var ipAddress = Dns.GetHostAddresses(computerName);
                var serverIpString = string.Join(" ", ipAddress.Select(a => a.ToString()));

                using (var udpClient = new UdpClient())
                {
                    udpClient.Client.ReceiveTimeout = udpTimeOutSeconds * 1000;
                    udpClient.Connect(computerName, 0x59a);
                    var packet = new byte[] { 0x03 };
                    udpClient.Client.Blocking = true;
                    udpClient.Send(packet, packet.Length);

                    var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    var bytesReceived = udpClient.Receive(ref remoteEndPoint);
                    var response = Encoding.ASCII.GetString(bytesReceived).Split(';');

                    results = ParseResponse(computerName, serverIpString, response, suppressVerbose, verbose);
                }
            }
            catch
            {
                results = new List<SqlUdpScanResult>();
            }

            if (!suppressVerbose && verbose != null)
            {
                verbose(" - " + computerName + " - UDP Scan Complete.");
            }

            foreach (var result in results)
            {
                yield return result;
            }
        }

        private static List<SqlUdpScanResult> ParseResponse(
            string computerName,
            string serverIp,
            string[] response,
            bool suppressVerbose,
            Action<string> verbose)
        {
            var results = new List<SqlUdpScanResult>();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < response.Length; i++)
            {
                if (!string.IsNullOrEmpty(response[i]))
                {
                    var key = Regex.Replace(response[i].ToLowerInvariant(), @"[\W]", string.Empty);
                    var value = i + 1 < response.Length ? response[i + 1] : string.Empty;
                    values[key] = value;
                }
                else if (!string.IsNullOrEmpty(GetValue(values, "tcp")))
                {
                    var instanceName = GetValue(values, "instancename");
                    var discoveredInstance = computerName + "\\" + instanceName;
                    if (!suppressVerbose && verbose != null)
                    {
                        verbose(computerName + " - Found: " + discoveredInstance);
                    }

                    results.Add(new SqlUdpScanResult
                    {
                        ComputerName = computerName,
                        Instance = discoveredInstance,
                        InstanceName = instanceName,
                        ServerIP = serverIp,
                        TCPPort = GetValue(values, "tcp"),
                        BaseVersion = GetValue(values, "version"),
                        IsClustered = GetValue(values, "isclustered")
                    });

                    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }

            return results;
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : string.Empty;
        }
    }

    public sealed class GetSqlInstanceScanUdpCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLInstanceScanUDP"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var udpTimeOut = GetIntArg(context, "UDPTimeOut", 2);
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var targets = ResolveComputerNamesInternal(context);

            foreach (var computerName in targets)
            {
                foreach (var result in SqlUdpScanner.Scan(
                             computerName,
                             udpTimeOut,
                             suppressVerbose,
                             msg => WriteVerbose(context, msg)))
                {
                    yield return result;
                }
            }
        }

        internal IEnumerable<string> ResolveComputerNamesInternal(SharpUpSqlContext context)
        {
            if (context.Pipeline.Count > 0)
            {
                foreach (var item in context.Pipeline)
                {
                    if (!string.IsNullOrWhiteSpace(item.ComputerName))
                    {
                        yield return item.ComputerName;
                    }
                    else if (!string.IsNullOrWhiteSpace(item.Instance))
                    {
                        yield return SharpUpSQL.Core.Helpers.InstanceParser.GetComputerNameFromInstance(item.Instance);
                    }
                }

                yield break;
            }

            var computerName = GetArg(context, "ComputerName");
            if (!string.IsNullOrWhiteSpace(computerName))
            {
                yield return computerName;
            }
        }
    }

    public sealed class GetSqlInstanceScanUdpThreadedCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLInstanceScanUDPThreaded"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var udpTimeOut = GetIntArg(context, "UDPTimeOut", 2);
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var threads = GetIntArg(context, "Threads", 5);
            var scanCommand = new GetSqlInstanceScanUdpCommand();
            var targets = scanCommand.ResolveComputerNamesInternal(context).Distinct().ToList();

            var results = ThreadPoolRunner.RunParallelMany(
                targets,
                computerName => SqlUdpScanner.Scan(
                    computerName,
                    udpTimeOut,
                    suppressVerbose,
                    msg => WriteVerbose(context, msg)),
                threads);

            foreach (var result in results)
            {
                yield return result;
            }
        }
    }
}
