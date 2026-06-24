using System;
using System.Collections.Generic;
using System.IO;

namespace SharpUpSQL.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var exe = args.Length > 0
                ? args[0]
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SharpUpSQL.exe");

            var tests = new List<TestCase>();
            tests.AddRange(CoreHelperTests.Cases());
            tests.AddRange(CommandRegistryTests.Cases(exe));

            return TestRunner.Run(tests);
        }
    }
}
