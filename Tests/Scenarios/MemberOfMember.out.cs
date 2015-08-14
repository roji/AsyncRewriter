#pragma warning disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Scenarios
{
    class Simple
    {
        public async Task<int> FooAsync(CancellationToken cancellationToken)
        {
            var c = new Container();
            return await c.Inner.BarAsync(cancellationToken);
        }
    }

    class Inner
    {
        public async Task<int> BarAsync(CancellationToken cancellationToken)
        {
            return 8;
        }
    }
}
