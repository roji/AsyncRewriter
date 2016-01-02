#pragma warning disable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Scenarios
{
    class Simple
    {
        public async Task<int> FooAsync(CancellationToken cancellationToken)
        {
            return await BarAsync<int>(cancellationToken);
        }

        public async Task<int> BarAsync<T>(CancellationToken cancellationToken)
        {
            return 8;
        }
    }
}