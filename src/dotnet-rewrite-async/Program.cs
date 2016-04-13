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
            var matcher = new Matcher();
            matcher.AddInclude(@"**\*.cs");
            matcher.AddExclude(OutputFileName);

            var inputFiles = matcher.GetResultsInFullPath(".").ToArray();
            Console.WriteLine("Rewriting async methods");
            var asyncCode = new Rewriter().RewriteAndMerge(inputFiles);
            File.WriteAllText(OutputFileName, asyncCode);
                /*
            var inputFiles = new List<string>();
            var latestChange = DateTime.MinValue;
            foreach (var f in matcher.GetResultsInFullPath("."))
            {
                inputFiles.Add(f);
                var change = File.GetLastWriteTimeUtc(f);
                if (change > latestChange)
                    latestChange = change;
            }

            if (!File.Exists(OutputFileName) || latestChange > File.GetLastWriteTimeUtc(OutputFileName))
            {
                Console.WriteLine("Rewriting async methods");
                var asyncCode = new Rewriter().RewriteAndMerge(inputFiles.ToArray());
                File.WriteAllText(OutputFileName, asyncCode);
            }
            else
            {
                Console.WriteLine("Skipping async rewriting, generated code up to date");
            }
            */
            return 0;
        }
    }
}