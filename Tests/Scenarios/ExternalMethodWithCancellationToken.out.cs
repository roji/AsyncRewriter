#pragma warning disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Scenarios
{
    class ExternalMethodWithCancellationToken
    {
        public async Task<int> FooAsync(Stream s, CancellationToken cancellationToken)
        {
            var data = new byte[10];
            return await s.ReadAsync(data, 0, 10, cancellationToken);
        }
    }
}