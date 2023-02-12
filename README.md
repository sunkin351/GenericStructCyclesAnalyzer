# Generic Struct Cycles Analyzer
This analyzer catches a few cases where the compiler doesn't error on a field or auto-property declaration that would cause a TypeLoadException at runtime. PR's are welcome!

An example of an issue this catches:
```cs
struct S0<T>
{
}

struct S1
{
    public S0<S1> Field;
}
```
Despite the fact that `S0<T>` doesn't actually have any instance fields of `T`,
the runtime still throws a `TypeLoadException` loading `S1` as it thinks it's a struct layout cycle,
and currently Microsoft's compiler doesn't report this issue at all.
So, to help this issue, this analyzer reports these and similar circumstances involving generic structs.
