using System;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.LinkedChain;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Tests
{
    internal static class CoreHelperTests
    {
        public static TestCase[] Cases()
        {
            return new[]
            {
                new TestCase
                {
                    Name = "LuhnHelper accepts valid Visa test number",
                    Body = () => TestAssert.True(
                        LuhnHelper.IsValidCreditCard("4111111111111111"),
                        "Visa test PAN should pass Luhn check")
                },
                new TestCase
                {
                    Name = "LuhnHelper rejects invalid number",
                    Body = () => TestAssert.True(
                        !LuhnHelper.IsValidCreditCard("4111111111111112"),
                        "Invalid PAN should fail Luhn check")
                },
                new TestCase
                {
                    Name = "InstanceParser extracts host from named instance",
                    Body = () => TestAssert.Equal(
                        "SQL01",
                        InstanceParser.GetComputerNameFromInstance("SQL01\\INSTANCE"),
                        "Named instance host parsing")
                },
                new TestCase
                {
                    Name = "InstanceParser extracts host from host,port",
                    Body = () => TestAssert.Equal(
                        "10.0.0.5",
                        InstanceParser.GetComputerNameFromInstance("10.0.0.5,1433"),
                        "Host,port parsing")
                },
                new TestCase
                {
                    Name = "ServerAddressHelper appends custom port",
                    Body = () => TestAssert.Equal(
                        "SQL01,1444",
                        ServerAddressHelper.FormatServer("SQL01", 1444, false),
                        "Port should append when not present")
                },
                new TestCase
                {
                    Name = "ServerAddressHelper preserves existing port",
                    Body = () => TestAssert.Equal(
                        "SQL01,1433",
                        ServerAddressHelper.FormatServer("SQL01,1433", 1444, false),
                        "Existing port should not be overwritten")
                },
                new TestCase
                {
                    Name = "ServerAddressHelper formats named pipe for default instance",
                    Body = () => TestAssert.Contains(
                        ServerAddressHelper.FormatServer("SQL01", null, true),
                        @"np:\\SQL01\pipe\sql\query",
                        "Default instance named pipe")
                },
                new TestCase
                {
                    Name = "LinkedChainQueryBuilder nests EXEC AT",
                    Body = () =>
                    {
                        var query = LinkedChainQueryBuilder.BuildExecAtChain(
                            "LINK_A,LINK_B",
                            "SELECT 1");
                        TestAssert.Contains(query, "AT LINK_A", "Outer link");
                        TestAssert.Contains(query, "AT LINK_B", "Inner link");
                        TestAssert.Contains(query, "SELECT 1", "Inner query preserved");
                    }
                },
                new TestCase
                {
                    Name = "LinkedChainQueryBuilder escapes dotted link names",
                    Body = () => TestAssert.Equal(
                        "[10.0.0.2]",
                        LinkedChainQueryBuilder.EscapeLinkName("10.0.0.2"),
                        "IP link names should be bracketed")
                },
                new TestCase
                {
                    Name = "ExceptionFormatter strips source paths from stack traces",
                    Body = () =>
                    {
                        var sanitized = ExceptionFormatter.SanitizeStackTrace(
                            "   at SharpUpSQL.Core.Execution.QueryExecutor.ExecuteQuery() in D:\\Project\\csProject\\SharpUpSQL\\src\\QueryExecutor.cs:line 65");
                        TestAssert.True(
                            !sanitized.Contains("D:\\Project"),
                            "Build path should be removed from stack trace");
                        TestAssert.Contains(
                            sanitized,
                            "QueryExecutor.ExecuteQuery()",
                            "Method name should remain");
                    }
                },
                new TestCase
                {
                    Name = "JsonPipeline round-trips pipeline objects",
                    Body = () =>
                    {
                        var json = "[{\"Instance\":\"SQL01\\\\INST\",\"ComputerName\":\"SQL01\"}]";
                        var parsed = JsonPipeline.ParsePipelineJson(json);
                        TestAssert.Equal(1, parsed.Count, "One pipeline object");
                        TestAssert.Equal("SQL01\\INST", parsed[0].Instance, "Instance value");
                        TestAssert.Equal("SQL01", parsed[0].ComputerName, "ComputerName value");
                    }
                },
                new TestCase
                {
                    Name = "JsonPipeline serializes result properties",
                    Body = () =>
                    {
                        var json = JsonPipeline.SerializeResults(new[]
                        {
                            new SampleResult { ComputerName = "SQL01", Instance = "SQL01" }
                        });
                        TestAssert.Contains(json, "\"ComputerName\":\"SQL01\"", "Serialized ComputerName");
                        TestAssert.Contains(json, "\"Instance\":\"SQL01\"", "Serialized Instance");
                    }
                }
            };
        }

        private sealed class SampleResult
        {
            public string ComputerName { get; set; }
            public string Instance { get; set; }
        }
    }
}
