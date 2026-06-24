using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Core.Execution
{
    public static class QueryExecutor
    {
        public static List<Dictionary<string, object>> ExecuteQuery(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose = null,
            bool suppressVerbose = false)
        {
            return ExecuteQuery(options, query, verbose, suppressVerbose, options != null && options.DebugSql);
        }

        public static List<Dictionary<string, object>> ExecuteQuery(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose,
            bool suppressVerbose,
            bool debugSql)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query cannot be empty.", "query");
            }

            if (debugSql && verbose != null)
            {
                verbose.Write("[DEBUG SQL] " + query);
            }

            if (options.UsesPassTheHash)
            {
                return ExecutePthQuery(options, query, verbose, debugSql);
            }

            var results = new List<Dictionary<string, object>>();

            using (var connection = SqlConnectionFactory.CreateConnection(options))
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose.Write("Connecting to " + options.Instance + "...");
                }

                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandTimeout = options.TimeOut <= 0 ? 30 : options.TimeOut;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(ReadRow(reader));
                        }
                    }
                }
            }

            if (!suppressVerbose && verbose != null)
            {
                verbose.Write(results.Count + " row(s) returned.");
            }

            return results;
        }

        public static object ExecuteScalar(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose = null,
            bool suppressVerbose = false)
        {
            return ExecuteScalar(options, query, verbose, suppressVerbose, options != null && options.DebugSql);
        }

        public static object ExecuteScalar(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose,
            bool suppressVerbose,
            bool debugSql)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.UsesPassTheHash)
            {
                var rows = ExecutePthQuery(options, query, verbose, debugSql);
                if (rows.Count == 0)
                {
                    return null;
                }

                foreach (var value in rows[0].Values)
                {
                    return value;
                }

                return null;
            }

            using (var connection = SqlConnectionFactory.CreateConnection(options))
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose.Write("Connecting to " + options.Instance + "...");
                }

                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandTimeout = options.TimeOut <= 0 ? 30 : options.TimeOut;
                    return command.ExecuteScalar();
                }
            }
        }

        public static void ExecuteNonQuery(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose = null,
            bool suppressVerbose = false)
        {
            ExecuteNonQuery(options, query, verbose, suppressVerbose, options != null && options.DebugSql);
        }

        public static void ExecuteNonQuery(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose,
            bool suppressVerbose,
            bool debugSql)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.UsesPassTheHash)
            {
                ExecutePthQuery(options, query, verbose, debugSql);
                return;
            }

            using (var connection = SqlConnectionFactory.CreateConnection(options))
            {
                if (!suppressVerbose && verbose != null)
                {
                    verbose.Write("Connecting to " + options.Instance + "...");
                }

                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandTimeout = options.TimeOut <= 0 ? 30 : options.TimeOut;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static SqlConnectionOptions PrepareOptions(SqlConnectionOptions options, string instance)
        {
            var prepared = options != null ? options.Clone() : new SqlConnectionOptions();
            prepared.Instance = ServerAddressHelper.FormatServer(
                instance,
                prepared.Port,
                prepared.ForceNamedPipe);
            return prepared;
        }

        private static List<Dictionary<string, object>> ExecutePthQuery(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose,
            bool debugSql)
        {
            var prepared = options.Clone();
            prepared.Instance = ServerAddressHelper.FormatServer(
                prepared.Instance,
                prepared.Port,
                prepared.ForceNamedPipe);

            using (var client = new PthTdsClient(prepared))
            {
                if (verbose != null)
                {
                    verbose.Write("PTH: connecting to " + prepared.Instance + "...");
                }

                return client.ExecuteQuery(query, verbose, debugSql);
            }
        }

        private static Dictionary<string, object> ReadRow(IDataRecord reader)
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[name] = value;
            }

            return row;
        }
    }
}
