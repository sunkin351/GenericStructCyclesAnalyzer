namespace TestAssembly;

public struct TestCase
{
    // Both of these cases produce a 
    public S0<TestCase> Field0;
    public S0<S1> Field1;

    public (int, int) Field2;
}

public struct S0<T>
{
}

public struct S1
{
    public TestCase Field;
}

public static class Program
{
    private static void Main()
    {
        var tuple = ("", new TestCase());
    }
}