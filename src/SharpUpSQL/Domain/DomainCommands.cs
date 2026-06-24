using System.Collections.Generic;
using SharpUpSQL.Commands;

namespace SharpUpSQL.Domain
{
    public sealed class GetDomainObjectCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-DomainObject"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var filter = GetArg(context, "LDAPFilter");
            if (string.IsNullOrWhiteSpace(filter))
            {
                filter = GetArg(context, "Filter");
            }

            var limit = GetIntArg(context, "Limit", 1000);
            foreach (var result in GetDomainObject.Execute(
                         filter,
                         GetArg(context, "DomainController"),
                         GetArg(context, "Username"),
                         GetArg(context, "Password"),
                         limit,
                         GetArg(context, "LDAPPath")))
            {
                yield return result;
            }
        }
    }

    public sealed class GetDomainSpnCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-DomainSpn"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            foreach (var result in GetDomainSpn.Execute(
                         GetArg(context, "SpnService"),
                         GetArg(context, "DomainController"),
                         GetArg(context, "Username"),
                         GetArg(context, "Password"),
                         GetArg(context, "ComputerName"),
                         GetArg(context, "DomainAccount"),
                         suppressVerbose,
                         message => WriteVerbose(context, message)))
            {
                yield return result;
            }
        }
    }
}
