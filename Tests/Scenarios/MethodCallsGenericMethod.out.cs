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
            return await BarAsync<int>();
        }

        public async Task<int> BarAsync<T>()
        {
            return 8;
        }
    }
}