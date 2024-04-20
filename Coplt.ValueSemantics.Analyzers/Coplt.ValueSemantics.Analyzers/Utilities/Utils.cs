using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Coplt.ValueSemantics.Analyzers.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coplt.ValueSemantics.Analysis.Utilities;

internal static class Utils
{
    public static void GetUsings(SyntaxNode? node, HashSet<string> usings)
    {
        for (;;)
        {
            if (node == null) break;
            if (node is CompilationUnitSyntax cus)
            {
                foreach (var use in cus.Usings)
                {
                    usings.Add(use.ToString());
                }
                return;
            }
            node = node.Parent;
        }
    }

    public static string GetAccessStr(this Accessibility self) => self switch
    {
        Accessibility.Public => "public",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        _ => "",
    };

    public static NameWrap WrapName(this INamedTypeSymbol symbol)
    {
        var access = symbol.DeclaredAccessibility.GetAccessStr();
        var type_decl = symbol switch
        {
            { IsValueType: true, IsRecord: true, IsReadOnly: false } => "partial record struct",
            { IsValueType: true, IsRecord: true, IsReadOnly: true } => "readonly partial record struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: true, IsRefLikeType: false } => "readonly partial struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: false, IsRefLikeType: true } => "ref partial struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: true, IsRefLikeType: true } =>
                "readonly ref partial struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: false, IsRefLikeType: false } => "partial struct",
            { IsValueType: false, IsRecord: true, IsAbstract: false } => "partial record",
            { IsValueType: false, IsRecord: true, IsAbstract: true } => "abstract partial record",
            { IsValueType: false, IsStatic: true } => "static partial class",
            { IsValueType: false, IsAbstract: true, } => "abstract partial class",
            _ => "partial class",
        };
        var generic = string.Empty;
        if (symbol.IsGenericType)
        {
            var ps = new List<string>();
            foreach (var tp in symbol.TypeParameters)
            {
                var variance = tp.Variance switch
                {
                    VarianceKind.Out => "out ",
                    VarianceKind.In => "in ",
                    _ => "",
                };
                ps.Add($"{variance}{tp.ToDisplayString()}");
            }
            generic = $"<{string.Join(", ", ps)}>";
        }
        return new NameWrap($"{access} {type_decl} {symbol.Name}{generic}");
    }

    public static ImmutableList<NameWrap>? WrapNames(this INamedTypeSymbol symbol,
        ImmutableList<NameWrap>? childs = null)
    {
        NameWrap wrap;
        var parent = symbol.ContainingType;
        if (parent == null)
        {
            var ns = symbol.ContainingNamespace;
            if (ns == null || ns.IsGlobalNamespace) return childs;
            wrap = new NameWrap($"namespace {ns}");
            return childs?.Insert(0, wrap) ?? ImmutableList.Create(wrap);
        }
        wrap = parent.WrapName();
        return WrapNames(parent, childs?.Insert(0, wrap) ?? ImmutableList.Create(wrap));
    }

    public static DiagnosticDescriptor MakeError(int id, LocalizableString msg)
        => new($"VALSEM_E{id:0000}", msg, msg, "", DiagnosticSeverity.Error, true, description: msg);

    public static DiagnosticDescriptor MakeWarning(int id, LocalizableString msg)
        => new($"VALSEM_W{id:0000}", msg, msg, "", DiagnosticSeverity.Warning, true, description: msg);

    public static DiagnosticDescriptor MakeInfo(int id, LocalizableString msg)
        => new($"VALSEM_I{id:0000}", msg, msg, "", DiagnosticSeverity.Info, true, description: msg);

    public static bool IsNotInstGenericType(this ITypeSymbol type) =>
        type is ITypeParameterSymbol
        || (type is INamedTypeSymbol { IsGenericType: true, TypeArguments: var typeArguments }
            && typeArguments.Any(IsNotInstGenericType))
        || (type is IArrayTypeSymbol { ElementType: var e } && e.IsNotInstGenericType())
        || (type is IPointerTypeSymbol { PointedAtType: var p } && p.IsNotInstGenericType());

    public static string ToCodeString(this object? value)
    {
        if (value is string s) return $"\"{s.Replace("\"", "\\\"")}\"";
        return $"{value}";
    }

    public static string ToCodeString(this RefKind v, bool ret_val = false, bool pass_arg = false) => v switch
    {
        RefKind.Ref => "ref",
        RefKind.Out => "out",
        RefKind.In => ret_val ? "ref readonly" : "in",
        (RefKind)4 => pass_arg ? "in" : "ref readonly",
        _ => "",
    };

    public static string ToCodeString(this GenericType v) => v switch
    {
        GenericType.Class => "class",
        GenericType.ClassNull => "class?",
        GenericType.Struct => "struct",
        GenericType.Unmanaged => "unmanaged",
        GenericType.NotNull => "notnull",
        _ => ""
    };

    public static string ToDocType(this string typ) => typ.Replace("<", "{").Replace(">", "}");

    public static PropertyInfo? ScopedKindPropertyInfo = typeof(IParameterSymbol).GetProperty("ScopedKind");

    public static ScopedType Scoped(this IParameterSymbol param) =>
        ScopedKindPropertyInfo == null
            ? ScopedType.None
            : Convert.ToInt32(ScopedKindPropertyInfo.GetValue(param)) switch
            {
                1 => ScopedType.ScopedRef,
                2 => ScopedType.ScopedType,
                _ => ScopedType.None,
            };

    public static bool MethodCompatible(IMethodSymbol a, IMethodSymbol m)
    {
        if (m.IsVararg != a.IsVararg ||
            m.TypeParameters.Length != a.TypeParameters.Length ||
            m.Parameters.Length != a.Parameters.Length)
            return false;
        for (var i = 0; i < m.Parameters.Length; i++)
        {
            var mp = m.Parameters[i];
            var ap = a.Parameters[i];
            if (mp.RefKind != ap.RefKind) return false;
            if (mp.Type.GetType() != ap.Type.GetType()) return false;
            if (SymbolEqualityComparer.Default.Equals(mp.Type, ap.Type)) continue;
            // todo: Determining generics is too complex
            var mds = mp.Type.ToDisplayString();
            var ads = ap.Type.ToDisplayString();
            if (mds == ads) continue;
            return true;
        }
        return true;
    }
}
