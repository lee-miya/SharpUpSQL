using System;
using System.Collections.Generic;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Audit
{
    public sealed class AuditContext
    {
        public SqlConnectionOptions Options { get; set; }
        public string Instance { get; set; }
        public VerboseWriter Verbose { get; set; }
        public bool SuppressVerbose { get; set; }
        public bool Exploit { get; set; }
        public bool NoOutput { get; set; }
        public bool Nested { get; set; }
        public bool NoDefaults { get; set; }
        public bool NoUserAsPass { get; set; }
        public bool NoUserEnum { get; set; }
        public string AttackerIp { get; set; }
        public int TimeOut { get; set; }
        public string Keyword { get; set; }
        public int SampleSize { get; set; }
        public string TestUsername { get; set; }
        public string TestPassword { get; set; }
        public string UserFile { get; set; }
        public string PassFile { get; set; }
        public int FuzzNum { get; set; }
        public string OutFolder { get; set; }

        public static AuditContext From(SharpUpSqlContext context, PipelineObject target)
        {
            var options = context.BuildConnectionOptions();
            var instance = !string.IsNullOrWhiteSpace(target.Instance)
                ? target.Instance
                : options.Instance;

            if (!string.IsNullOrWhiteSpace(instance))
            {
                options.Instance = instance;
            }

            if (string.IsNullOrWhiteSpace(instance))
            {
                instance = Environment.MachineName;
            }

            string value;
            int parsed;

            var audit = new AuditContext
            {
                Options = options,
                Instance = instance,
                Verbose = context.Verbose,
                SuppressVerbose = context.Switches.Contains("SuppressVerbose"),
                Exploit = context.Switches.Contains("Exploit"),
                NoOutput = context.Switches.Contains("NoOutput"),
                Nested = context.Switches.Contains("Nested"),
                NoDefaults = context.Switches.Contains("NoDefaults"),
                NoUserAsPass = context.Switches.Contains("NoUserAsPass"),
                NoUserEnum = context.Switches.Contains("NoUserEnum"),
                Keyword = GetArg(context, "Keyword") ?? "Password",
                TestUsername = GetArg(context, "TestUsername"),
                TestPassword = GetArg(context, "TestPassword"),
                UserFile = GetArg(context, "UserFile"),
                PassFile = GetArg(context, "PassFile"),
                AttackerIp = GetArg(context, "AttackerIp"),
                OutFolder = GetArg(context, "OutFolder"),
                TimeOut = 5,
                SampleSize = 1,
                FuzzNum = 10000
            };

            if (context.Arguments.TryGetValue("TimeOut", out value) &&
                int.TryParse(value, out parsed))
            {
                audit.TimeOut = parsed;
            }

            if (context.Arguments.TryGetValue("SampleSize", out value) &&
                int.TryParse(value, out parsed))
            {
                audit.SampleSize = parsed;
            }

            if (context.Arguments.TryGetValue("FuzzNum", out value) &&
                int.TryParse(value, out parsed))
            {
                audit.FuzzNum = parsed;
            }

            return audit;
        }

        private static string GetArg(SharpUpSqlContext context, string name)
        {
            string value;
            return context.Arguments.TryGetValue(name, out value) ? value : null;
        }
    }
}
