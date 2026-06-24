using System;
using System.Data.SqlClient;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Core.Auth
{
    /// <summary>
    /// Creates <see cref="SqlConnection"/> objects matching PowerUpSQL Get-SQLConnectionObject behavior.
    /// Pass-the-hash uses <see cref="PthTdsClient"/> instead of SqlConnection.
    /// </summary>
    public static class SqlConnectionFactory
    {
        public static SqlConnection CreateConnection(SqlConnectionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.UsesPassTheHash)
            {
                throw new InvalidOperationException(
                    "Pass-the-hash connections must use PthTdsClient or QueryExecutor, not SqlConnection.");
            }

            var instance = string.IsNullOrWhiteSpace(options.Instance)
                ? Environment.MachineName
                : ServerAddressHelper.FormatServer(options.Instance, options.Port, options.ForceNamedPipe);

            var database = string.IsNullOrWhiteSpace(options.Database) ? "Master" : options.Database;
            var dacPrefix = options.Dac ? "ADMIN:" : string.Empty;

            var appNameString = string.IsNullOrEmpty(options.AppName)
                ? string.Empty
                : ";Application Name=\"" + options.AppName + "\"";

            var workstationString = string.IsNullOrEmpty(options.WorkstationId)
                ? string.Empty
                : ";Workstation Id=\"" + options.WorkstationId + "\"";

            var encryptString = string.IsNullOrEmpty(options.Encrypt) ? string.Empty : ";Encrypt=Yes";
            var trustCertString = string.IsNullOrEmpty(options.TrustServerCert)
                ? string.Empty
                : ";TrustServerCertificate=Yes";

            var packetSizeString = options.PacketSize.HasValue && options.PacketSize.Value > 0
                ? ";Packet Size=" + options.PacketSize.Value
                : string.Empty;

            var connection = new SqlConnection();
            var username = options.Username;

            if (string.IsNullOrEmpty(username))
            {
                connection.ConnectionString = string.Format(
                    "Server={0}{1};Database={2};Integrated Security=SSPI;Connection Timeout={3}{4}{5}{6}{7}{8}",
                    dacPrefix,
                    instance,
                    database,
                    options.TimeOut,
                    appNameString,
                    encryptString,
                    trustCertString,
                    workstationString,
                    packetSizeString);
            }
            else if (username.Contains("\\"))
            {
                connection.ConnectionString = string.Format(
                    "Server={0}{1};Database={2};Integrated Security=SSPI;uid={3};pwd={4};Connection Timeout={5}{6}{7}{8}{9}{10}",
                    dacPrefix,
                    instance,
                    database,
                    username,
                    options.Password,
                    options.TimeOut,
                    appNameString,
                    encryptString,
                    trustCertString,
                    workstationString,
                    packetSizeString);
            }
            else
            {
                connection.ConnectionString = string.Format(
                    "Server={0}{1};Database={2};User ID={3};Password={4};Connection Timeout={5}{6}{7}{8}{9}{10}",
                    dacPrefix,
                    instance,
                    database,
                    username,
                    options.Password,
                    options.TimeOut,
                    appNameString,
                    encryptString,
                    trustCertString,
                    workstationString,
                    packetSizeString);
            }

            return connection;
        }
    }
}
