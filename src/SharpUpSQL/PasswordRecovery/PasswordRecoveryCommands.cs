using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Attack;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;

namespace SharpUpSQL.PasswordRecovery
{
    public sealed class GetSqlRecoverPwAutoLogonCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLRecoverPwAutoLogon"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                PasswordRecoveryEngine.GetSqlRecoverPwAutoLogon(
                    options,
                    instance,
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class GetSqlServerPasswordHashCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLServerPasswordHash"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            return CommonCommandHelper.ForEachTarget(context, (instance, options, suppressVerbose) =>
                PasswordRecoveryEngine.GetSqlServerPasswordHash(
                    options,
                    instance,
                    GetArg(context, "PrincipalName"),
                    GetSwitch(context, "Migrate"),
                    context.Verbose,
                    suppressVerbose).Cast<object>());
        }
    }

    public sealed class InvokeSqlUncPathInjectionCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-SQLUncPathInjection"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var options = context.BuildConnectionOptions();
            var instance = GetArg(context, "Instance");
            if (string.IsNullOrWhiteSpace(instance) && context.Pipeline.Count > 0)
            {
                instance = context.Pipeline[0].Instance;
            }

            return PasswordRecoveryEngine.InvokeSqlUncPathInjection(
                options,
                instance,
                GetArg(context, "CaptureIp"),
                GetArg(context, "DomainController"),
                GetIntArg(context, "TimeOut", 5),
                GetIntArg(context, "Threads", 10),
                context.Verbose,
                GetSwitch(context, "SuppressVerbose"));
        }
    }

    public sealed class InvokeTokenManipulationCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Invoke-TokenManipulation"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            if (GetSwitch(context, "RevToSelf"))
            {
                var reverted = TokenManipulationHelper.Revert();
                yield return new TokenManipulationResult
                {
                    Status = reverted ? "Success" : "Failed",
                    Message = reverted ? "Reverted to self." : "Failed to revert token."
                };
                yield break;
            }

            var processId = GetIntArg(context, "ProcessId", 0);
            if (processId == 0)
            {
                string processIdValue;
                if (context.Arguments.TryGetValue("ProcessId", out processIdValue))
                {
                    int.TryParse(processIdValue, out processId);
                }
            }

            if (processId == 0)
            {
                WriteVerbose(context, "ProcessId is required.");
                yield break;
            }

            if (GetSwitch(context, "ImpersonateUser"))
            {
                var success = TokenManipulationHelper.ImpersonateProcess(processId);
                yield return new TokenManipulationResult
                {
                    Status = success ? "Success" : "Failed",
                    Message = success
                        ? "Impersonated user token from process " + processId + "."
                        : "Failed to impersonate process " + processId + "."
                };
                yield break;
            }

            if (GetSwitch(context, "CreateProcess"))
            {
                var application = GetArg(context, "CreateProcess") ?? "cmd.exe";
                var arguments = GetArg(context, "ProcessArgs");
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    arguments = GetArg(context, "CommandLine");
                }

                var success = TokenManipulationHelper.CreateProcessWithToken(processId, application, arguments);
                yield return new TokenManipulationResult
                {
                    Status = success ? "Success" : "Failed",
                    Message = success
                        ? "Created process " + application + " with token from process " + processId + "."
                        : "Failed to create process with token from process " + processId + "."
                };
            }
        }
    }
}
