using System;
using System.Collections.Generic;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Cli
{
    public sealed class ParsedCli
    {
        public ParsedCli()
        {
            Arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Switches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Pipeline = new List<PipelineObject>();
        }

        public string CommandName { get; set; }
        public Dictionary<string, string> Arguments { get; private set; }
        public HashSet<string> Switches { get; private set; }
        public List<PipelineObject> Pipeline { get; private set; }
        public bool Verbose { get; set; }
        public bool DebugSql { get; set; }
        public string OutputFormat { get; set; }
        public string StdinFormat { get; set; }
        public string Error { get; set; }
    }

    public static class CliArgumentParser
    {
        public static ParsedCli Parse(string[] args)
        {
            var parsed = new ParsedCli();
            if (args == null || args.Length == 0)
            {
                parsed.Error = "No command specified.";
                return parsed;
            }

            var index = 0;
            parsed.CommandName = args[index++];

            while (index < args.Length)
            {
                var token = args[index];
                if (string.Equals(token, "-Verbose", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "--verbose", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Verbose = true;
                    index++;
                    continue;
                }

                if (string.Equals(token, "-Debug", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "--debug", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.DebugSql = true;
                    index++;
                    continue;
                }

                if (token.StartsWith("-", StringComparison.Ordinal))
                {
                    var name = token.TrimStart('-');
                    if (string.Equals(name, "format", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "stdin", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length)
                        {
                            parsed.Error = "Missing value for " + token;
                            return parsed;
                        }

                        var value = args[index + 1];
                        if (string.Equals(name, "format", StringComparison.OrdinalIgnoreCase))
                        {
                            parsed.OutputFormat = value;
                        }
                        else
                        {
                            parsed.StdinFormat = value;
                        }

                        index += 2;
                        continue;
                    }

                    if (index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        parsed.Arguments[name] = args[index + 1];
                        index += 2;
                    }
                    else
                    {
                        parsed.Switches.Add(name);
                        index++;
                    }

                    continue;
                }

                parsed.Error = "Unexpected argument: " + token;
                return parsed;
            }

            return parsed;
        }

        public static void LoadPipelineFromStdin(ParsedCli parsed)
        {
            if (!Console.IsInputRedirected || string.IsNullOrEmpty(parsed.StdinFormat))
            {
                return;
            }

            var input = Console.In.ReadToEnd();
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            if (string.Equals(parsed.StdinFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                parsed.Pipeline.AddRange(JsonPipeline.ParsePipelineJson(input));
                return;
            }

            foreach (var line in input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                parsed.Pipeline.Add(new PipelineObject { Instance = trimmed });
            }
        }
    }
}
