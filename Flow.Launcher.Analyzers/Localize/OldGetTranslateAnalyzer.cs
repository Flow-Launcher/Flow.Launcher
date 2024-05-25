using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Flow.Launcher.Analyzers.Localize;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OldGetTranslateAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AnalyzerDiagnostics.OldLocalizationApiUsed);

    private static readonly string[] oldLocalizationClasses = { "IPublicAPI", "Internationalization" };
    private const string OldLocalizationMethodName = "GetTranslation";

    private const string StringFormatMethodName = "Format";
    private const string StringFormatTypeName = "string";

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocationExpr);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol) return;

        if (IsFormatStringCall(methodSymbol) &&
            GetFirstArgumentInvocationExpression(invocationExpr) is {} innerInvocationExpr)
        {
            if (!IsTranslateCall(semanticModel.GetSymbolInfo(innerInvocationExpr)) ||
                GetFirstArgumentStringValue(innerInvocationExpr) is not {} translationKey)
                return;

            var diagnostic = Diagnostic.Create(
                AnalyzerDiagnostics.OldLocalizationApiUsed,
                invocationExpr.GetLocation(),
                translationKey,
                GetInvocationArguments(invocationExpr)
            );
            context.ReportDiagnostic(diagnostic);
        }
        else if (IsTranslateCall(methodSymbol) && GetFirstArgumentStringValue(invocationExpr) is {} translationKey)
        {
            if (IsParentFormatStringCall(semanticModel, invocationExpr)) return;

            var diagnostic = Diagnostic.Create(
                AnalyzerDiagnostics.OldLocalizationApiUsed,
                invocationExpr.GetLocation(),
                translationKey,
                string.Empty
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string GetInvocationArguments(InvocationExpressionSyntax invocationExpr) =>
        string.Join(", ", invocationExpr.ArgumentList.Arguments.Skip(1));

    private static bool IsParentFormatStringCall(SemanticModel? semanticModel, SyntaxNode? syntaxNode) =>
        syntaxNode is InvocationExpressionSyntax { Parent.Parent.Parent: {} parent } &&
        IsFormatStringCall(semanticModel?.GetSymbolInfo(parent));

    private static bool IsFormatStringCall(SymbolInfo? symbolInfo) =>
        symbolInfo is { Symbol: IMethodSymbol methodSymbol } && IsFormatStringCall(methodSymbol);

    private static bool IsFormatStringCall(IMethodSymbol? methodSymbol) =>
        methodSymbol is { Name: StringFormatMethodName } &&
        methodSymbol.ContainingType.ToDisplayString() is StringFormatTypeName;

    private static InvocationExpressionSyntax? GetFirstArgumentInvocationExpression(InvocationExpressionSyntax invocationExpr) =>
        invocationExpr.ArgumentList.Arguments.FirstOrDefault()?.Expression as InvocationExpressionSyntax;

    private static bool IsTranslateCall(SymbolInfo symbolInfo) =>
        symbolInfo.Symbol is IMethodSymbol { Name: OldLocalizationMethodName } innerMethodSymbol &&
        oldLocalizationClasses.Contains(innerMethodSymbol.ContainingType.Name);

    private static bool IsTranslateCall(IMethodSymbol methodSymbol) =>
        methodSymbol is { Name: OldLocalizationMethodName } &&
        oldLocalizationClasses.Contains(methodSymbol.ContainingType.Name);

    private static string? GetFirstArgumentStringValue(InvocationExpressionSyntax invocationExpr) =>
        invocationExpr.ArgumentList.Arguments.FirstOrDefault()?.Expression switch
        {
            LiteralExpressionSyntax syntax => syntax.Token.ValueText,
            _ => null
        };
}
