using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// This file was injected into your project by the AsyncGenerator package

namespace AsyncGenerator
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class GenerateAsyncAttribute : Attribute
    {
        public GenerateAsyncAttribute(string transformedName = null, bool withOverride = false) { }
    }
}
