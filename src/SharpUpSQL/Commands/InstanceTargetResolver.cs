using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Commands
{
    public static class InstanceTargetResolver
    {
        public static IEnumerable<PipelineObject> Resolve(SharpUpSqlContext context)
        {
            var targets = new List<PipelineObject>(context.Pipeline);

            string instance;
            string ip;
            var hasInstance = context.Arguments.TryGetValue("Instance", out instance);
            var hasIp = context.Arguments.TryGetValue("IPAddress", out ip);

            if (hasInstance && !string.IsNullOrWhiteSpace(instance) && instance.Contains(","))
            {
                var parts = instance
                    .Split(',')
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToList();

                if (parts.Count >= 2 && IsPortOnly(parts[parts.Count - 1]))
                {
                    var port = parts[parts.Count - 1];
                    var hosts = parts.Take(parts.Count - 1).ToList();
                    if (hosts.Count == 1)
                    {
                        targets.Add(new PipelineObject
                        {
                            Instance = hosts[0] + "," + port,
                            IPAddress = ip
                        });
                    }
                    else
                    {
                        foreach (var host in hosts)
                        {
                            targets.Add(new PipelineObject
                            {
                                Instance = host + "," + port,
                                IPAddress = ip
                            });
                        }
                    }
                }
                else
                {
                    foreach (var host in parts)
                    {
                        targets.Add(new PipelineObject
                        {
                            Instance = host,
                            IPAddress = ip
                        });
                    }
                }
            }
            else if (hasInstance || hasIp)
            {
                targets.Add(new PipelineObject
                {
                    Instance = instance,
                    IPAddress = ip
                });
            }

            if (targets.Count == 0)
            {
                targets.Add(new PipelineObject());
            }

            return targets;
        }

        private static bool IsPortOnly(string value)
        {
            int port;
            return int.TryParse(value, out port) && port > 0 && port <= 65535;
        }
    }
}
