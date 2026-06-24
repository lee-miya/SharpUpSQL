using System.Collections.Generic;

namespace SharpUpSQL.Core.Output
{
    public sealed class PipelineObject
    {
        public string Instance { get; set; }
        public string ComputerName { get; set; }
        public string IPAddress { get; set; }

        public static PipelineObject FromDictionary(Dictionary<string, string> properties)
        {
            var obj = new PipelineObject();
            if (properties == null)
            {
                return obj;
            }

            string instance;
            if (properties.TryGetValue("Instance", out instance))
            {
                obj.Instance = instance;
            }

            string computerName;
            if (properties.TryGetValue("ComputerName", out computerName))
            {
                obj.ComputerName = computerName;
            }

            string ipAddress;
            if (properties.TryGetValue("IPAddress", out ipAddress))
            {
                obj.IPAddress = ipAddress;
            }

            return obj;
        }
    }
}
