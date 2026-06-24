using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpUpSQL.Tests
{
    internal static class TestAssert
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                var expectedText = expected == null ? "null" : expected.ToString();
                var actualText = actual == null ? "null" : actual.ToString();
                throw new InvalidOperationException(
                    message + " (expected: " + expectedText + ", actual: " + actualText + ")");
            }
        }

        public static void Contains(string haystack, string needle, string message)
        {
            if (haystack == null || haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    internal sealed class TestCase
    {
        public string Name { get; set; }
        public Action Body { get; set; }
    }

    internal static class TestRunner
    {
        public static int Run(IEnumerable<TestCase> tests)
        {
            var passed = 0;
            var failed = 0;

            foreach (var test in tests)
            {
                try
                {
                    test.Body();
                    Console.WriteLine("[PASS] " + test.Name);
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[FAIL] " + test.Name + ": " + ex.Message);
                    failed++;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Unit tests: " + passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
