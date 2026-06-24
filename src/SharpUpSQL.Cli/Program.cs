using System;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Cli
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var parsed = CliArgumentParser.Parse(args);
            if (!string.IsNullOrEmpty(parsed.Error))
            {
                PrintUsage(parsed.Error);
                return 1;
            }

            ISharpUpSqlCommand command;
            if (!CommandRegistry.TryGet(parsed.CommandName, out command))
            {
                PrintUsage("Unknown command: " + parsed.CommandName);
                return 1;
            }

            CliArgumentParser.LoadPipelineFromStdin(parsed);

            var context = new SharpUpSqlContext();
            context.Verbose.Enabled = parsed.Verbose;
            context.DebugSql = parsed.DebugSql;
            context.OutputFormat = parsed.OutputFormat;

            foreach (var pair in parsed.Arguments)
            {
                context.Arguments[pair.Key] = pair.Value;
            }

            foreach (var sw in parsed.Switches)
            {
                context.Switches.Add(sw);
            }

            context.Pipeline.AddRange(parsed.Pipeline);

            try
            {
                var results = command.Execute(context).ToList();
                if (string.Equals(context.OutputFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(JsonPipeline.SerializeResults(results));
                }
                else
                {
                    ResultFormatter.WriteResults(results);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                if (parsed.Verbose)
                {
                    Console.Error.WriteLine(ex.ToString());
                }

                return 1;
            }
        }

        private static void PrintUsage(string message)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: SharpUpSQL.exe <Command> [-Verbose] [-Debug] [-Parameter Value] [-Switch]");
            Console.Error.WriteLine("       SharpUpSQL.exe <Command> --stdin json [--format json]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Global options:");
            Console.Error.WriteLine("  -Port <n>              Custom TCP port (appended to -Instance)");
            Console.Error.WriteLine("  -Hash <ntlm>           NT hash for pass-the-hash (-Domain, -Username required)");
            Console.Error.WriteLine("  -ForceNamedPipe        Connect via named pipe");
            Console.Error.WriteLine("  -PacketSize <n>        TDS packet size");
            Console.Error.WriteLine("  -Instance h1,h2[,port] Comma-separated multi-host targets");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Available commands:");
            foreach (var name in CommandRegistry.GetCommandNames())
            {
                Console.Error.WriteLine("  " + name);
            }
        }
    }
}
