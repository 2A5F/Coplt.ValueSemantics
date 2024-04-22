// using System;
//
// namespace Coplt.ValueSemantics.Analyzers.Sample.Test2;
//
//
// public enum FooEnum
// {
//     A,
//     B,
// }
//
// public record Foo<T>()
// {
//     /// <summary>
//     /// Field1
//     /// </summary>
//     public int Field1 = 0;
//
//     /// <summary>
//     /// Field2
//     /// </summary>
//     public static int Field2 = 0;
//
//     /// <summary>
//     /// Field3
//     /// </summary>
//     public readonly int Field3 = 0;
//
//     public ref readonly int Prop1 => throw new NotImplementedException();
//
//     public int this[int a]
//     {
//         get => throw new NotImplementedException();
//         set => throw new NotImplementedException();
//     }
//
//     public object? Some(FooEnum a = FooEnum.A) => throw new NotImplementedException();
//
//     /// <summary>
//     /// Add
//     /// </summary>
//     public void Some<A, B>(ref int a) => throw new NotImplementedException();
// }
//
// [ValueInherit]
// public partial record Bar<T>()
// {
//     [ValueBase] private Foo<T> _base = new();
// }
//
// public static class Test
// {
//     public static int Some1<T>(Bar<T> a) => a.Field1;
// }
