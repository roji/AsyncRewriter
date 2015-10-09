using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class Simple<T>
    {
        [RewriteAsync]
        public int Foo()
        {
            return 8;
        }
    }
}
