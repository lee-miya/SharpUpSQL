using System;

namespace SharpUpSQL.Core.Helpers
{
    public static class InstanceHelper
    {
        public static string GetComputerName(string instance)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                return Environment.MachineName;
            }

            var backslash = instance.IndexOf('\\');
            if (backslash > 0)
            {
                return instance.Substring(0, backslash);
            }

            var comma = instance.IndexOf(',');
            if (comma > 0)
            {
                return instance.Substring(0, comma);
            }

            return instance;
        }

        public static string SanitizeFileName(string instance)
        {
            if (string.IsNullOrEmpty(instance))
            {
                return "unknown";
            }

            return instance
                .Replace("\\", "_")
                .Replace(",", "_")
                .Replace(":", "_")
                .Replace("/", "_");
        }
    }
}
