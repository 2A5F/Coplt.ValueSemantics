using System.Collections.Immutable;
using System.Linq;
using Coplt.ValueSemantics.Analysis.Utilities;
using Coplt.ValueSemantics.Analyzers.Utilities;
using Microsoft.CodeAnalysis;

namespace Coplt.ValueSemantics.Analysis.Generators.Templates;

public record struct ValueInheritBase(
    string Type,
    string Name,
    bool CanReadonly,
    RefKind RefKind,
    bool IsStruct
);

public record struct ValueInheritField(
    string Type,
    string Name,
    Accessibility Accessibility,
    bool ReadOnly,
    bool Static
);

public record struct ValueInheritProperty(
    string Type,
    string Name,
    Accessibility Accessibility,
    RefKind Ref,
    bool Get,
    bool Set,
    bool ReadOnly,
    bool Index,
    ImmutableArray<ValueInheritArgs> Args,
    bool Static
);

public record struct ValueInheritMethods(
    string RetType,
    RefKind RetRef,
    string Name,
    Accessibility Accessibility,
    bool ReadOnly,
    ImmutableArray<ValueInheritArgs> Args,
    ImmutableArray<ValueInheritTypeArgs> TypeArgs,
    bool Static
);

public record struct ValueInheritArgs(
    string Type,
    string Name,
    RefKind RefKind,
    ScopedType scoped,
    string? Default,
    bool Params,
    NullableAnnotation Null);

public record struct ValueInheritTypeArgs(
    string Type,
    string Name,
    GenericType GenericType,
    bool HasCtor,
    bool HasNotNull,
    ImmutableArray<string> BaseTypes)
{
    public bool HasWhere => GenericType is not GenericType.None || HasCtor || HasNotNull || BaseTypes.Length > 0;
}

public class ValueInheritTemplate(
    GenBase GenBase,
    string Name,
    int LangVersion,
    bool IsStruct,
    ValueInheritBase Base,
    ImmutableArray<ValueInheritField> Fields,
    ImmutableArray<ValueInheritProperty> Properties,
    ImmutableArray<ValueInheritMethods> Methods
) : ATemplate(GenBase)
{
    public const string Forward = "[global::Coplt.ValueSemantics.Metas.Forward]";

    protected override void DoGen()
    {
        sb.AppendLine(GenBase.Target.Code);
        sb.AppendLine("{");

        #region Field Forwards

        if (Fields.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    #region Field Forwards");

            foreach (var field in Fields)
            {
                var sta = field.Static ? " static" : "";
                var sro = field.ReadOnly && IsStruct && Base.CanReadonly ? " readonly" : "";
                var ro = field.ReadOnly || Base.RefKind is RefKind.RefReadOnly ? " readonly" : "";
                var r_rev = Base.RefKind is not RefKind.None ? $" ref{ro}" : "";
                var v_rev = Base.RefKind is not RefKind.None ? $"ref " : "";
                var acc = field.Accessibility is Accessibility.Internal ? "internal" : "public";
                var src = field.Static ? Base.Type : Base.Name;

                sb.AppendLine();
                sb.AppendLine($"    /// <inheritdoc cref=\"{Base.Type.ToDocType()}.{field.Name}\"/>");
                sb.AppendLine($"    {Forward}");
                if (!field.Static && IsStruct && Base.IsStruct)
                    sb.AppendLine($"    {UnscopedRef}");
                sb.AppendLine($"    {acc}{sta}{sro}{r_rev} {field.Type} {field.Name}");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        {AggressiveInlining}");
                sb.AppendLine($"        get => {v_rev}{src}.{field.Name};");
                sb.AppendLine($"    }}");
            }

            sb.AppendLine();
            sb.AppendLine($"    #endregion // Field Forwards");
        }

        #endregion

        #region Property Forwards

        if (Properties.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    #region Property Forwards");

            foreach (var property in Properties)
            {
                var sta = property.Static ? " static" : "";
                var sro = property.ReadOnly && IsStruct && Base.CanReadonly ? " readonly" : "";
                var acc = property.Accessibility is Accessibility.Internal ? "internal" : "public";
                var src = property.Static ? Base.Type : Base.Name;
                var ret_ref = property.Ref is not RefKind.None ? $"{property.Ref.ToCodeString(ret_val: true)} " : "";
                var rv_ref = property.Ref is not RefKind.None ? "ref " : "";

                sb.AppendLine();

                #region Doc

                if (!property.Index)
                {
                    sb.AppendLine($"    /// <inheritdoc cref=\"{Base.Type.ToDocType()}.{property.Name}\"/>");
                }
                else
                {
                    sb.Append($"    /// <inheritdoc cref=\"{Base.Type.ToDocType()}");
                    sb.Append($"[");
                    if (property.Args.Length > 0)
                    {
                        GenDocArgs(property.Args);
                    }
                    sb.Append($"]");
                    sb.AppendLine($"\"/>");
                }

                #endregion

                sb.AppendLine($"    {Forward}");
                if (!property.Static && IsStruct && Base.IsStruct)
                    sb.AppendLine($"    {UnscopedRef}");

                if (!property.Index)
                {
                    sb.AppendLine($"    {acc}{sta}{sro} {ret_ref}{property.Type} {property.Name}");
                    sb.AppendLine($"    {{");
                    if (property.Get)
                    {
                        sb.AppendLine($"        {AggressiveInlining}");
                        sb.AppendLine($"        get => {rv_ref}{src}.{property.Name};");
                    }
                    if (property.Set && (Base.IsStruct, Base.RefKind) is not (true, RefKind.None))
                    {
                        sb.AppendLine($"        {AggressiveInlining}");
                        sb.AppendLine($"        set => {src}.{property.Name} = value;");
                    }
                    sb.AppendLine($"    }}");
                }
                else
                {
                    sb.Append($"    {acc}{sta}{sro} {ret_ref}{property.Type} this[");
                    GenDeclArgs(property.Args);
                    sb.AppendLine($"]");
                    sb.AppendLine($"    {{");
                    if (property.Get)
                    {
                        sb.AppendLine($"        {AggressiveInlining}");
                        sb.Append($"        get => {rv_ref}{src}[");
                        GenCallArgs(property.Args);
                        sb.AppendLine($"];");
                    }
                    if (property.Set && (Base.IsStruct, Base.RefKind) is not (true, RefKind.None))
                    {
                        sb.AppendLine($"        {AggressiveInlining}");
                        sb.Append($"        set => {src}[");
                        GenCallArgs(property.Args);
                        sb.AppendLine($"] = value;");
                    }
                    sb.AppendLine($"    }}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"    #endregion // Property Forwards");
        }

        #endregion

        #region Method Forwards

        if (Methods.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"    #region Method Forwards");

            foreach (var method in Methods)
            {
                var sta = method.Static ? " static" : "";
                var acc = method.Accessibility is Accessibility.Internal ? "internal" : "public";
                var src = method.Static ? Base.Type : Base.Name;
                var ret_ref = method.RetRef is not RefKind.None ? $"{method.RetRef.ToCodeString(ret_val: true)} " : "";
                var rv_ref = method.RetRef is not RefKind.None ? $"ref " : "";
                var sro = method.ReadOnly && IsStruct && Base.CanReadonly ? " readonly" : "";

                sb.AppendLine();

                #region Doc

                sb.Append($"    /// <inheritdoc cref=\"{Base.Type.ToDocType()}.{method.Name}");

                #region Doc Generics

                if (method.TypeArgs.Length > 0)
                {
                    sb.Append($"{{");
                    var first_arg = true;
                    foreach (var typeArg in method.TypeArgs)
                    {
                        if (first_arg) first_arg = false;
                        else sb.Append($", ");
                        sb.Append($"{typeArg.Name}");
                    }
                    sb.Append($"}}");
                }

                #endregion

                #region DocArgs

                sb.Append($"(");
                if (method.Args.Length > 0)
                {
                    GenDocArgs(method.Args);
                }
                sb.Append($")");

                #endregion

                sb.AppendLine($"\"/>");

                #endregion

                sb.AppendLine($"    {Forward}");
                if (!method.Static && IsStruct && Base.IsStruct)
                    sb.AppendLine($"    {UnscopedRef}");
                sb.AppendLine($"    {AggressiveInlining}");

                #region Decl

                sb.Append($"    {acc}{sta}{sro} {ret_ref}{method.RetType} {method.Name}");

                #region Generics

                if (method.TypeArgs.Length > 0)
                {
                    sb.Append($"<");
                    var first_arg = true;
                    foreach (var typeArg in method.TypeArgs)
                    {
                        if (first_arg) first_arg = false;
                        else sb.Append($", ");
                        sb.Append($"{typeArg.Name}");
                    }
                    sb.Append($">");
                }

                #endregion

                #region Args

                sb.Append($"(");
                if (method.Args.Length > 0)
                {
                    GenDeclArgs(method.Args);
                }
                sb.AppendLine($")");

                #endregion

                #endregion

                #region Wheres

                foreach (var typeArg in method.TypeArgs)
                {
                    if (!typeArg.HasWhere) continue;
                    var first_arg = true;

                    sb.Append($"        where {typeArg.Name} : ");

                    if (typeArg.GenericType is not GenericType.None)
                    {
                        first_arg = false;
                        sb.Append($"{typeArg.GenericType.ToCodeString()}");
                    }

                    foreach (var baseType in typeArg.BaseTypes)
                    {
                        if (first_arg) first_arg = false;
                        else sb.Append($", ");
                        sb.Append($"{baseType}");
                    }

                    if (typeArg.HasCtor)
                    {
                        sb.Append($", ");
                        sb.Append($"new()");
                    }

                    sb.AppendLine();
                }

                #endregion

                #region Call

                sb.Append($"        => {rv_ref}{src}.{method.Name}");

                #region Call Genrics

                if (method.TypeArgs.Length > 0)
                {
                    sb.Append($"<");
                    var first_arg = true;
                    foreach (var typeArg in method.TypeArgs)
                    {
                        if (first_arg) first_arg = false;
                        else sb.Append($", ");
                        sb.Append($"{typeArg.Name}");
                    }
                    sb.Append($">");
                }

                #endregion

                #region Call Args

                sb.Append($"(");
                if (method.Args.Length > 0)
                {
                    GenCallArgs(method.Args);
                }
                sb.Append($")");

                #endregion

                sb.AppendLine($";");

                #endregion
            }

            sb.AppendLine();
            sb.AppendLine($"    #endregion // Method Forwards");
        }

        #endregion

        sb.AppendLine();
        sb.AppendLine("}");
    }

    private void GenDocArgs(ImmutableArray<ValueInheritArgs> args)
    {
        var first_arg = true;
        foreach (var arg in args)
        {
            if (first_arg) first_arg = false;
            else sb.Append($", ");
            if (arg.RefKind is not RefKind.None) sb.Append($"{arg.RefKind.ToCodeString()} ");
            sb.Append($"{arg.Type.ToDocType()}");
        }
    }

    private void GenDeclArgs(ImmutableArray<ValueInheritArgs> args)
    {
        var first_arg = true;
        foreach (var arg in args)
        {
            if (first_arg) first_arg = false;
            else sb.Append($", ");
            switch (arg)
            {
                case { scoped: ScopedType.ScopedType } or
                    { scoped: ScopedType.ScopedRef, RefKind: not RefKind.Out }:
                    sb.Append($"scoped ");
                    break;
                case { scoped: ScopedType.None, RefKind: RefKind.Out }:
                    sb.Append($"{UnscopedRef} ");
                    break;
            }
            if (arg.RefKind is not RefKind.None) sb.Append($"{arg.RefKind.ToCodeString()} ");
            sb.Append($"{arg.Type} {arg.Name}");
            if (arg.Default != null)
            {
                sb.Append($" = ({arg.Type}){arg.Default}");
            }
        }
    }

    private void GenCallArgs(ImmutableArray<ValueInheritArgs> args)
    {
        var first_arg = true;
        foreach (var arg in args)
        {
            if (first_arg) first_arg = false;
            else sb.Append($", ");
            if (arg.RefKind is not RefKind.None)
                sb.Append($"{arg.RefKind.ToCodeString(pass_arg: true)} ");
            sb.Append($"{arg.Name}");
        }
    }
}
