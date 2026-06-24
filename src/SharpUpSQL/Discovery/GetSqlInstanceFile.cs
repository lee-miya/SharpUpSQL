using System;
using System.Collections.Generic;
using System.IO;
using SharpUpSQL.Commands;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Discovery
{
    public sealed class SqlInstanceFileResult
    {
        public string ComputerName { get; set; }
        public string Instance { get; set; }
    }

    public sealed class GetSqlInstanceFileCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Get-SQLInstanceFile"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var filePath = GetArg(context, "FilePath");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Console.Error.WriteLine("File path does not appear to be valid.");
                yield break;
            }

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine("File path does not appear to be valid.");
                yield break;
            }

            WriteVerbose(context, "Importing instances from file path.");
            var count = 0;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var instance = line != null ? line.Trim() : null;
                if (string.IsNullOrEmpty(instance))
                {
                    continue;
                }

                var computerName = InstanceParser.GetComputerNameFromInstance(instance);
                count++;
                yield return new SqlInstanceFileResult
                {
                    ComputerName = computerName,
                    Instance = instance
                };
            }

            WriteVerbose(context, count + " instances where found in " + filePath + ".");
        }
    }
}
