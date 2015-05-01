using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AsyncRewriter
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// http://stackoverflow.com/questions/2961753/how-to-hide-files-generated-by-custom-tool-in-visual-studio
    /// </remarks>
    public class RewriteAsync : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] InputFiles { get; set; }
        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        readonly Rewriter _rewriter;

        public RewriteAsync()
        {
            _rewriter = new Rewriter(new TaskLoggingAdapter(Log));
        }

        public override bool Execute()
        {
            var outputPaths = _rewriter.Rewrite(InputFiles.Select(f => f.ItemSpec));
            OutputFiles = outputPaths.Select(p => new TaskItem(p)).ToArray();
            return true;
        }
    }
}
