using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Output;
using SharpUpSQL.Core.Threading;

namespace SharpUpSQL.Core
{
    public sealed class GetSqlConnectionTestThreadedCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLConnectionTestThreaded"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var options = context.BuildConnectionOptions();
            var ipRange = GetArg(context, "IPRange");
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var threads = GetIntArg(context, "Threads", 5);
            var targets = ResolveTargets(context).ToList();

            var results = ThreadPoolRunner.RunParallel(
                targets,
                target => ConnectionTester.Test(
                    target.Instance,
                    target.IPAddress,
                    ipRange,
                    options,
                    context.Verbose,
                    suppressVerbose),
                threads);

            foreach (var result in results)
            {
                yield return result;
            }
        }

        private static IEnumerable<PipelineObject> ResolveTargets(SharpUpSqlContext context)
        {
            var targets = new List<PipelineObject>(context.Pipeline);

            string instance;
            string ip;
            var hasInstance = context.Arguments.TryGetValue("Instance", out instance);
            var hasIp = context.Arguments.TryGetValue("IPAddress", out ip);

            if (hasInstance || hasIp)
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
    }
}
