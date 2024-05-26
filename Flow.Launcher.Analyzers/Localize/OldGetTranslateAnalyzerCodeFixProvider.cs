using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Flow.Launcher.Analyzers.Localize
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OldGetTranslateAnalyzerCodeFixProvider)), Shared]
    public class OldGetTranslateAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AnalyzerDiagnostics.OldLocalizationApiUsed.Id);

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
                    title: "Replace with 'Localize.localization_key(...args)'",
                    createChangedDocument: _ => Task.FromResult(FixOldTranslation(context, root, diagnostic)),
                    equivalenceKey: AnalyzerDiagnostics.OldLocalizationApiUsed.Id
                ),
                diagnostic
            );
        }

        private static Document FixOldTranslation(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocationExpr = root
                ?.FindToken(diagnosticSpan.Start).Parent
                ?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .First();

            if (invocationExpr is null || root is null) return context.Document;

            var argumentList = invocationExpr.ArgumentList.Arguments;
            var argument = argumentList.First().Expression;

            if (GetTranslationKey(argument) is string translationKey)
                return FixOldTranslationWithoutStringFormat(context, translationKey, root, invocationExpr);

            if (GetTranslationKeyFromInnerInvocation(argument) is string translationKeyInside)
                return FixOldTranslationWithStringFormat(context, argumentList, translationKeyInside, root, invocationExpr);

            return context.Document;
        }


        private static string GetTranslationKey(ExpressionSyntax syntax)
        {
            if (
                syntax is LiteralExpressionSyntax literalExpressionSyntax &&
                literalExpressionSyntax.Token.Value is string translationKey
            )
                return translationKey;
            return null;
        }

        private static Document FixOldTranslationWithoutStringFormat(
            CodeFixContext context, string translationKey, SyntaxNode root, InvocationExpressionSyntax invocationExpr
        ) {
            var newInvocationExpr = SyntaxFactory.ParseExpression(
                $"Localize.{translationKey}()"
            );

            var newRoot = root.ReplaceNode(invocationExpr, newInvocationExpr);
            var newDocument = context.Document.WithSyntaxRoot(newRoot);
            return newDocument;
        }

        private static string GetTranslationKeyFromInnerInvocation(ExpressionSyntax syntax)
        {
            if (
                syntax is InvocationExpressionSyntax invocationExpressionSyntax &&
                invocationExpressionSyntax.ArgumentList.Arguments.Count is 1
            )
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
            InvocationExpressionSyntax invocationExpr
        ) {
            var newArguments = string.Join(", ", argumentList.Skip(1).Select(a => a.Expression));
            var newInnerInvocationExpr = SyntaxFactory.ParseExpression($"Localize.{translationKey2}({newArguments})");

            var newRoot = root.ReplaceNode(invocationExpr, newInnerInvocationExpr);
            return context.Document.WithSyntaxRoot(newRoot);
        }

    }
}
