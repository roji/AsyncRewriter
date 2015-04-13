using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests.Scenarios
{
    class Simple
    {
        [GenerateAsync]
        public int Foo()
        {
            return 8;
        }
    }
}
