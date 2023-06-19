using System;
using System.Collections.Generic;
using System.Linq;

namespace HelloWorld
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            IFoo foo = new Foo(new Bar());
            foo.DoStuff();
            new Bar().DoStuff();
        }
    }
}