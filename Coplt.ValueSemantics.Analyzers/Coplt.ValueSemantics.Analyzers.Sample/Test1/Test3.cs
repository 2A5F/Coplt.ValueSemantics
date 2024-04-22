// using System;
// using System.Collections.Generic;
// using System.Diagnostics.CodeAnalysis;
// using System.Runtime.CompilerServices;
//
// namespace Coplt.ValueSemantics.Analyzers.Sample.Test3;
//
// public enum FooEnum
// {
//     A,
//     B,
// }
//
// public record struct Foo<T>()
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
//     public readonly ref readonly int Prop1 => throw new NotImplementedException();
//
//     public int this[int a]
//     {
//         get => throw new NotImplementedException();
//         set => throw new NotImplementedException();
//     }
//
//     public readonly object? Some(FooEnum a = FooEnum.A) => throw new NotImplementedException();
//
//     /// <summary>
//     /// Add
//     /// </summary>
//     public void Some<A, B>(ref int a) => throw new NotImplementedException();
// }
//
// [ValueInherit]
// public partial record struct Bar<T>()
// {
//     [ValueBase]
//     private Foo<T> _base { get; set; } = new();
// }
//
// public static class Test
// {
//     public static int Some1<T>(Bar<T> a) => a.Field1;
// }
