using System.Collections.Generic;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Commands
{
    public sealed class SharpUpSqlContext
    {
        public Dictionary<string, string> Arguments { get; private set; }
        public HashSet<string> Switches { get; private set; }
        public List<PipelineObject> Pipeline { get; private set; }
        public VerboseWriter Verbose { get; private set; }
        public string OutputFormat { get; set; }
        public bool DebugSql { get; set; }

        public SharpUpSqlContext()
        {
            Arguments = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            Switches = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            Pipeline = new List<PipelineObject>();
            Verbose = new VerboseWriter();
        }

        public SqlConnectionOptions BuildConnectionOptions()
        {
            var options = new SqlConnectionOptions
            {
                Instance = Get("Instance"),
                Username = Get("Username"),
                Password = Get("Password"),
                Hash = Get("Hash"),
                Domain = Get("Domain") ?? string.Empty,
                Database = Get("Database") ?? "Master",
                Dac = Switches.Contains("DAC"),
                AppName = Get("AppName") ?? string.Empty,
                WorkstationId = Get("WorkstationId") ?? string.Empty,
                Encrypt = Get("Encrypt") ?? string.Empty,
                TrustServerCert = Get("TrustServerCert") ?? string.Empty,
                ForceNamedPipe = Switches.Contains("ForceNamedPipe"),
                DebugSql = DebugSql || Switches.Contains("Debug")
            };

            int timeout;
            if (int.TryParse(Get("TimeOut"), out timeout))
            {
                options.TimeOut = timeout;
            }

            int port;
            if (int.TryParse(Get("Port"), out port) && port > 0)
            {
                options.Port = port;
            }

            int packetSize;
            if (int.TryParse(Get("PacketSize"), out packetSize) && packetSize > 0)
            {
                options.PacketSize = packetSize;
            }

            return options;
        }

        private string Get(string key)
        {
            string value;
            Arguments.TryGetValue(key, out value);
            return value;
        }
    }
}
