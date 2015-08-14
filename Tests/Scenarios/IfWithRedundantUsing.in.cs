using System;
using System.Collections.Generic;
#if !FOO
using System.Linq;
#endif
using System;
using System.Text;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class IfWithUsing
    {
        [RewriteAsync]
        public int Foo()
        {
            return 8;
        }
    }
}
