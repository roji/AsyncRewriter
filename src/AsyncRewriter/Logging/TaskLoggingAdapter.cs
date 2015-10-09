#if NET452
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AsyncRewriter.Logging
{
    public class TaskLoggingAdapter : ILogger
    {
        readonly TaskLoggingHelper _underlying;

        public TaskLoggingAdapter(TaskLoggingHelper underlying)
        {
            _underlying = underlying;
        }

        public void Info(string message)
        {
            _underlying.LogMessage(message);
        }

        public void Info(string message, params object[] args)
        {
            _underlying.LogMessage(message, args);
        }

        public void Debug(string message)
        {
            _underlying.LogMessage(MessageImportance.Low, message);
        }

        public void Debug(string message, params object[] args)
        {
            _underlying.LogMessage(MessageImportance.Low, message, args);
        }
    }
}
#endif