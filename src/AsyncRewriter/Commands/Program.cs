#if DNX451 || DNXCORE50
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Framework.FileSystemGlobbing;

namespace AsyncRewriter.Commands
{
    public class Program
    {
        readonly string _projectDir;
        const string OutputFileName = "GeneratedAsync.cs";

        public Program()
        {
            // TODO: Is there some way to extract the actual file list from the DNX runtime?
            // This way we'd take excludes/includes, external directories, etc. into account
            _projectDir = PlatformServices.Default.Application.ApplicationBasePath;
        }

        public void Main(string[] args)
        {
#if DNXCORE50
            throw new NotSupportedException("Async rewriter not yet supported on CoreCLR");
#else
            var matcher = new Matcher();
            matcher.AddInclude(@"**\*.cs");
            matcher.AddExclude(OutputFileName);

            var inputFiles = new List<string>();
            var latestChange = DateTime.MinValue;
            foreach (var f in matcher.GetResultsInFullPath(_projectDir))
            {
                inputFiles.Add(f);
                var change = File.GetLastWriteTimeUtc(f);
                if (change > latestChange)
                    latestChange = change;
            }

            var outputFile = Path.Combine(_projectDir, OutputFileName);
            if (!File.Exists(outputFile) || latestChange > File.GetLastWriteTimeUtc(outputFile))
            {
                Console.WriteLine("Rewriting async methods in " + _projectDir);
                var asyncCode = new Rewriter().RewriteAndMerge(inputFiles.ToArray());
                File.WriteAllText(outputFile, asyncCode);
            }
            else
            {
                Console.WriteLine("Skipping async rewriting, generated code up to date");
            }
#endif
        }
    }
}
#endif