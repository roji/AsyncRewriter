using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class Simple
    {
        [RewriteAsync]
        public int Foo()
        {
            var s = new StringWriter();
            s.Write("hello");
        }
    }
}
