using System;

namespace Tests.Scenarios
{
    class Simple
    {
        [RewriteAsync]
        public int Foo()
        {
            return Bar();
        }

        [RewriteAsync]
        public int Bar()
        {
            return 8;
        }
    }
}
