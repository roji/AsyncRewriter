using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AsyncGenerator
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// http://stackoverflow.com/questions/2961753/how-to-hide-files-generated-by-custom-tool-in-visual-studio
    /// </remarks>
    public class GenerateAsync : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] InputFiles { get; set; }
        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        readonly Generator _generator;

        public GenerateAsync()
        {
            _generator = new Generator();
        }

        public override bool Execute()
        {
            var outputPaths = _generator.Generate(InputFiles.Select(f => f.ItemSpec));
            OutputFiles = outputPaths.Select(p => new TaskItem(p)).ToArray();
            return true;
        }
    }
}
