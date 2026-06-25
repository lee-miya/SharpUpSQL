using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using SharpUpSQL.Core.Auth;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Core.Execution
{
    public static class SqlPermissionHelper
    {
        public const int SelectPermissionDeniedErrorNumber = 229;

        public static bool IsSelectPermissionDenied(SqlException ex, string objectName = null)
        {
            if (ex == null)
            {
                return false;
            }

            foreach (SqlError error in ex.Errors)
            {
                if (error.Number != SelectPermissionDeniedErrorNumber)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(objectName))
                {
                    return error.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (error.Message.IndexOf(objectName, StringComparison.OrdinalIgnoreCase) >= 0
                    && error.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (string.IsNullOrEmpty(objectName))
            {
                return ex.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return ex.Message.IndexOf(objectName, StringComparison.OrdinalIgnoreCase) >= 0
                && ex.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryExecuteQuery(
            SqlConnectionOptions options,
            string query,
            VerboseWriter verbose,
            bool suppressVerbose,
            out List<Dictionary<string, object>> rows)
        {
            try
            {
                rows = QueryExecutor.ExecuteQuery(options, query, verbose, suppressVerbose);
                return true;
            }
            catch (SqlException ex)
            {
                if (!IsSelectPermissionDenied(ex))
                {
                    throw;
                }

                rows = new List<Dictionary<string, object>>();
                WritePermissionDenied(verbose, suppressVerbose, ex);
                return false;
            }
        }

        public static List<Dictionary<string, object>> ExecuteQueryWithFallback(
            SqlConnectionOptions options,
            string primaryQuery,
            string fallbackQuery,
            string deniedObjectName,
            VerboseWriter verbose,
            bool suppressVerbose,
            string fallbackMessage = null)
        {
            try
            {
                return QueryExecutor.ExecuteQuery(options, primaryQuery, verbose, suppressVerbose);
            }
            catch (SqlException ex)
            {
                if (!IsSelectPermissionDenied(ex, deniedObjectName))
                {
                    throw;
                }

                if (!suppressVerbose && verbose != null)
                {
                    verbose.Write(
                        fallbackMessage
                        ?? ("SELECT on " + deniedObjectName + " denied; using reduced query."));
                }

                return QueryExecutor.ExecuteQuery(options, fallbackQuery, verbose, suppressVerbose);
            }
        }

        private static void WritePermissionDenied(VerboseWriter verbose, bool suppressVerbose, SqlException ex)
        {
            if (!suppressVerbose && verbose != null)
            {
                verbose.Write("SELECT permission denied: " + ex.Message);
            }
        }
    }
}
