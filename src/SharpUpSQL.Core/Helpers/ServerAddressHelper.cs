using System;

namespace SharpUpSQL.Core.Helpers
{
    public static class ServerAddressHelper
    {
        public static string FormatServer(string instance, int? port, bool forceNamedPipe)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                instance = Environment.MachineName;
            }

            if (forceNamedPipe)
            {
                return FormatNamedPipe(instance);
            }

            if (port.HasValue && port.Value > 0 && !InstanceHasPort(instance))
            {
                return instance.Trim() + "," + port.Value;
            }

            return instance.Trim();
        }

        public static bool InstanceHasPort(string instance)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                return false;
            }

            var slash = instance.IndexOf('\\');
            var hostPart = slash >= 0 ? instance.Substring(0, slash) : instance;
            return hostPart.IndexOf(',') >= 0;
        }

        public static void ParseHostPort(string instance, out string host, out int port)
        {
            host = Environment.MachineName;
            port = 1433;

            if (string.IsNullOrWhiteSpace(instance))
            {
                return;
            }

            var working = instance.Trim();
            var slash = working.IndexOf('\\');
            if (slash >= 0)
            {
                working = working.Substring(0, slash);
            }

            var comma = working.IndexOf(',');
            if (comma >= 0)
            {
                host = working.Substring(0, comma);
                int parsed;
                if (int.TryParse(working.Substring(comma + 1), out parsed) && parsed > 0)
                {
                    port = parsed;
                }
            }
            else
            {
                host = working;
            }
        }

        private static string FormatNamedPipe(string instance)
        {
            var trimmed = instance.Trim();
            var slash = trimmed.IndexOf('\\');
            if (slash < 0)
            {
                return "np:\\\\" + trimmed + "\\pipe\\sql\\query";
            }

            var host = trimmed.Substring(0, slash);
            var inst = trimmed.Substring(slash + 1);
            if (string.Equals(inst, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
            {
                return "np:\\\\" + host + "\\pipe\\sql\\query";
            }

            return "np:\\\\" + host + "\\pipe\\MSSQL$" + inst + "\\sql\\query";
        }
    }
}
