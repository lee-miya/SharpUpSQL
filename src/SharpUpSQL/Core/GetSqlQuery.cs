using System.Collections.Generic;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;

namespace SharpUpSQL.Core
{
    public sealed class GetSqlQueryCommand : SqlInstanceCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLQuery"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var query = GetArg(context, "Query");
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new System.ArgumentException("Query parameter is required.");
            }

            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            foreach (var target in ResolveTargets(context))
            {
                var options = BuildConnectionOptions(context);
                options.Instance = ResolveInstance(target, context) ?? options.Instance;

                foreach (var row in SqlEnumerationEngine.GetSqlQuery(
                             options,
                             query,
                             options.Instance,
                             context.Verbose,
                             suppressVerbose))
                {
                    yield return row;
                }
            }
        }
    }
}
