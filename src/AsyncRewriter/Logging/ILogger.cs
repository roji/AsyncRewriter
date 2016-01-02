using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncRewriter.Logging
{
    public interface ILogger
    {
        void Info(string message);
        void Info(string message, params object[] args);
        void Debug(string message);
        void Debug(string message, params object[] args);
    }
}
