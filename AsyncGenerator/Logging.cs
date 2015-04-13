using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AsyncGenerator
{
    public interface ILogger
    {
        void Info(string message);
        void Info(string message, params object[] args);
        void Debug(string message);
        void Debug(string message, params object[] args);
    }

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

    public class ConsoleLoggingAdapter : ILogger
    {
        readonly LogLevel _level;

        public ConsoleLoggingAdapter(LogLevel level=LogLevel.Info)
        {
            _level = level;
        }

        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        public void Info(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Debug(string message)
        {
            if (_level == LogLevel.Debug)
                Console.WriteLine(message);
        }

        public void Debug(string message, params object[] args)
        {
            if (_level == LogLevel.Debug)
                Console.WriteLine(message, args);
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
    }
}
