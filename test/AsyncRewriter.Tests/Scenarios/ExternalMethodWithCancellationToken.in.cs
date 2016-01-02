using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class ExternalMethodWithCancellationToken
    {
        [RewriteAsync]
        public int Foo(Stream s)
        {
            var data = new byte[10];
            return s.Read(data, 0, 10);
        }
    }
}
