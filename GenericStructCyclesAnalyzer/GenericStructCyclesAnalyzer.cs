using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GenericStructCyclesAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GenericStructCyclesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _descriptor = new(
        "GSC0",
        "Generic Struct Cycle Dependency",
        "The Type declaration of Field or Auto-Property `{0}.{1}` would cause a TypeLoadException at runtime!",
        "",
        DiagnosticSeverity.Error,
        true,
        null,
        """
        Catches a few cases where the compiler doesn't error on a field or auto-property declaration that would cause a TypeLoadException at runtime.
        """,
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
        IFieldSymbol field = (IFieldSymbol)ctx.Symbol;

        var parentType = field.ContainingType;

        if (!parentType.IsValueType)
        {
            return;
        }

        // Compiler already detects this for non-generic types
        if (field.Type is not INamedTypeSymbol { TypeParameters.Length: > 0 } fieldType)
        {
            return;
        }

        if (IsOrContainsGenericStructCycle(parentType, fieldType.TypeArguments, ctx.CancellationToken))
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(
                    _descriptor,
                    field.Locations[0],
                    field.Name,
                    (field.AssociatedSymbol ?? field).Name
                )
            );
        }
    }

    private static bool IsOrContainsGenericStructCycle(
        INamedTypeSymbol targetType,
        ImmutableArray<ITypeSymbol> arguments,
        CancellationToken cancel)
    {
        foreach (var type in arguments)
        {
            cancel.ThrowIfCancellationRequested();

            if (type is ITypeParameterSymbol || !type.IsValueType)
                continue;

            // Probably doesn't cover all cases, advice wanted
            if (SymbolEqualityComparer.Default.Equals(targetType, type.OriginalDefinition)
                || (type is INamedTypeSymbol namedSymbol && IsOrContainsGenericStructCycle(targetType, namedSymbol.TypeArguments, cancel)))
            {
                return true;
            }

            // Compiler does not detect this.
            // This might cover more cases than desired.
            // Advice wanted.
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol field)
                {
                    if (SymbolEqualityComparer.Default.Equals(targetType, field.Type.OriginalDefinition))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
