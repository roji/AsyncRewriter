using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using AsyncRewriter;

namespace AsyncRewriter.Commands
{
    public class Program
    {
        const string OutputFileName = "GeneratedAsync.cs";

        public static int Main(string[] args)
        {
            var workDir = args.Length > 0 ? args[0] : ".";
            var outputFileNameWithPath = workDir == "." ? OutputFileName : Path.Combine(workDir, OutputFileName);

            var matcher = new Matcher();
            matcher.AddInclude(@"**\*.cs");
            matcher.AddExclude(OutputFileName);

            var inputFiles = matcher.GetResultsInFullPath(workDir).ToArray();
            Console.WriteLine("Rewriting async methods");
            var asyncCode = new Rewriter().RewriteAndMerge(inputFiles);
            File.WriteAllText(outputFileNameWithPath, asyncCode);
            return 0;
        }
    }
}