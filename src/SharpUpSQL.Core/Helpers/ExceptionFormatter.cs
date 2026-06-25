using System;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpUpSQL.Core.Helpers
{
    /// <summary>
    /// Formats exceptions for console output without exposing build-machine source paths.
    /// </summary>
    public static class ExceptionFormatter
    {
        private static readonly Regex StackFramePathPattern = new Regex(
            @"\s+in\s+(?:[a-zA-Z]:)?[^:]+:line\s+\d+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string Format(Exception exception, bool includeStackTrace)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendException(builder, exception, includeStackTrace);
            return builder.ToString().TrimEnd();
        }

        public static string SanitizeStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return stackTrace;
            }

            return StackFramePathPattern.Replace(stackTrace, string.Empty);
        }

        private static void AppendException(StringBuilder builder, Exception exception, bool includeStackTrace)
        {
            builder.Append(exception.GetType().FullName);
            builder.Append(": ");
            builder.Append(exception.Message);

            if (includeStackTrace && !string.IsNullOrEmpty(exception.StackTrace))
            {
                builder.AppendLine();
                builder.Append(SanitizeStackTrace(exception.StackTrace));
            }

            if (exception.InnerException != null)
            {
                builder.AppendLine();
                builder.Append(" ---> ");
                AppendException(builder, exception.InnerException, includeStackTrace);
                builder.AppendLine();
                builder.Append("   --- End of inner exception stack trace ---");
            }
        }
    }
}
