using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AsyncRewriter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Tests
{
    class Tests
    {
        [Test, TestCaseSource(typeof(AsyncCodeTestCaseSource))]
        public void AllTests(string inPath, string expectedPath)
        {
            var rewriter = new Rewriter(new ConsoleLoggingAdapter(LogLevel.Debug));
            var actualPath = rewriter.Rewrite(inPath)[0];
            try
            {
                var actual = SyntaxFactory.SyntaxTree(SyntaxFactory.ParseCompilationUnit(File.ReadAllText(actualPath)).NormalizeWhitespace()).ToString();
                var expected = SyntaxFactory.SyntaxTree(SyntaxFactory.ParseCompilationUnit(File.ReadAllText(expectedPath)).NormalizeWhitespace()).ToString();
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
