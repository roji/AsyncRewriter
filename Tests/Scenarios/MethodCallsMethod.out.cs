#pragma warning disable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Scenarios
{
    class Simple
    {
        public async Task<int> FooAsync()
        {
            return await BarAsync();
        }

        public async Task<int> BarAsync()
        {
            return 8;
        }
    }
}