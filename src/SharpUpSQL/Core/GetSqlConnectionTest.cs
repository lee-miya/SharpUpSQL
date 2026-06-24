using System.Collections.Generic;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Core
{
    public sealed class GetSqlConnectionTestCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLConnectionTest"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var options = context.BuildConnectionOptions();
            var ipRange = GetArg(context, "IPRange");
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var targets = ResolveTargets(context);

            foreach (var target in targets)
            {
                yield return ConnectionTester.Test(
                    target.Instance,
                    target.IPAddress,
                    ipRange,
                    options,
                    context.Verbose,
                    suppressVerbose);
            }
        }

        private static IEnumerable<PipelineObject> ResolveTargets(SharpUpSqlContext context)
        {
            if (context.Pipeline.Count > 0)
            {
                return context.Pipeline;
            }

            string instance;
            context.Arguments.TryGetValue("Instance", out instance);
            string ip;
            context.Arguments.TryGetValue("IPAddress", out ip);

            return new[]
            {
                new PipelineObject
                {
                    Instance = instance,
                    IPAddress = ip
                }
            };
        }
    }
}
