using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AsyncRewriter;

namespace Tests.Scenarios
{
    abstract class Parent
    {
        public abstract int Foo();
    }

    class Simple : Parent
    {
        [RewriteAsync]
        public override int Foo()
        {
            return 8;
        }
    }
}
