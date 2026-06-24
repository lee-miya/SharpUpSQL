using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;

namespace SharpUpSQL.Discovery
{
    public sealed class SqlInstanceLocalResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string ServiceDisplayName { get; set; }
        public string ServiceName { get; set; }
        public string ServicePath { get; set; }
        public string ServiceAccount { get; set; }
        public string State { get; set; }
    }

    public sealed class GetSqlInstanceLocalCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLInstanceLocal"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var services = GetSqlServiceLocal.Execute(
                verbose: msg => WriteVerbose(context, msg),
                wmiTimeOutSeconds: GetIntArg(context, "WmiTimeOut", GetSqlServiceLocal.DefaultWmiTimeOutSeconds));

            var results = services
                .Where(s => s.ServicePath != null &&
                            s.ServicePath.IndexOf("sqlservr.exe", System.StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(s => new SqlInstanceLocalResult
                {
                    ComputerName = s.ComputerName,
                    Instance = s.Instance,
                    ServiceDisplayName = s.ServiceDisplayName,
                    ServiceName = s.ServiceName,
                    ServicePath = s.ServicePath,
                    ServiceAccount = s.ServiceAccount,
                    State = s.ServiceState
                })
                .ToList();

            WriteVerbose(context, results.Count + " local instances where found.");
            return results;
        }
    }
}
