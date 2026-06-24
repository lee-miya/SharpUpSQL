using System.Collections.Generic;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Commands
{
    public interface ISharpUpSqlCommand
    {
        string Name { get; }

        IEnumerable<object> Execute(SharpUpSqlContext context);
    }

    public abstract class SharpUpSqlCommandBase : ISharpUpSqlCommand
    {
        public abstract string Name { get; }

        public abstract IEnumerable<object> Execute(SharpUpSqlContext context);

        protected string GetArg(SharpUpSqlContext context, string name, string defaultValue = null)
        {
            string value;
            if (context.Arguments.TryGetValue(name, out value))
            {
                return value;
            }

            return defaultValue;
        }

        protected bool GetSwitch(SharpUpSqlContext context, string name)
        {
            return context.Switches.Contains(name);
        }

        protected int GetIntArg(SharpUpSqlContext context, string name, int defaultValue)
        {
            string value;
            int parsed;
            if (context.Arguments.TryGetValue(name, out value) &&
                int.TryParse(value, out parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        protected void WriteVerbose(SharpUpSqlContext context, string message)
        {
            context.Verbose.Write(message);
        }
    }
}
