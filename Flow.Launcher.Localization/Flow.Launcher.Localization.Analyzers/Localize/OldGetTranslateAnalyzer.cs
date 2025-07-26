using System.Collections.Immutable;
using System.Linq;
using Flow.Launcher.Localization.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Flow.Launcher.Localization.Analyzers.Localize
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OldGetTranslateAnalyzer : DiagnosticAnalyzer
    {
        #region DiagnosticAnalyzer

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            AnalyzerDiagnostics.OldLocalizationApiUsed
        );

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        #endregion

        #region Analyze Methods

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var symbolInfo = semanticModel.GetSymbolInfo(invocationExpr);

            // Check if the method is a format string call
            if (!(symbolInfo.Symbol is IMethodSymbol methodSymbol)) return;

            // First branch: detect a call to string.Format containing a translate call anywhere in its arguments.
            if (IsFormatStringCall(methodSymbol))
            {
                var arguments = invocationExpr.ArgumentList.Arguments;
                // Check all arguments is an invocation (i.e. a candidate for Context.API.GetTranslation(…))
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (GetArgumentInvocationExpression(invocationExpr, i) is InvocationExpressionSyntax innerInvocationExpr &&
                        IsTranslateCall(semanticModel.GetSymbolInfo(innerInvocationExpr)) &&
                        GetFirstArgumentStringValue(innerInvocationExpr) is string translationKey)
                    {
                        var diagnostic = Diagnostic.Create(
                            AnalyzerDiagnostics.OldLocalizationApiUsed,
                            invocationExpr.GetLocation(),
                            translationKey,
                            GetInvocationArguments(invocationExpr, i)
                        );
                        context.ReportDiagnostic(diagnostic);
                        return;
                    }
                }
            }
            // Second branch: direct translate call (outside of a Format call)
            else if (IsTranslateCall(methodSymbol) && GetFirstArgumentStringValue(invocationExpr) is string translationKey)
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

        #region Utils

        private static string GetInvocationArguments(InvocationExpressionSyntax invocationExpr, int translateArgIndex) =>
            string.Join(", ", invocationExpr.ArgumentList.Arguments.Skip(translateArgIndex + 1));

        /// <summary>
        /// Walk up the tree to see if we're already inside a Format call
        /// </summary>
        private static bool IsParentFormatStringCall(SemanticModel semanticModel, SyntaxNode syntaxNode)
        {
            var parent = syntaxNode.Parent;
            while (parent != null)
            {
                if (parent is InvocationExpressionSyntax parentInvocation)
                {
                    var symbol = semanticModel.GetSymbolInfo(parentInvocation).Symbol as IMethodSymbol;
                    if (IsFormatStringCall(symbol))
                    {
                        return true;
                    }
                }
                parent = parent.Parent;
            }
            return false;
        }

        private static bool IsFormatStringCall(IMethodSymbol methodSymbol) =>
            methodSymbol?.Name == Constants.StringFormatMethodName &&
            methodSymbol.ContainingType.ToDisplayString() == Constants.StringFormatTypeName;

        private static InvocationExpressionSyntax GetArgumentInvocationExpression(InvocationExpressionSyntax invocationExpr, int index) =>
            invocationExpr.ArgumentList.Arguments[index].Expression as InvocationExpressionSyntax;

        private static bool IsTranslateCall(SymbolInfo symbolInfo) =>
            symbolInfo.Symbol is IMethodSymbol innerMethodSymbol &&
            innerMethodSymbol.Name == Constants.OldLocalizationMethodName &&
            Constants.OldLocalizationClasses.Contains(innerMethodSymbol.ContainingType.Name);

        private static bool IsTranslateCall(IMethodSymbol methodSymbol) =>
            methodSymbol?.Name is Constants.OldLocalizationMethodName &&
            Constants.OldLocalizationClasses.Contains(methodSymbol.ContainingType.Name);

        private static string GetFirstArgumentStringValue(InvocationExpressionSyntax invocationExpr)
        {
            if (invocationExpr.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax syntax)
                return syntax.Token.ValueText;
            return null;
        }

        #endregion

        #endregion
    }
}
