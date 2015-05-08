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
        string asyncHelpersPath;

        [Test, TestCaseSource(typeof(AsyncCodeTestCaseSource))]
        public void AllTests(string inPath, string expectedPath)
        {
            var rewriter = new Rewriter(new ConsoleLoggingAdapter(LogLevel.Debug));
            var actual = rewriter.RewriteAndMerge(new[] { inPath, asyncHelpersPath });

            actual = SyntaxFactory.SyntaxTree(SyntaxFactory.ParseCompilationUnit(actual).NormalizeWhitespace()).ToString();
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

        [TestFixtureSetUp]
        public void Setup()
        {
            // Dump AsyncRewriterHelper.cs from the AsyncRewriter assembly to some temp file
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            asyncHelpersPath = Path.Combine(tempDirectory, "AsyncRewriterHelpers.cs");

            using (var reader = new StreamReader(typeof (Rewriter).Assembly.GetManifestResourceStream(
                string.Format("{0}.{1}", typeof (Rewriter).Namespace, "AsyncRewriterHelpers.cs")
                )))
            {
                File.WriteAllText(asyncHelpersPath, reader.ReadToEnd());
            }
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            var dir = Path.GetDirectoryName(asyncHelpersPath);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
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
