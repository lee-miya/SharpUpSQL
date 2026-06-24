namespace SharpUpSQL.Attack
{
    public sealed class SqlOsCommandResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string CommandResults { get; set; }
    }

    public sealed class SqlAgentJobOsResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string Results { get; set; }
    }

    public sealed class SqlAssemblyFileResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
        public string DatabaseName { get; set; }
        public string AssemblyId { get; set; }
        public string AssemblyName { get; set; }
        public string FileId { get; set; }
        public string FileName { get; set; }
        public string ClrName { get; set; }
        public string Content { get; set; }
        public string PermissionSetDesc { get; set; }
        public string CreateDate { get; set; }
        public string ModifyDate { get; set; }
        public string IsUserDefined { get; set; }
    }

    public sealed class CreateSqlFileResult
    {
        public string OutFile { get; set; }
        public string Message { get; set; }
    }

    public sealed class SqlImpersonateServiceCmdResult
    {
        public string Instance { get; set; }
        public string ServiceDisplayName { get; set; }
        public string ServiceAccount { get; set; }
        public string Command { get; set; }
        public string Status { get; set; }
    }
}
