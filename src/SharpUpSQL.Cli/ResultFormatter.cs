using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SharpUpSQL.Common;

namespace SharpUpSQL.Cli
{
    public static class ResultFormatter
    {
        public static void WriteResults(IEnumerable<object> results)
        {
            WriteResults(results, Console.Out);
        }

        public static void WriteResults(IEnumerable<object> results, TextWriter writer)
        {
            if (writer == null)
            {
                writer = Console.Out;
            }

            var list = results != null ? results.ToList() : new List<object>();
            if (list.Count == 0)
            {
                return;
            }

            var maps = list.Select(GetRowValues).ToList();
            var headers = maps.SelectMany(m => m.Keys).Distinct().ToList();
            var widths = headers.ToDictionary(h => h, h => h.Length);

            foreach (var map in maps)
            {
                foreach (var header in headers)
                {
                    string value;
                    widths[header] = Math.Max(
                        widths[header],
                        map.TryGetValue(header, out value) ? value.Length : 0);
                }
            }

            WriteRow(writer, headers, widths, headers.ToDictionary(h => h, h => h));
            writer.WriteLine();

            foreach (var map in maps)
            {
                WriteRow(writer, headers, widths, map);
                writer.WriteLine();
            }
        }

        private static void WriteRow(
            TextWriter writer,
            List<string> headers,
            Dictionary<string, int> widths,
            Dictionary<string, string> values)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(" ");
                }

                string value;
                if (!values.TryGetValue(headers[i], out value))
                {
                    value = string.Empty;
                }

                writer.Write(value.PadRight(widths[headers[i]]));
            }
        }

        private static Dictionary<string, string> GetRowValues(object item)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var queryRow = item as SqlQueryResult;
            if (queryRow != null)
            {
                values["ComputerName"] = queryRow.ComputerName ?? string.Empty;
                values["Instance"] = queryRow.Instance ?? string.Empty;
                foreach (var pair in queryRow.Columns)
                {
                    values[pair.Key] = FormatValue(pair.Value);
                }

                return values;
            }

            foreach (var property in item.GetType()
                         .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(p => p.CanRead))
            {
                values[property.Name] = FormatValue(property.GetValue(item, null));
            }

            return values;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is DateTime)
            {
                return ((DateTime)value).ToString("g");
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var parts = new List<string>();
                foreach (var item in enumerable)
                {
                    parts.Add(item != null ? item.ToString() : string.Empty);
                }

                return string.Join(", ", parts.ToArray());
            }

            return value.ToString();
        }
    }
}
