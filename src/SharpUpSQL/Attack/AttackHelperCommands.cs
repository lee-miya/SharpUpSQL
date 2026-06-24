using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Execution;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Attack
{
    internal static class AssemblyFileEngine
    {
        internal static IEnumerable<SqlAssemblyFileResult> Execute(
            SqlConnectionOptions options,
            string instance,
            string databaseName,
            string assemblyName,
            string exportFolder,
            bool noDefaults,
            VerboseWriter verbose,
            bool suppressVerbose)
        {
            if (string.IsNullOrWhiteSpace(instance))
            {
                instance = Environment.MachineName;
            }

            var computerName = InstanceHelper.GetComputerName(instance);
            options = options.Clone();
            options.Instance = instance;

            var test = ConnectionTester.Test(instance, null, null, options, verbose, suppressVerbose);
            if (!string.Equals(test.Status, "Accessible", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            foreach (var database in SqlEnumerationEngine.GetSqlDatabase(
                         options,
                         instance,
                         databaseName,
                         noDefaults,
                         true,
                         false,
                         verbose,
                         suppressVerbose))
            {
                var dbName = database.DatabaseName;
                if (!suppressVerbose && verbose != null && verbose.Enabled)
                {
                    verbose.Write(instance + " : Grabbing assembly file information from " + dbName + ".");
                }

                var assemblyFilter = string.IsNullOrWhiteSpace(assemblyName)
                    ? string.Empty
                    : "WHERE af.name LIKE " + SqlValueFormatter.QuoteLiteral("%" + assemblyName + "%");

                var query = "USE " + QuoteIdentifier(dbName) + ";" +
                            " SELECT af.assembly_id, a.name AS assembly_name, af.file_id, af.name AS file_name," +
                            " a.clr_name, af.content, a.permission_set_desc, a.create_date, a.modify_date, a.is_user_defined" +
                            " FROM sys.assemblies a INNER JOIN sys.assembly_files af ON a.assembly_id = af.assembly_id " +
                            assemblyFilter;

                foreach (var row in SqlEnumerationEngine.GetSqlQuery(options, query, instance, verbose, suppressVerbose))
                {
                    var result = new SqlAssemblyFileResult
                    {
                        ComputerName = computerName,
                        Instance = instance,
                        DatabaseName = dbName,
                        AssemblyId = GetRowValue(row, "assembly_id"),
                        AssemblyName = GetRowValue(row, "assembly_name"),
                        FileId = GetRowValue(row, "file_id"),
                        FileName = GetRowValue(row, "file_name"),
                        ClrName = GetRowValue(row, "clr_name"),
                        Content = GetRowValue(row, "content"),
                        PermissionSetDesc = GetRowValue(row, "permission_set_desc"),
                        CreateDate = GetRowValue(row, "create_date"),
                        ModifyDate = GetRowValue(row, "modify_date"),
                        IsUserDefined = GetRowValue(row, "is_user_defined")
                    };

                    if (!string.IsNullOrWhiteSpace(exportFolder))
                    {
                        ExportAssemblyFile(instance, dbName, result, exportFolder, verbose);
                    }

                    yield return result;
                }
            }
        }

        private static void ExportAssemblyFile(
            string instance,
            string databaseName,
            SqlAssemblyFileResult row,
            string exportFolder,
            VerboseWriter verbose)
        {
            var exportRoot = Path.Combine(exportFolder, "CLRExports");
            Directory.CreateDirectory(exportRoot);

            var instanceFolder = Path.Combine(exportRoot, (instance ?? string.Empty).Replace('\\', '_'));
            Directory.CreateDirectory(instanceFolder);

            var databaseFolder = Path.Combine(instanceFolder, databaseName ?? string.Empty);
            Directory.CreateDirectory(databaseFolder);

            var fileName = row.FileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "assembly";
            }

            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".dll";
            }

            var fullPath = Path.Combine(databaseFolder, fileName);
            if (verbose != null && verbose.Enabled)
            {
                verbose.Write(instance + " : - Exporting " + fileName);
            }

            var bytes = ParseContentBytes(row.Content);
            if (bytes != null && bytes.Length > 0)
            {
                File.WriteAllBytes(fullPath, bytes);
            }
        }

        private static byte[] ParseContentBytes(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
            }

            return bytes;
        }

        private static string GetRowValue(SqlQueryResult row, string key)
        {
            if (row == null)
            {
                return string.Empty;
            }

            return SqlValueFormatter.Format(row[key]);
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "[" + (identifier ?? string.Empty).Replace("]", "]]") + "]";
        }
    }

    public sealed class CreateSqlFileXpDllCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Create-SQLFileXpDll"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            yield return XpDllGenerator.Generate(
                GetArg(context, "Command"),
                GetArg(context, "ExportName"),
                GetArg(context, "OutFile"),
                msg => WriteVerbose(context, msg));
        }
    }

    public sealed class CreateSqlFileClrDllCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Create-SQLFileCLRDll"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var result = ClrDllGenerator.CreateFiles(
                GetArg(context, "OutDir") ?? Path.GetTempPath(),
                GetArg(context, "OutFile"),
                GetArg(context, "ProcedureName"),
                GetArg(context, "AssemblyName"),
                GetArg(context, "AssemblyClassName"),
                GetArg(context, "AssemblyMethodName"),
                GetArg(context, "SourceDllPath"));

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                throw new InvalidOperationException(result.Error);
            }

            WriteVerbose(context, "Target C#  File: " + result.SourcePath);
            WriteVerbose(context, "Target DLL File: " + result.DllPath);

            yield return new CreateSqlFileResult
            {
                OutFile = result.DllPath,
                Message = "CLR files written: " + result.SqlScriptPath
            };
        }
    }

    public sealed class GetSqlAssemblyFileCommand : SharpUpSqlCommandBase
    {
        public override string Name { get { return "Get-SQLAssemblyFile"; } }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var options = context.BuildConnectionOptions();

            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var instance = !string.IsNullOrWhiteSpace(target.Instance)
                    ? target.Instance
                    : options.Instance;

                foreach (var row in AssemblyFileEngine.Execute(
                             options,
                             instance,
                             GetArg(context, "DatabaseName"),
                             GetArg(context, "AssemblyName"),
                             GetArg(context, "ExportFolder"),
                             GetSwitch(context, "NoDefaults"),
                             context.Verbose,
                             suppressVerbose))
                {
                    yield return row;
                }
            }
        }
    }
}
