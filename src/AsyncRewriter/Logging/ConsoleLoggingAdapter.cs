using System;

namespace AsyncRewriter.Logging
{
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
