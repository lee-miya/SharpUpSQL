using System.Collections.Generic;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Commands
{
    public abstract class SqlInstanceCommandBase : SharpUpSqlCommandBase
    {
        protected IEnumerable<PipelineObject> ResolveTargets(SharpUpSqlContext context)
        {
            return InstanceTargetResolver.Resolve(context);
        }

        protected SqlConnectionOptions BuildConnectionOptions(
            SharpUpSqlContext context,
            string databaseOverride = null)
        {
            var options = context.BuildConnectionOptions();
            if (!string.IsNullOrEmpty(databaseOverride))
            {
                options.Database = databaseOverride;
            }

            return options;
        }

        protected string ResolveInstance(PipelineObject target, SharpUpSqlContext context)
        {
            if (!string.IsNullOrWhiteSpace(target.Instance))
            {
                return target.Instance;
            }

            string instance;
            if (context.Arguments.TryGetValue("Instance", out instance) &&
                !string.IsNullOrWhiteSpace(instance))
            {
                return instance;
            }

            return null;
        }
    }
}
