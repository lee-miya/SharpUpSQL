using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using SharpUpSQL.Commands;
using SharpUpSQL.Common;
using SharpUpSQL.Core.Helpers;

namespace SharpUpSQL.Attack
{
    public sealed class SqlDumpInfoResult
    {
        public string Instance { get; set; }
        public string OutFolder { get; set; }
        public string FilesWritten { get; set; }
        public string Format { get; set; }
    }

    /// <summary>
    /// PowerUpSQL Invoke-SQLDumpInfo — exports enumeration data to CSV or XML files.
    /// </summary>
    public sealed class InvokeSqlDumpInfoCommand : SharpUpSqlCommandBase
    {
        public override string Name
        {
            get { return "Invoke-SQLDumpInfo"; }
        }

        public override IEnumerable<object> Execute(SharpUpSqlContext context)
        {
            var outFolder = GetArg(context, "OutFolder") ?? ".";
            var useXml = GetSwitch(context, "xml");
            var useCsv = GetSwitch(context, "csv") || !useXml;
            var crawlLinks = GetSwitch(context, "CrawlLinks");
            var suppressVerbose = GetSwitch(context, "SuppressVerbose");
            var format = useXml ? "xml" : "csv";
            var files = new List<string>();

            foreach (var target in InstanceTargetResolver.Resolve(context))
            {
                var options = context.BuildConnectionOptions();
                var instance = !string.IsNullOrWhiteSpace(target.Instance)
                    ? target.Instance
                    : options.Instance;

                if (!string.IsNullOrWhiteSpace(instance))
                {
                    options.Instance = instance;
                }

                var safeInstance = InstanceHelper.SanitizeFileName(instance ?? Environment.MachineName);
                Directory.CreateDirectory(outFolder);

                WriteDump(
                    outFolder,
                    safeInstance + "_Server_version_information",
                    format,
                    SqlEnumerationEngine.GetSqlServerInfo(options, instance, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Databases",
                    format,
                    SqlEnumerationEngine.GetSqlDatabase(options, instance, null, true, false, false, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Database_tables",
                    format,
                    SqlEnumerationEngine.GetSqlTable(options, instance, null, null, true, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Database_columns",
                    format,
                    SqlEnumerationEngine.GetSqlColumn(options, instance, null, null, null, null, true, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Server_logins",
                    format,
                    SqlEnumerationEngine.GetSqlServerLogin(options, instance, null, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Server_agent_jobs",
                    format,
                    SqlEnumerationEngine.GetSqlAgentJob(options, instance, null, null, false, null, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Server_agent_jobs_globaltmptbl",
                    format,
                    SqlEnumerationEngine.GetSqlAgentJob(options, instance, null, "##", false, null, context.Verbose, suppressVerbose),
                    files);

                WriteDump(
                    outFolder,
                    safeInstance + "_Server_links",
                    format,
                    SqlEnumerationEngine.GetSqlServerLink(options, instance, null, context.Verbose, suppressVerbose),
                    files);

                if (crawlLinks)
                {
                    WriteDump(
                        outFolder,
                        safeInstance + "_Server_links_crawl",
                        format,
                        LinkServerEngine.GetSqlServerLinkCrawl(
                            options,
                            instance,
                            null,
                            null,
                            false,
                            true,
                            context.Verbose,
                            suppressVerbose),
                        files);
                }

                WriteVerbose(context, files.Count + " file(s) written to " + Path.GetFullPath(outFolder));

                yield return new SqlDumpInfoResult
                {
                    Instance = instance,
                    OutFolder = Path.GetFullPath(outFolder),
                    FilesWritten = string.Join("; ", files.ToArray()),
                    Format = format
                };
            }
        }

        private static void WriteDump<T>(
            string folder,
            string baseName,
            string format,
            IEnumerable<T> rows,
            List<string> filesWritten)
        {
            var list = rows.Cast<object>().ToList();
            var path = Path.Combine(folder, baseName + "." + format);

            if (string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
            {
                WriteXml(path, list);
            }
            else
            {
                WriteCsv(path, list);
            }

            filesWritten.Add(path);
        }

        private static void WriteCsv(string path, List<object> rows)
        {
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                if (rows.Count == 0)
                {
                    return;
                }

                var maps = rows.Select(GetRowValues).ToList();
                var headers = maps.SelectMany(m => m.Keys).Distinct().ToList();

                writer.WriteLine(string.Join(",", headers.Select(EscapeCsv).ToArray()));

                foreach (var map in maps)
                {
                    var line = headers.Select(h => EscapeCsv(map.ContainsKey(h) ? map[h] : string.Empty));
                    writer.WriteLine(string.Join(",", line.ToArray()));
                }
            }
        }

        private static void WriteXml(string path, List<object> rows)
        {
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false };
            using (var writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("SharpUpSQLDump");

                foreach (var row in rows)
                {
                    writer.WriteStartElement("Row");
                    foreach (var pair in GetRowValues(row))
                    {
                        writer.WriteElementString(pair.Key, pair.Value);
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static Dictionary<string, string> GetRowValues(object row)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var queryRow = row as SqlQueryResult;
            if (queryRow != null)
            {
                values["ComputerName"] = queryRow.ComputerName ?? string.Empty;
                values["Instance"] = queryRow.Instance ?? string.Empty;
                foreach (var pair in queryRow.Columns)
                {
                    values[pair.Key] = FormatExportValue(pair.Value);
                }

                return values;
            }

            foreach (var property in row.GetType()
                         .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(p => p.CanRead))
            {
                values[property.Name] = FormatExportValue(property.GetValue(row, null));
            }

            return values;
        }

        private static string FormatExportValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
