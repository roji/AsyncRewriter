using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class CancellationTokenAtBeginning
    {
        [RewriteAsync]
        public int Foo(int x)
        {
            return 8;
        }
    }
}
