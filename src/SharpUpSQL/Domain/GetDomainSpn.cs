using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace SharpUpSQL.Domain
{
    public sealed class DomainSpnResult
    {
        public string UserSid { get; set; }
        public string User { get; set; }
        public string UserCn { get; set; }
        public string Service { get; set; }
        public string ComputerName { get; set; }
        public string Spn { get; set; }
        public string LastLogon { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// PowerUpSQL Get-DomainSpn — query domain SPN registrations via LDAP.
    /// </summary>
    public static class GetDomainSpn
    {
        public static IEnumerable<DomainSpnResult> Execute(
            string spnService = null,
            string domainController = null,
            string username = null,
            string password = null,
            string computerName = null,
            string domainAccount = null,
            bool suppressVerbose = false,
            Action<string> verbose = null)
        {
            if (!suppressVerbose && verbose != null)
            {
                verbose("Getting domain SPNs...");
            }

            var spnFilter = string.Empty;
            if (!string.IsNullOrEmpty(domainAccount))
            {
                spnFilter = "(objectcategory=person)(SamAccountName=" + domainAccount + ")";
            }
            else if (!string.IsNullOrEmpty(computerName))
            {
                spnFilter = "(objectcategory=computer)(SamAccountName=" + computerName + "$)";
            }

            var servicePattern = string.IsNullOrEmpty(spnService) ? "*" : spnService;
            var ldapFilter = "(&(servicePrincipalName=" + servicePattern + ")" + spnFilter + ")";

            var results = new List<DomainSpnResult>();
            foreach (var domainObject in GetDomainObject.Execute(
                         ldapFilter,
                         domainController,
                         username,
                         password))
            {
                if (!domainObject.Properties.ContainsKey("serviceprincipalname"))
                {
                    continue;
                }

                var spnValues = domainObject.Properties["serviceprincipalname"];
                IEnumerable<object> spnList;
                var array = spnValues as object[];
                if (array != null)
                {
                    spnList = array;
                }
                else
                {
                    spnList = new[] { spnValues };
                }

                var sidString = ConvertSidToString(domainObject.Properties["objectsid"]);

                var samAccountObj = domainObject.Properties["samaccountname"];
                var samAccount = samAccountObj != null ? samAccountObj.ToString() : string.Empty;

                var cnObj = domainObject.Properties["cn"];
                var cn = cnObj != null ? cnObj.ToString() : string.Empty;

                var descriptionObj = domainObject.Properties["description"];
                var description = descriptionObj != null ? descriptionObj.ToString() : string.Empty;

                var lastLogon = FormatLastLogon(domainObject.Properties["lastlogon"]);

                foreach (var spnObj in spnList)
                {
                    var item = spnObj != null ? spnObj.ToString() : null;
                    if (string.IsNullOrEmpty(item))
                    {
                        continue;
                    }

                    var slashParts = item.Split('/');
                    if (slashParts.Length < 2)
                    {
                        continue;
                    }

                    var spnServer = slashParts[1].Split(':')[0].Split(' ')[0];
                    var spnServiceName = slashParts[0];

                    results.Add(new DomainSpnResult
                    {
                        UserSid = sidString,
                        User = samAccount,
                        UserCn = cn,
                        Service = spnServiceName,
                        ComputerName = spnServer,
                        Spn = item,
                        LastLogon = lastLogon,
                        Description = description
                    });
                }
            }

            if (results.Count > 0)
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose(results.Count + " SPNs found on servers that matched search criteria.");
                }
            }
            else if (!suppressVerbose && verbose != null)
            {
                verbose("0 SPNs found.");
            }

            return results;
        }

        private static string ConvertSidToString(object sidObject)
        {
            if (sidObject == null)
            {
                return string.Empty;
            }

            try
            {
                var sidBytes = sidObject as byte[];
                if (sidBytes != null)
                {
                    return new SecurityIdentifier(sidBytes, 0).Value;
                }

                var sidString = sidObject as string;
                if (sidString != null && sidString.Contains("-"))
                {
                    return sidString;
                }

                var text = sidObject.ToString();
                if (text.Contains(" "))
                {
                    var parts = text.Split(' ');
                    var bytes = new byte[parts.Length];
                    for (var i = 0; i < parts.Length; i++)
                    {
                        bytes[i] = byte.Parse(parts[i]);
                    }

                    return new SecurityIdentifier(bytes, 0).Value;
                }
            }
            catch
            {
                return sidObject.ToString();
            }

            return sidObject.ToString();
        }

        private static string FormatLastLogon(object lastLogonValue)
        {
            if (lastLogonValue == null)
            {
                return string.Empty;
            }

            try
            {
                long fileTime;
                if (lastLogonValue is long)
                {
                    fileTime = (long)lastLogonValue;
                }
                else
                {
                    fileTime = Convert.ToInt64(lastLogonValue);
                }

                return DateTime.FromFileTime(fileTime).ToString("g");
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
