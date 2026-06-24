namespace SharpUpSQL.Audit
{
    /// <summary>
    /// PowerUpSQL Invoke-SQLAudit* standard output row.
    /// </summary>
    public sealed class SqlAuditResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string Vulnerability { get; set; }
        public string Description { get; set; }
        public string Remediation { get; set; }
        public string Severity { get; set; }
        public string IsVulnerable { get; set; }
        public string IsExploitable { get; set; }
        public string Exploited { get; set; }
        public string ExploitCmd { get; set; }
        public string Details { get; set; }
        public string Reference { get; set; }
        public string Author { get; set; }

        public static SqlAuditResult Create(
            string computerName,
            string instance,
            string vulnerability,
            string description,
            string remediation,
            string severity,
            string isVulnerable,
            string isExploitable,
            string exploited,
            string exploitCmd,
            string details,
            string reference,
            string author)
        {
            return new SqlAuditResult
            {
                ComputerName = computerName ?? string.Empty,
                Instance = instance ?? string.Empty,
                Vulnerability = vulnerability ?? string.Empty,
                Description = description ?? string.Empty,
                Remediation = remediation ?? string.Empty,
                Severity = severity ?? string.Empty,
                IsVulnerable = isVulnerable ?? "No",
                IsExploitable = isExploitable ?? "No",
                Exploited = exploited ?? "No",
                ExploitCmd = exploitCmd ?? string.Empty,
                Details = details ?? string.Empty,
                Reference = reference ?? string.Empty,
                Author = author ?? string.Empty
            };
        }
    }
}
