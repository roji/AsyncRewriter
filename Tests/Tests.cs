using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AsyncGenerator;
using NUnit.Framework;

namespace Tests
{
    class Tests
    {
        [Test, TestCaseSource(typeof(AsyncCodeTestCaseSource))]
        public void AllTests(string inPath, string expectedPath)
        {
            var generator = new Generator();
            var actualPath = generator.Generate(inPath)[0];
            try
            {
                var actual   = string.Join("", File.ReadLines(actualPath).Select(l => l.Replace("\r", "")).ToArray());
                var expected = string.Join("", File.ReadLines(expectedPath).Select(l => l.Replace("\r", "")).ToArray());
                if (actual != expected)
                {
                    Console.WriteLine("Actual:");
                    Console.WriteLine(actual);
                    Console.WriteLine("********");
                    Console.WriteLine("Expected:");
                    Console.WriteLine(expected);
                    Console.WriteLine("********");
                }
                Assert.That(actual, Is.EqualTo(expected));
            }
            finally
            {
                File.Delete(actualPath);
            }
        }
    }

    class AsyncCodeTestCaseSource : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            var scenarioPath = "Scenarios";
            foreach (var fullInPath in Directory.EnumerateFiles(scenarioPath, "*.in.cs"))
            {
                var name = Path.GetFileName(fullInPath).Replace(".in.cs", "");
                var fullOutPath = Path.Combine(scenarioPath, name + ".out.cs");
                if (!File.Exists(fullOutPath))
                    throw new Exception("No out file " + fullOutPath);

                yield return new TestCaseData(fullInPath, fullOutPath).SetName(name);
            }
        }
    }
}
