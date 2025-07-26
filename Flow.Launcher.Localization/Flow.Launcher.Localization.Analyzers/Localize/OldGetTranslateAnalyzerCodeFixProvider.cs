using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Localization.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Flow.Launcher.Localization.Analyzers.Localize
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OldGetTranslateAnalyzerCodeFixProvider)), Shared]
    public class OldGetTranslateAnalyzerCodeFixProvider : CodeFixProvider
    {
        #region CodeFixProvider

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            AnalyzerDiagnostics.OldLocalizationApiUsed.Id
        );

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Replace with '{Constants.ClassName}.localization_key(...args)'",
                    createChangedDocument: _ => Task.FromResult(FixOldTranslation(context, root, diagnostic)),
                    equivalenceKey: AnalyzerDiagnostics.OldLocalizationApiUsed.Id
                ),
                diagnostic
            );
        }

        #endregion

        #region Fix Methods

        private static Document FixOldTranslation(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            if (root is null) return context.Document;

            var invocationExpr = root
                .FindToken(diagnosticSpan.Start).Parent
                .AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();

            if (invocationExpr is null) return context.Document;

            var argumentList = invocationExpr.ArgumentList.Arguments;

            // Loop through the arguments to find the translation key.
            for (int i = 0; i < argumentList.Count; i++)
            {
                var argument = argumentList[i].Expression;

                // Case 1: The argument is a literal (direct GetTranslation("key"))
                if (GetTranslationKey(argument) is string translationKey)
                    return FixOldTranslationWithoutStringFormat(context, translationKey, root, invocationExpr);

                // Case 2: The argument is itself an invocation (nested GetTranslation)
                if (GetTranslationKeyFromInnerInvocation(argument) is string translationKeyInside)
                {
                    // If there are arguments following this translation call, treat as a Format call.
                    if (i < argumentList.Count - 1)
                        return FixOldTranslationWithStringFormat(context, argumentList, translationKeyInside, root, invocationExpr, i);

                    // Otherwise, treat it as a direct translation call.
                    return FixOldTranslationWithoutStringFormat(context, translationKeyInside, root, invocationExpr);
                }
            }

            return context.Document;
        }

        #region Utils

        private static string GetTranslationKey(ExpressionSyntax syntax)
        {
            if (syntax is LiteralExpressionSyntax literalExpressionSyntax &&
                literalExpressionSyntax.Token.Value is string translationKey)
                return translationKey;
            return null;
        }

        private static Document FixOldTranslationWithoutStringFormat(
            CodeFixContext context, string translationKey, SyntaxNode root, InvocationExpressionSyntax invocationExpr)
        {
            var newInvocationExpr = SyntaxFactory.ParseExpression(
                $"{Constants.ClassName}.{translationKey}()"
            );

            var newRoot = root.ReplaceNode(invocationExpr, newInvocationExpr);
            var newDocument = context.Document.WithSyntaxRoot(newRoot);
            return newDocument;
        }

        private static string GetTranslationKeyFromInnerInvocation(ExpressionSyntax syntax)
        {
            if (syntax is InvocationExpressionSyntax invocationExpressionSyntax &&
                invocationExpressionSyntax.ArgumentList.Arguments.Count == 1)
            {
                var firstArgument = invocationExpressionSyntax.ArgumentList.Arguments.First().Expression;
                return GetTranslationKey(firstArgument);
            }
            return null;
        }

        private static Document FixOldTranslationWithStringFormat(
            CodeFixContext context,
            SeparatedSyntaxList<ArgumentSyntax> argumentList,
            string translationKey2,
            SyntaxNode root,
            InvocationExpressionSyntax invocationExpr,
            int translationArgIndex)
        {
            // Skip all arguments before and including the translation call
            var newArguments = string.Join(", ", argumentList.Skip(translationArgIndex + 1).Select(a => a.Expression));
            var newInnerInvocationExpr = SyntaxFactory.ParseExpression($"{Constants.ClassName}.{translationKey2}({newArguments})");

            var newRoot = root.ReplaceNode(invocationExpr, newInnerInvocationExpr);
            return context.Document.WithSyntaxRoot(newRoot);
        }

        #endregion

        #endregion
    }
}
