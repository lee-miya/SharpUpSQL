using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Common
{
    public abstract class SqlInstanceResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
    }

    public sealed class SqlQueryResult : SqlInstanceResult
    {
        private readonly Dictionary<string, object> _columns = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, object> Columns
        {
            get { return _columns; }
        }

        public object this[string name]
        {
            get
            {
                object value;
                return _columns.TryGetValue(name, out value) ? value : null;
            }
            set { _columns[name] = value; }
        }
    }

    public sealed class SqlServerInfoResult : SqlInstanceResult
    {
        public string DomainName { get; set; }
        public string ServiceProcessID { get; set; }
        public string ServiceName { get; set; }
        public string ServiceAccount { get; set; }
        public string AuthenticationMode { get; set; }
        public string ForcedEncryption { get; set; }
        public string Clustered { get; set; }
        public string SQLServerVersionNumber { get; set; }
        public string SQLServerMajorVersion { get; set; }
        public string SQLServerEdition { get; set; }
        public string SQLServerServicePack { get; set; }
        public string OSArchitecture { get; set; }
        public string OsMachineType { get; set; }
        public string OSVersionName { get; set; }
        public string OsVersionNumber { get; set; }
        public string Currentlogin { get; set; }
        public string IsSysadmin { get; set; }
        public string ActiveSessions { get; set; }
    }

    public sealed class SqlDatabaseResult : SqlInstanceResult
    {
        public string DatabaseId { get; set; }
        public string DatabaseName { get; set; }
        public string DatabaseOwner { get; set; }
        public string OwnerIsSysadmin { get; set; }
        public string is_trustworthy_on { get; set; }
        public string is_db_chaining_on { get; set; }
        public string is_broker_enabled { get; set; }
        public string is_encrypted { get; set; }
        public string is_read_only { get; set; }
        public string create_date { get; set; }
        public string recovery_model_desc { get; set; }
        public string FileName { get; set; }
        public string DbSizeMb { get; set; }
        public string has_dbaccess { get; set; }
    }

    public sealed class SqlServerLinkResult : SqlInstanceResult
    {
        public string DatabaseLinkId { get; set; }
        public string DatabaseLinkName { get; set; }
        public string DatabaseLinkLocation { get; set; }
        public string Product { get; set; }
        public string Provider { get; set; }
        public string Catalog { get; set; }
        public string LocalLogin { get; set; }
        public string RemoteLoginName { get; set; }
        public string is_rpc_out_enabled { get; set; }
        public string is_data_access_enabled { get; set; }
        public string modify_date { get; set; }
    }

    public sealed class SqlServerLinkCrawlResult : SqlInstanceResult
    {
        public string Version { get; set; }
        public string Links { get; set; }
        public string Path { get; set; }
        public string User { get; set; }
        public string Sysadmin { get; set; }
        public string CustomQuery { get; set; }
    }

    public sealed class SqlServerLinkCrawlExport2Result
    {
        public string LinkSrc { get; set; }
        public string LinkName { get; set; }
        public string LinkInstance { get; set; }
        public string LinkUser { get; set; }
        public string LinkSysadmin { get; set; }
        public string LinkVersion { get; set; }
        public string LinkHops { get; set; }
        public string LinkPath { get; set; }
    }

    public sealed class SqlTableResult : SqlInstanceResult
    {
        public string DatabaseName { get; set; }
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public string TableType { get; set; }
        public string is_ms_shipped { get; set; }
        public string is_published { get; set; }
        public string is_schema_published { get; set; }
        public string create_date { get; set; }
        public string modified_date { get; set; }
    }

    public sealed class SqlColumnResult : SqlInstanceResult
    {
        public string DatabaseName { get; set; }
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public string TableType { get; set; }
        public string ColumnName { get; set; }
        public string ColumnDataType { get; set; }
        public string ColumnMaxLength { get; set; }
        public string is_ms_shipped { get; set; }
        public string is_published { get; set; }
        public string is_schema_published { get; set; }
        public string create_date { get; set; }
        public string modified_date { get; set; }
    }

    public sealed class SqlColumnSampleDataResult : SqlInstanceResult
    {
        public string Database { get; set; }
        public string Schema { get; set; }
        public string Table { get; set; }
        public string Column { get; set; }
        public string Sample { get; set; }
        public string RowCount { get; set; }
        public string IsCC { get; set; }
    }

    public sealed class SqlServerLoginResult : SqlInstanceResult
    {
        public string PrincipalId { get; set; }
        public string PrincipalName { get; set; }
        public string PrincipalSid { get; set; }
        public string PrincipalType { get; set; }
        public string CreateDate { get; set; }
        public string IsLocked { get; set; }
    }

    public sealed class SqlAgentJobResult : SqlInstanceResult
    {
        public string DatabaseName { get; set; }
        public string Job_Id { get; set; }
        public string Job_Name { get; set; }
        public string Job_Description { get; set; }
        public string Job_Owner { get; set; }
        public string Proxy_Id { get; set; }
        public string Proxy_Credential { get; set; }
        public string Date_Created { get; set; }
        public string Last_Run_Date { get; set; }
        public string Enabled { get; set; }
        public string Server { get; set; }
        public string Step_Name { get; set; }
        public string SubSystem { get; set; }
        public string Command { get; set; }
    }

    public sealed class SqlSysadminCheckResult : SqlInstanceResult
    {
        public string CurrentLogin { get; set; }
        public string IsSysadmin { get; set; }
    }

    public sealed class SqlGenericRowResult : SqlInstanceResult
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string CreateDate { get; set; }
        public string ModifyDate { get; set; }
        public string Extra1 { get; set; }
        public string Extra2 { get; set; }
        public string Extra3 { get; set; }
    }

    internal static class SqlValueFormatter
    {
        public static string Format(object value)
        {
            if (value == null || value is DBNull)
            {
                return string.Empty;
            }

            if (value is DateTime)
            {
                return ((DateTime)value).ToString("g", CultureInfo.InvariantCulture);
            }

            if (value is byte[])
            {
                return "0x" + BitConverter.ToString((byte[])value).Replace("-", string.Empty);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public static void StampInstance(SqlInstanceResult row, string instance)
        {
            row.Instance = instance ?? Environment.MachineName;
            row.ComputerName = InstanceHelper.GetComputerName(row.Instance);
        }

        public static string BracketIdentifier(string name)
        {
            return "[" + (name ?? string.Empty).Replace("]", "]]") + "]";
        }

        public static string QuoteLiteral(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        public static string LikeFilter(string column, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return string.Empty;
            }

            return " AND " + column + " LIKE " + QuoteLiteral("%" + pattern + "%");
        }
    }
}
