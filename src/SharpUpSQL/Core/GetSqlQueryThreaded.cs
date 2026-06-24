using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Threading;

namespace SharpUpSQL.Core
{
    public sealed class GetSqlQueryThreadedCommand : SqlInstanceCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLQueryThreaded"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var query = GetArg(context, "Query");
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new System.ArgumentException("Query parameter is required.");
            }

            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var threads = GetIntArg(context, "Threads", 5);
            var targets = ResolveTargets(context).ToList();

            var results = ThreadPoolRunner.RunParallelMany(
                targets,
                target =>
                {
                    var options = BuildConnectionOptions(context);
                    options.Instance = ResolveInstance(target, context) ?? options.Instance;
                    return SqlEnumerationEngine.GetSqlQuery(
                        options,
                        query,
                        options.Instance,
                        context.Verbose,
                        suppressVerbose).Cast<object>();
                },
                threads);

            foreach (var result in results)
            {
                yield return result;
            }
        }
    }
}
