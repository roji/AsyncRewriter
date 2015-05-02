using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using AsyncRewriter;

namespace Tests.Scenarios
{
    class Simple
    {
        [RewriteAsync]
        public int Foo()
        {
            var c = new Container();
            return c.Inner.Bar();
        }
    }

    class Container
    {
        public Inner Inner { get; set; }
        public Container() { Inner = new Inner(); }
    }

    class Inner
    {
        [RewriteAsync]
        public int Bar()
        {
            return 8;
        }
    }
}
