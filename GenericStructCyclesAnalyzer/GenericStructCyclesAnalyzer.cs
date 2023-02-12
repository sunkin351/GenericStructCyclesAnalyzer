using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GenericStructCyclesAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class GenericStructCyclesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _descriptor = new(
        "GSC0",
        "Generic Struct Cycle Dependency",
        "Struct Member '{0}' of Type '{1}' causes a cycle in the struct layout (according to the runtime)",
        "",
        DiagnosticSeverity.Error,
        true,
        "Catches a few cases where the compiler doesn't error on a field or auto-property declaration that would cause a TypeLoadException at runtime.",
        null,
        WellKnownDiagnosticTags.NotConfigurable
    );

    private static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiags = ImmutableArray.Create(_descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(SymbolAction, SymbolKind.Field);
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => _supportedDiags;

    private static void SymbolAction(SymbolAnalysisContext ctx)
    {
        var field = (IFieldSymbol)ctx.Symbol;

        var parentType = field.ContainingType;

        if (field.IsStatic || !parentType.IsValueType)
        {
            return;
        }

        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        
        if (IsOrContainsGenericStructCycle(parentType, field.Type, set, ctx.CancellationToken))
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(
                    _descriptor,
                    field.Locations[0],
                    (field.AssociatedSymbol ?? field).ToDisplayString(),
                    field.Type.ToDisplayString()
                )
            );
        }
    }

    private static bool IsOrContainsGenericStructCycle(
        INamedTypeSymbol targetType,
        ITypeSymbol checkedType,
        HashSet<ITypeSymbol> set,
        CancellationToken cancel)
    {
        if (!checkedType.IsValueType)
            return false;

        var originalDef = checkedType.OriginalDefinition;

        if (SymbolEqualityComparer.Default.Equals(targetType, originalDef))
            return true;
        
        if (checkedType is INamedTypeSymbol namedCheckedType)
        {
            foreach (var type in namedCheckedType.TypeArguments)
            {
                cancel.ThrowIfCancellationRequested();

                if (IsOrContainsGenericStructCycle(targetType, type, set, cancel))
                {
                    return true;
                }
            }
        }

        if (set.Add(originalDef))
        {
            foreach (var field in originalDef.GetMembers().OfType<IFieldSymbol>())
            {
                cancel.ThrowIfCancellationRequested();

                if (!field.IsStatic && IsOrContainsGenericStructCycle(targetType, field.Type, set, cancel))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
