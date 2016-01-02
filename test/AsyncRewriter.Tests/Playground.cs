using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncRewriter;
using NUnit.Framework;

namespace Tests
{
    class Playground
    {
        [Test, Explicit]
        public void Npgsql()
        {
            const string npgsqlPath = @"C:\projects\npgsql\src\Npgsql";

            var rewriter = new Rewriter(new ConsoleLoggingAdapter(LogLevel.Debug));
            var generatedCode = rewriter.RewriteAndMerge(Directory.EnumerateFiles(npgsqlPath, "*.cs", SearchOption.AllDirectories).ToArray());
            Console.WriteLine(generatedCode);
        }
    }
}
