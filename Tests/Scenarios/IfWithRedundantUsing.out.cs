#pragma warning disable
using System;
using System.Collections.Generic;
#if !FOO
using System.Linq;
#endif
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Scenarios
{
    class IfWithUsing
    {
        public async Task<int> FooAsync()
        {
            return 8;
        }
    }
}