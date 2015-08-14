#pragma warning disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Scenarios
{
    class CancellationTokenAtBeginning
    {
        public async Task<int> FooAsync(CancellationToken cancellationToken, int x)
        {
            return 8;
        }
    }
}