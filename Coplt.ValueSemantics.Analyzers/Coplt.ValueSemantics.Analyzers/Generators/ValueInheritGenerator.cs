using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Coplt.ValueSemantics.Analysis.Generators.Templates;
using Coplt.ValueSemantics.Analysis.Utilities;
using Coplt.ValueSemantics.Analyzers.Resources;
using Coplt.ValueSemantics.Analyzers.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Coplt.ValueSemantics.Analyzers.Generators;

[Generator]
public class ValueInheritGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sources = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Coplt.ValueSemantics.ValueInheritAttribute",
                (syntax, _) => syntax is StructDeclarationSyntax or ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, _) =>
                {
                    var diagnostics = new List<Diagnostic>();
                    var attr = ctx.Attributes.First();
                    var syntax = (TypeDeclarationSyntax)ctx.TargetNode;
                    var semanticModel = ctx.SemanticModel;
                    var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
                    var rawFullName = symbol.ToDisplayString();
                    var nameWraps = symbol.WrapNames();
                    var nameWrap = symbol.WrapName();

                    var parseOptions = (CSharpParseOptions)ctx.SemanticModel.SyntaxTree.Options;
                    var langVersion = parseOptions.LanguageVersion.GetLangVersion();

                    var isStruct = syntax is StructDeclarationSyntax
                                   || (syntax is RecordDeclarationSyntax rds &&
                                       rds.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword));

                    var theBase = symbol.GetMembers()
                        .Select<ISymbol, (ISymbol? s, ValueInheritBase b, ITypeSymbol t)?>(s =>
                        {
                            if (s is IFieldSymbol fs)
                            {
                                if (!fs.GetAttributes().Any(a =>
                                        a.AttributeClass?.ToDisplayString() ==
                                        "Coplt.ValueSemantics.ValueBaseAttribute"))
                                    return null;
                                return (fs,
                                    new ValueInheritBase(fs.Type.ToDisplayString(), fs.Name, true,
                                        RefKind.None, fs.Type.IsValueType),
                                    fs.Type);
                            }
                            else if (s is IPropertySymbol ps)
                            {
                                if (!ps.GetAttributes().Any(a =>
                                        a.AttributeClass?.ToDisplayString() ==
                                        "Coplt.ValueSemantics.ValueBaseAttribute")) return null;
                                return (ps,
                                    new ValueInheritBase(ps.Type.ToDisplayString(), ps.Name,
                                        ps.GetMethod?.IsReadOnly ?? ps.SetMethod?.IsReadOnly ?? false, RefKind.None,
                                        ps.Type.IsValueType), ps.Type);
                            }
                            return null;
                        })
                        .FirstOrDefault(a => a != null);

                    if (theBase == null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            Utils.MakeError(1, Strings.Generators_ValueInherit_Error_NoBase),
                            attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
                            syntax.Identifier.GetLocation()));

                        theBase = new(null, new("void", "_base", false, RefKind.None, false),
                            semanticModel.Compilation.GetSpecialType(SpecialType.System_Void));
                    }

                    var theBaseValue = theBase.Value;

                    var inheritBase = theBaseValue.b;
                    var inheritBaseType = theBaseValue.t;

                    inheritBase.RefKind = (isStruct, langVersion, theBase.Value.s) switch
                    {
                        (_, _, IPropertySymbol ps) => ps.RefKind,
                        ((false, _, _) or (true, >= 1100, _)) and (_, _, IFieldSymbol fs)
                            => fs.IsRequired ? RefKind.Ref : RefKind.RefReadOnly,
                        _ => RefKind.None,
                    };

                    var named = attr.NamedArguments
                        .ToDictionary(a => a.Key, a => a.Value);

                    var memberNames = new HashSet<string>(symbol.MemberNames);

                    #region ForwardFields

                    var fields = ImmutableArray<ValueInheritField>.Empty;

                    var shouldForwardFields = true;

                    if (named.TryGetValue("ForwardFields", out var _ForwardFields))
                    {
                        if (_ForwardFields.Value is false)
                        {
                            shouldForwardFields = false;
                        }
                    }

                    if (shouldForwardFields)
                    {
                        fields = inheritBaseType.GetMembers()
                            .Where(a => a is IFieldSymbol)
                            .Cast<IFieldSymbol>()
                            .Where(a => a is
                            {
                                DeclaredAccessibility: Accessibility.Public or Accessibility.Internal,
                                CanBeReferencedByName: true,
                                IsImplicitlyDeclared: false,
                            })
                            .Where(a => !memberNames.Contains(a.Name))
                            .Select(a => new ValueInheritField(a.Type.ToDisplayString(), a.Name,
                                a.DeclaredAccessibility, a.IsReadOnly, a.IsStatic))
                            .ToImmutableArray();
                    }

                    #endregion

                    #region ForwardProperties

                    var properties = ImmutableArray<ValueInheritProperty>.Empty;

                    var shouldForwardProperties = true;

                    if (named.TryGetValue("ForwardProperties", out var _ForwardProperties))
                    {
                        if (_ForwardProperties.Value is false)
                        {
                            shouldForwardProperties = false;
                        }
                    }

                    if (shouldForwardProperties)
                    {
                        properties = inheritBaseType.GetMembers()
                            .Where(a => a is IPropertySymbol)
                            .Cast<IPropertySymbol>()
                            .Where(a => a is
                            {
                                DeclaredAccessibility: Accessibility.Public or Accessibility.Internal,

                                IsImplicitlyDeclared: false,
                            } and ({ CanBeReferencedByName: true } or { IsIndexer: true }))
                            .Where(a => !memberNames.Contains(a.Name))
                            .Select(a =>
                            {
                                var args = a.Parameters.Select(p =>
                                    {
                                        var dv = p.HasExplicitDefaultValue
                                            ? p.ExplicitDefaultValue.ToCodeString()
                                            : null;
                                        var scoped = p.Scoped();
                                        return new ValueInheritArgs(p.Type.ToDisplayString(), p.Name, p.RefKind, scoped,
                                            dv, p.IsParams, p.NullableAnnotation);
                                    })
                                    .ToImmutableArray();
                                return new ValueInheritProperty(a.Type.ToDisplayString(), a.Name,
                                    a.DeclaredAccessibility, a.RefKind,
                                    a.GetMethod is
                                        { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal },
                                    a.SetMethod is
                                        { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal },
                                    a.GetMethod?.IsReadOnly ?? a.SetMethod?.IsReadOnly ?? false,
                                    a.IsIndexer, args, a.IsStatic);
                            })
                            .ToImmutableArray();
                    }

                    #endregion

                    #region ForwardMethods

                    var methods = ImmutableArray<ValueInheritMethods>.Empty;

                    var shouldForwardMethods = true;

                    if (named.TryGetValue("ForwardMethods", out var _ForwardMethods))
                    {
                        if (_ForwardMethods.Value is false)
                        {
                            shouldForwardMethods = false;
                        }
                    }

                    if (shouldForwardMethods)
                    {
                        methods = inheritBaseType.GetMembers()
                            .Where(a => a is IMethodSymbol)
                            .Cast<IMethodSymbol>()
                            .Where(a => a is
                            {
                                DeclaredAccessibility: Accessibility.Public or Accessibility.Internal,
                                CanBeReferencedByName: true,
                                IsImplicitlyDeclared: false,
                                MethodKind: MethodKind.Ordinary,
                                IsVararg: false, // todo support vararg
                            })
                            .Where(a =>
                            {
                                if (memberNames.Contains(a.Name))
                                {
                                    if (symbol.GetMembers(a.Name).Where(m => m is IMethodSymbol)
                                        .Cast<IMethodSymbol>()
                                        .Any(m => Utils.MethodCompatible(a, m))) return false;
                                }
                                return true;
                            })
                            .Select(a =>
                            {
                                var args = a.Parameters.Select(p =>
                                    {
                                        var dv = p.HasExplicitDefaultValue
                                            ? p.ExplicitDefaultValue.ToCodeString()
                                            : null;
                                        var scoped = p.Scoped();
                                        return new ValueInheritArgs(p.Type.ToDisplayString(), p.Name, p.RefKind, scoped,
                                            dv, p.IsParams, p.NullableAnnotation);
                                    })
                                    .ToImmutableArray();
                                var typeArgs = a.TypeParameters.Select(p =>
                                {
                                    var gt = p switch
                                    {
                                        { HasUnmanagedTypeConstraint: true } => GenericType.Unmanaged,
                                        {
                                            HasReferenceTypeConstraint: true,
                                            ReferenceTypeConstraintNullableAnnotation: NullableAnnotation.Annotated
                                        } => GenericType.ClassNull,
                                        { HasReferenceTypeConstraint: true } => GenericType.Class,
                                        { HasValueTypeConstraint: true } => GenericType.Struct,
                                        { HasNotNullConstraint: true } => GenericType.NotNull,
                                        _ => GenericType.None,
                                    };
                                    return new ValueInheritTypeArgs(p.ToDisplayString(), p.Name, gt,
                                        p.HasConstructorConstraint, p.HasNotNullConstraint,
                                        p.ConstraintTypes.Select(t => t.ToDisplayString()).ToImmutableArray());
                                }).ToImmutableArray();
                                return new ValueInheritMethods(a.ReturnType.ToDisplayString(), a.RefKind,
                                    a.Name, a.DeclaredAccessibility, a.IsReadOnly, args,
                                    typeArgs, a.IsStatic);
                            })
                            .ToImmutableArray();
                    }

                    #endregion

                    return (
                        syntax, rawFullName, nameWraps, nameWrap,
                        langVersion, isStruct,
                        inheritBase, fields, properties, methods,
                        diagnostics: AlwaysEq.Create(diagnostics)
                    );
                }
            )
            .Select(static (input, _) =>
            {
                var (syntax, rawFullName, nameWraps, nameWrap,
                    langVersion, isStruct,
                    inheritBase, fields, properties, methods,
                    diagnostics) = input;

                var nullable = NullableContextOptions.Enable;
                var usings = new HashSet<string>();
                Utils.GetUsings(syntax, usings);
                var genBase = new GenBase(rawFullName, nullable, usings, nameWraps, nameWrap);

                var name = syntax.Identifier.ToString();

                return (
                    genBase, name,
                    langVersion, isStruct,
                    inheritBase, fields, properties, methods,
                    diagnostics
                );
            });

        context.RegisterSourceOutput(sources, static (ctx, input) =>
        {
            var (genBase, name,
                langVersion, isStruct,
                inheritBase, fields, properties, methods,
                diagnostics) = input;

            if (diagnostics.Value.Count > 0)
            {
                foreach (var diagnostic in diagnostics.Value)
                {
                    ctx.ReportDiagnostic(diagnostic);
                }
            }

            var code = new ValueInheritTemplate(
                genBase, name,
                langVersion, isStruct,
                inheritBase, fields, properties, methods
            ).Gen();
            var sourceText = SourceText.From(code, Encoding.UTF8);
            var rawSourceFileName = genBase.FileFullName;
            var sourceFileName = $"{rawSourceFileName}.ValueInherit.g.cs";
            ctx.AddSource(sourceFileName, sourceText);
        });
    }
}
