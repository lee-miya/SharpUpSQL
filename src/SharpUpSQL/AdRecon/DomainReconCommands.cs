using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;

namespace SharpUpSQL.AdRecon
{
    internal static class DomainReconCommandHelper
    {
        internal static DomainReconRequest BuildRequest(
            SharpUpSqlContext context,
            string instance,
            SqlConnectionOptions options,
            bool suppressVerbose,
            string ldapPath = null,
            string ldapFilter = null,
            string ldapFields = null)
        {
            return new DomainReconRequest
            {
                Options = options,
                Instance = instance,
                LinkUsername = GetArg(context, "LinkUsername"),
                LinkPassword = GetArg(context, "LinkPassword"),
                UseAdHoc = context.Switches.Contains("UseAdHoc"),
                TargetDomain = GetArg(context, "TargetDomain"),
                LdapPath = ldapPath,
                LdapFilter = ldapFilter,
                LdapFields = ldapFields,
                Verbose = context.Verbose,
                SuppressVerbose = suppressVerbose
            };
        }

        internal static IEnumerable<object> ForEachTarget(
            SharpUpSqlContext context,
            Func<DomainReconRequest, IEnumerable<object>> action)
        {
            var suppressVerbose = context.Switches.Contains("SuppressVerbose");
            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var options = context.BuildConnectionOptions();
                var instance = !string.IsNullOrWhiteSpace(target.Instance)
                    ? target.Instance
                    : options.Instance;

                if (!string.IsNullOrWhiteSpace(instance))
                {
                    options.Instance = instance;
                }

                foreach (var item in action(BuildRequest(context, instance, options, suppressVerbose)))
                {
                    yield return item;
                }
            }
        }

        private static string GetArg(SharpUpSqlContext context, string name)
        {
            string value;
            return context.Arguments.TryGetValue(name, out value) ? value : null;
        }
    }

    public sealed class GetSqlDomainObjectCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainObject"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "LdapPath");
                request.LdapFilter = GetArg(context, "LdapFilter");
                request.LdapFields = GetArg(context, "LdapFields");
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainUserCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainUser"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var filterUser = GetArg(context, "FilterUser") ?? "*";
            var userState = GetArg(context, "UserState") ?? "Enabled";
            var pwLastSet = GetIntArg(context, "PwLastSet", 0);
            var pwLastSetFilter = DomainReconEngine.BuildPwLastSetFilter(pwLastSet);
            var userStateFilter = DomainReconEngine.BuildUserStateFilter(userState);
            var ldapFilter = "(&(objectCategory=Person)(objectClass=user)" + pwLastSetFilter +
                             "(SamAccountName=" + filterUser + ")" + userStateFilter + ")";
            const string ldapFields = "samaccountname,name,admincount,whencreated,whenchanged,adspath";

            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = ldapFilter;
                request.LdapFields = ldapFields;
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainComputerCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainComputer"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var filterComputer = GetArg(context, "FilterComputer") ?? "*";
            var ldapFilter = "(&(objectCategory=Computer)(SamAccountName=" + filterComputer + "))";
            const string ldapFields =
                "samaccountname,dnshostname,operatingsystem,operatingsystemversion,operatingSystemServicePack,whencreated,whenchanged,adspath";

            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = ldapFilter;
                request.LdapFields = ldapFields;
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainGroupCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainGroup"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var filterGroup = GetArg(context, "FilterGroup") ?? "*";
            var ldapFilter = "(&(objectClass=Group)(SamAccountName=" + filterGroup + "))";
            const string ldapFields = "samaccountname,adminCount,whencreated,whenchanged,adspath";

            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = ldapFilter;
                request.LdapFields = ldapFields;
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainOuCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainOu"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            const string ldapFilter = "(objectCategory=organizationalUnit)";
            const string ldapFields = "name,distinguishedname,adspath,instancetype,whencreated,whenchanged";

            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = ldapFilter;
                request.LdapFields = ldapFields;
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainSubnetCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainSubnet"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var domain = GetArg(context, "TargetDomain") ?? baseRequest.TargetDomain;
                var dn = DomainReconEngine.ResolveDomainDistinguishedName(baseRequest, domain);
                if (string.IsNullOrEmpty(dn))
                {
                    return Enumerable.Empty<object>();
                }

                var request = baseRequest;
                request.LdapPath = domain + "/CN=Sites,CN=Configuration," + dn;
                request.LdapFilter = "(objectCategory=subnet)";
                request.LdapFields = "name,distinguishedname,siteobject,whencreated,whenchanged,location";
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainSiteCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainSite"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var domain = GetArg(context, "TargetDomain") ?? baseRequest.TargetDomain;
                var dn = DomainReconEngine.ResolveDomainDistinguishedName(baseRequest, domain);
                if (string.IsNullOrEmpty(dn))
                {
                    return Enumerable.Empty<object>();
                }

                var request = baseRequest;
                request.LdapPath = domain + "/CN=Sites,CN=Configuration," + dn;
                request.LdapFilter = "(objectCategory=site)";
                request.LdapFields = "name,distinguishedname,whencreated,whenchanged";
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainAccountPolicyCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainAccountPolicy"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = "(objectClass=domainDNS)";
                request.LdapFields =
                    "pwdhistorylength,lockoutthreshold,lockoutduration,lockoutobservationwindow,minpwdlength,minpwdage,pwdproperties,whenchanged,gplink";

                return DomainReconEngine.GetSqlDomainObject(request)
                    .Select(row => new SqlDomainAccountPolicyResult
                    {
                        pwdhistorylength = SqlValueFormatter.Format(row["pwdhistorylength"]),
                        lockoutthreshold = SqlValueFormatter.Format(row["lockoutthreshold"]),
                        lockoutduration = DomainReconEngine.FormatAccountPolicyDuration(
                            SqlValueFormatter.Format(row["lockoutduration"])),
                        lockoutobservationwindow = DomainReconEngine.FormatAccountPolicyDuration(
                            SqlValueFormatter.Format(row["lockoutobservationwindow"])),
                        minpwdlength = SqlValueFormatter.Format(row["minpwdlength"]),
                        minpwdage = DomainReconEngine.FormatAccountPolicyAge(SqlValueFormatter.Format(row["minpwdage"])),
                        pwdproperties = SqlValueFormatter.Format(row["pwdproperties"]),
                        whenchanged = SqlValueFormatter.Format(row["whenchanged"]),
                        gplink = SqlValueFormatter.Format(row["gplink"])
                    })
                    .Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainTrustCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainTrust"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = "(objectClass=trustedDomain)";
                request.LdapFields =
                    "trustpartner,distinguishedname,trusttype,trustdirection,trustattributes,whencreated,whenchanged,adspath";

                return DomainReconEngine.GetSqlDomainObject(request)
                    .Select(row => new SqlDomainTrustResult
                    {
                        Trustpartner = SqlValueFormatter.Format(row["trustpartner"]),
                        Distinguishedname = SqlValueFormatter.Format(row["distinguishedname"]),
                        Trusttype = DomainReconEngine.ResolveTrustType(SqlValueFormatter.Format(row["trusttype"])),
                        Trustdirection = DomainReconEngine.ResolveTrustDirection(SqlValueFormatter.Format(row["trustdirection"])),
                        Trustattributes = DomainReconEngine.ResolveTrustAttribute(SqlValueFormatter.Format(row["trustattributes"])),
                        Whencreated = SqlValueFormatter.Format(row["whencreated"]),
                        Whenchanged = SqlValueFormatter.Format(row["whenchanged"]),
                        Objectclass = SqlValueFormatter.Format(row["objectclass"])
                    })
                    .Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainPasswordsLapsCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainPasswordsLAPS"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = "(objectCategory=Computer)";
                request.LdapFields = "dnshostname,ms-MCS-AdmPwd,adspath";

                return DomainReconEngine.GetSqlDomainObject(request)
                    .Select(row =>
                    {
                        var password = SqlValueFormatter.Format(row["ms-MCS-AdmPwd"]);
                        if (string.IsNullOrEmpty(password))
                        {
                            return null;
                        }

                        return new SqlDomainLapsResult
                        {
                            Hostname = SqlValueFormatter.Format(row["dnshostname"]),
                            Password = password
                        };
                    })
                    .Where(item => item != null)
                    .Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainControllerCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainController"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            const string ldapFilter = "(&(objectCategory=computer)(userAccountControl:1.2.840.113556.1.4.803:=8192))";
            const string ldapFields =
                "name,dnshostname,operatingsystem,operatingsystemversion,operatingsystemservicepack,whenchanged,logoncount";

            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = ldapFilter;
                request.LdapFields = ldapFields;
                return DomainReconEngine.GetSqlDomainObject(request).Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainExploitableSystemCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainExploitableSystem"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = "(objectCategory=Computer)";
                request.LdapFields =
                    "dnshostname,operatingsystem,operatingsystemversion,operatingsystemservicepack,whenchanged,logoncount";

                var computers = DomainReconEngine.GetSqlDomainObject(request).ToList();
                var results = new List<SqlDomainExploitableSystemResult>();

                foreach (var exploit in ExploitCatalog.Entries)
                {
                    foreach (var computer in computers)
                    {
                        var adsOs = SqlValueFormatter.Format(computer["operatingsystem"]);
                        var adsSp = SqlValueFormatter.Format(computer["operatingsystemservicepack"]);
                        if (!adsOs.StartsWith(exploit.OperatingSystem, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!string.Equals(adsSp ?? string.Empty, exploit.ServicePack ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var lastLogonRaw = SqlValueFormatter.Format(computer["logoncount"]);
                        string lastLogon = lastLogonRaw;
                        long fileTime;
                        if (long.TryParse(lastLogonRaw, out fileTime))
                        {
                            lastLogon = DateTime.FromFileTime(fileTime).ToString(CultureInfo.InvariantCulture);
                        }

                        results.Add(new SqlDomainExploitableSystemResult
                        {
                            ComputerName = SqlValueFormatter.Format(computer["dnshostname"]),
                            OperatingSystem = adsOs,
                            ServicePack = adsSp,
                            LastLogon = lastLogon,
                            MsfModule = exploit.MsfModule,
                            CVE = exploit.Cve
                        });
                    }
                }

                return results
                    .OrderByDescending(r =>
                    {
                        DateTime parsed;
                        return DateTime.TryParse(r.LastLogon, out parsed) ? parsed : DateTime.MinValue;
                    })
                    .Cast<object>();
            });
        }
    }

    public sealed class GetSqlDomainGroupMemberCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLDomainGroupMember"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var filterGroup = GetArg(context, "FilterGroup") ?? "Domain Admins";

            return DomainReconCommandHelper.ForEachTarget(context, baseRequest =>
            {
                var lookup = baseRequest;
                lookup.LdapPath = GetArg(context, "TargetDomain");
                lookup.LdapFilter = "(&(objectCategory=group)(samaccountname=" + filterGroup + "))";
                lookup.LdapFields = "distinguishedname";
                lookup.SuppressVerbose = true;

                var dnRow = DomainReconEngine.GetSqlDomainObject(lookup).FirstOrDefault();
                if (dnRow == null)
                {
                    return Enumerable.Empty<object>();
                }

                var dn = SqlValueFormatter.Format(dnRow["distinguishedname"]);
                if (string.IsNullOrEmpty(dn))
                {
                    return Enumerable.Empty<object>();
                }

                var request = baseRequest;
                request.LdapPath = GetArg(context, "TargetDomain");
                request.LdapFilter = "(&(objectCategory=user)(memberOf=" + dn + "))";
                request.LdapFields = "samaccountname,displayname";

                return DomainReconEngine.GetSqlDomainObject(request)
                    .Select(row => new SqlDomainGroupMemberResult
                    {
                        Group = filterGroup,
                        sAMAccountName = SqlValueFormatter.Format(row["samaccountname"]),
                        displayName = SqlValueFormatter.Format(row["displayname"])
                    })
                    .Cast<object>();
            });
        }
    }
}
