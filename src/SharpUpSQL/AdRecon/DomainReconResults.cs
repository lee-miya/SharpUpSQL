using System;
using SharpUpSQL.Common;

namespace SharpUpSQL.AdRecon
{
    public sealed class SqlDomainLapsResult : SqlInstanceResult
    {
        public string Hostname { get; set; }
        public string Password { get; set; }
    }

    public sealed class SqlDomainTrustResult : SqlInstanceResult
    {
        public string Trustpartner { get; set; }
        public string Distinguishedname { get; set; }
        public string Trusttype { get; set; }
        public string Trustdirection { get; set; }
        public string Trustattributes { get; set; }
        public string Whencreated { get; set; }
        public string Whenchanged { get; set; }
        public string Objectclass { get; set; }
    }

    public sealed class SqlDomainAccountPolicyResult : SqlInstanceResult
    {
        public string pwdhistorylength { get; set; }
        public string lockoutthreshold { get; set; }
        public string lockoutduration { get; set; }
        public string lockoutobservationwindow { get; set; }
        public string minpwdlength { get; set; }
        public string minpwdage { get; set; }
        public string pwdproperties { get; set; }
        public string whenchanged { get; set; }
        public string gplink { get; set; }
    }

    public sealed class SqlDomainGroupMemberResult : SqlInstanceResult
    {
        public string Group { get; set; }
        public string sAMAccountName { get; set; }
        public string displayName { get; set; }
    }

    public sealed class SqlDomainExploitableSystemResult
    {
        public string ComputerName { get; set; }
        public string OperatingSystem { get; set; }
        public string ServicePack { get; set; }
        public string LastLogon { get; set; }
        public string MsfModule { get; set; }
        public string CVE { get; set; }
    }
}
