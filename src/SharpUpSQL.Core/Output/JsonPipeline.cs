using System;
using System.Collections.Generic;
using System.Text;

namespace SharpUpSQL.Core.Output
{
    public static class JsonPipeline
    {
        public static List<PipelineObject> ParsePipelineJson(string json)
        {
            var results = new List<PipelineObject>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return results;
            }

            var trimmed = json.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                throw new FormatException("JSON pipeline input must be an array.");
            }

            var index = 1;
            while (index < trimmed.Length)
            {
                index = SkipWhitespace(trimmed, index);
                if (index >= trimmed.Length || trimmed[index] == ']')
                {
                    break;
                }

                if (trimmed[index] != '{')
                {
                    throw new FormatException("Expected object in JSON pipeline array.");
                }

                string objectJson;
                index = ReadObject(trimmed, index, out objectJson);
                results.Add(ParsePipelineObject(objectJson));
                index = SkipWhitespace(trimmed, index);
                if (index < trimmed.Length && trimmed[index] == ',')
                {
                    index++;
                }
            }

            return results;
        }

        public static string SerializeResults(IEnumerable<object> results)
        {
            var builder = new StringBuilder();
            builder.Append('[');
            var first = true;
            foreach (var item in results)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(SerializeObject(item));
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static PipelineObject ParsePipelineObject(string objectJson)
        {
            var obj = new PipelineObject();
            var index = 1;
            while (index < objectJson.Length)
            {
                index = SkipWhitespace(objectJson, index);
                if (index >= objectJson.Length || objectJson[index] == '}')
                {
                    break;
                }

                string key;
                index = ReadString(objectJson, index, out key);
                index = SkipWhitespace(objectJson, index);
                if (index >= objectJson.Length || objectJson[index] != ':')
                {
                    throw new FormatException("Invalid JSON property.");
                }

                index++;
                index = SkipWhitespace(objectJson, index);
                string value;
                index = ReadString(objectJson, index, out value);

                if (string.Equals(key, "Instance", StringComparison.OrdinalIgnoreCase))
                {
                    obj.Instance = value;
                }
                else if (string.Equals(key, "ComputerName", StringComparison.OrdinalIgnoreCase))
                {
                    obj.ComputerName = value;
                }
                else if (string.Equals(key, "IPAddress", StringComparison.OrdinalIgnoreCase))
                {
                    obj.IPAddress = value;
                }

                index = SkipWhitespace(objectJson, index);
                if (index < objectJson.Length && objectJson[index] == ',')
                {
                    index++;
                }
            }

            return obj;
        }

        private static string SerializeObject(object item)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            var first = true;
            foreach (var property in item.GetType().GetProperties())
            {
                if (!property.CanRead)
                {
                    continue;
                }

                var value = property.GetValue(item, null);
                if (value == null)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(Escape(property.Name)).Append("\":");
                builder.Append(SerializeValue(value));
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static string SerializeValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            return "\"" + Escape(value.ToString()) + "\"";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            return index;
        }

        private static int ReadObject(string text, int start, out string objectJson)
        {
            var depth = 0;
            var index = start;
            for (; index < text.Length; index++)
            {
                if (text[index] == '{')
                {
                    depth++;
                }
                else if (text[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objectJson = text.Substring(start, index - start + 1);
                        return index + 1;
                    }
                }
            }

            throw new FormatException("Unterminated JSON object.");
        }

        private static int ReadString(string text, int start, out string value)
        {
            if (start >= text.Length || text[start] != '"')
            {
                throw new FormatException("Expected JSON string.");
            }

            var builder = new StringBuilder();
            for (var i = start + 1; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '"')
                {
                    value = builder.ToString();
                    return i + 1;
                }

                if (ch == '\\' && i + 1 < text.Length)
                {
                    i++;
                    switch (text[i])
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        default:
                            builder.Append(text[i]);
                            break;
                    }
                }
                else
                {
                    builder.Append(ch);
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }
    }
}
