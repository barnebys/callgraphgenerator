namespace HelloWorld;

public class Foo : IFoo
{
    private readonly IBar _bar;

    public Foo(IBar bar)
    {
        _bar = bar;
    }
    public void DoStuff()
    {
        Console.WriteLine("Foo::DoStuff()");
        _bar.DoStuff();
        DoOtherStuff();
    }

    public void DoOtherStuff()
    {
        DoStuff();
    }
}