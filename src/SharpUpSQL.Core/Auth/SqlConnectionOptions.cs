namespace SharpUpSQL.Core.Auth
{
    public sealed class SqlConnectionOptions
    {
        public SqlConnectionOptions()
        {
            Database = "Master";
            AppName = string.Empty;
            WorkstationId = string.Empty;
            Encrypt = string.Empty;
            TrustServerCert = string.Empty;
            Domain = string.Empty;
            TimeOut = 1;
        }

        public string Instance { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Hash { get; set; }
        public string Domain { get; set; }
        public string Database { get; set; }
        public bool Dac { get; set; }
        public string AppName { get; set; }
        public string WorkstationId { get; set; }
        public string Encrypt { get; set; }
        public string TrustServerCert { get; set; }
        public int TimeOut { get; set; }
        public int? Port { get; set; }
        public int? PacketSize { get; set; }
        public bool ForceNamedPipe { get; set; }
        public bool DebugSql { get; set; }

        public bool UsesPassTheHash
        {
            get { return !string.IsNullOrWhiteSpace(Hash); }
        }

        public SqlConnectionOptions Clone()
        {
            return new SqlConnectionOptions
            {
                Instance = Instance,
                Username = Username,
                Password = Password,
                Hash = Hash,
                Domain = Domain,
                Database = Database,
                Dac = Dac,
                AppName = AppName,
                WorkstationId = WorkstationId,
                Encrypt = Encrypt,
                TrustServerCert = TrustServerCert,
                TimeOut = TimeOut,
                Port = Port,
                PacketSize = PacketSize,
                ForceNamedPipe = ForceNamedPipe,
                DebugSql = DebugSql
            };
        }
    }
}
