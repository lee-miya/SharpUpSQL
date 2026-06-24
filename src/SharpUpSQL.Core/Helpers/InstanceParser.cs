using System;

namespace SharpUpSQL.Core.Helpers
{
    public static class InstanceParser
    {
        /// <summary>
        /// Parses computer name from instance string (PowerUpSQL Get-ComputerNameFromInstance).
        /// </summary>
        public static string GetComputerNameFromInstance(string instance)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                return Environment.MachineName;
            }

            var slashParts = instance.Split('\\');
            var commaParts = slashParts[0].Split(',');
            return commaParts[0];
        }
    }
}
