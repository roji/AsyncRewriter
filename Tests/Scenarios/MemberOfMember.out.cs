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
        public async Task<int> FooAsync()
        {
            var c = new Container();
            return await c.Inner.BarAsync();
        }
    }

    class Inner
    {
        public async Task<int> BarAsync()
        {
            return 8;
        }
    }
}
