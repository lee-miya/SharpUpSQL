using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;

namespace SharpUpSQL.Domain
{
    public sealed class DomainObjectResult
    {
        public Hashtable Properties { get; private set; }

        public DomainObjectResult()
        {
            Properties = new Hashtable(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// PowerUpSQL Get-DomainObject — LDAP query against Active Directory.
    /// </summary>
    public static class GetDomainObject
    {
        public static IEnumerable<DomainObjectResult> Execute(
            string ldapFilter,
            string domainController = null,
            string username = null,
            string password = null,
            int limit = 1000,
            string ldapPath = null)
        {
            if (string.IsNullOrEmpty(ldapFilter))
            {
                yield break;
            }

            DirectorySearcher searcher;
            if (!string.IsNullOrEmpty(domainController))
            {
                DirectoryEntry domainEntry;
                try
                {
                    domainEntry = !string.IsNullOrEmpty(username)
                        ? new DirectoryEntry("LDAP://" + domainController, username, password)
                        : new DirectoryEntry("LDAP://" + domainController);

                    var distinguishedName = domainEntry.Properties["distinguishedName"];
                    var domainName = distinguishedName != null && distinguishedName.Value != null
                        ? distinguishedName.Value.ToString()
                        : null;

                    if (string.IsNullOrEmpty(domainName))
                    {
                        Console.Error.WriteLine("Authentication failed or domain controller is not reachable.");
                        yield break;
                    }

                    var ldapRoot = "LDAP://" + domainController;
                    if (!string.IsNullOrEmpty(ldapPath))
                    {
                        ldapRoot = ldapRoot + "/" + ldapPath + "," + domainName;
                    }

                    domainEntry = !string.IsNullOrEmpty(username)
                        ? new DirectoryEntry(ldapRoot, username, password)
                        : new DirectoryEntry(ldapRoot);
                }
                catch
                {
                    Console.Error.WriteLine("Authentication failed or domain controller is not reachable.");
                    yield break;
                }

                searcher = new DirectorySearcher(domainEntry);
            }
            else
            {
                DirectoryEntry rootEntry;
                if (!string.IsNullOrEmpty(ldapPath))
                {
                    var root = new DirectoryEntry();
                    var distinguishedName = root.Properties["distinguishedName"];
                    var domainName = distinguishedName != null && distinguishedName.Value != null
                        ? distinguishedName.Value.ToString()
                        : string.Empty;
                    rootEntry = new DirectoryEntry("LDAP://" + ldapPath + "," + domainName);
                }
                else
                {
                    rootEntry = new DirectoryEntry();
                }

                searcher = new DirectorySearcher(rootEntry);
            }

            using (searcher)
            {
                searcher.PageSize = limit;
                searcher.Filter = ldapFilter;
                searcher.SearchScope = SearchScope.Subtree;

                SearchResultCollection results = null;
                try
                {
                    results = searcher.FindAll();
                    foreach (SearchResult result in results)
                    {
                        var domainObject = new DomainObjectResult();
                        foreach (string propertyName in result.Properties.PropertyNames)
                        {
                            var values = result.Properties[propertyName];
                            if (values == null || values.Count == 0)
                            {
                                continue;
                            }

                            if (values.Count == 1)
                            {
                                domainObject.Properties[propertyName] = values[0];
                            }
                            else
                            {
                                var array = new object[values.Count];
                                values.CopyTo(array, 0);
                                domainObject.Properties[propertyName] = array;
                            }
                        }

                        yield return domainObject;
                    }
                }
                finally
                {
                    if (results != null)
                    {
                        results.Dispose();
                    }
                }
            }
        }
    }
}
