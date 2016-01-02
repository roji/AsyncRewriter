using System;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class Simple
    {
        [RewriteAsync]
        public int Foo()
        {
            return Bar<int>();
        }

        [RewriteAsync]
        public int Bar<T>()
        {
            return 8;
        }
    }
}
