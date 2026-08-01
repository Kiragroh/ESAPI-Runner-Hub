using System;
using System.Collections.Generic;
using System.IO;

namespace EsapiRunnerHub.Tests
{
    internal static class TestHarness
    {
        private sealed class TestCase
        {
            public string Name { get; set; }
            public Action Action { get; set; }
        }

        private static readonly List<TestCase> Tests = new List<TestCase>();

        public static void Test(string name, Action action)
        {
            Tests.Add(new TestCase { Name = name, Action = action });
        }

        public static int Run()
        {
            var failures = 0;
            foreach (var test in Tests)
            {
                try
                {
                    test.Action();
                    Console.WriteLine("PASS " + test.Name);
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.WriteLine("FAIL " + test.Name + ": " + exception.Message);
                }
            }

            Console.WriteLine("RESULT " + (Tests.Count - failures) + " passed, " + failures + " failed");
            return failures == 0 ? 0 : 1;
        }

        public static string PathFromRoot(string relativePath)
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Repository root not found.");
        }

        public static void AssertContains(string actual, string expected)
        {
            if (actual == null || !actual.Contains(expected))
            {
                throw new InvalidOperationException("Expected text was not found: " + expected);
            }
        }
    }
}

