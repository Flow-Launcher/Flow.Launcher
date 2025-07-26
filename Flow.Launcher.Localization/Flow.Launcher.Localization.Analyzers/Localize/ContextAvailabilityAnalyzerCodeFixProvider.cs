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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace Flow.Launcher.Localization.Analyzers.Localize
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ContextAvailabilityAnalyzerCodeFixProvider)), Shared]
    public class ContextAvailabilityAnalyzerCodeFixProvider : CodeFixProvider
    {
        #region CodeFixProvider

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            AnalyzerDiagnostics.ContextIsAField.Id,
            AnalyzerDiagnostics.ContextIsNotStatic.Id,
            AnalyzerDiagnostics.ContextAccessIsTooRestrictive.Id,
            AnalyzerDiagnostics.ContextIsNotDeclared.Id
        );

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            if (diagnostic.Id == AnalyzerDiagnostics.ContextIsAField.Id)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace with static property",
                        createChangedDocument: _ => Task.FromResult(FixContextIsAFieldError(context, root, diagnosticSpan)),
                        equivalenceKey: AnalyzerDiagnostics.ContextIsAField.Id
                    ),
                    diagnostic
                );
            }
            else if (diagnostic.Id == AnalyzerDiagnostics.ContextIsNotStatic.Id)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Make static",
                        createChangedDocument: _ => Task.FromResult(FixContextIsNotStaticError(context, root, diagnosticSpan)),
                        equivalenceKey: AnalyzerDiagnostics.ContextIsNotStatic.Id
                    ),
                    diagnostic
                );
            }
            else if (diagnostic.Id == AnalyzerDiagnostics.ContextAccessIsTooRestrictive.Id)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Make internal",
                        createChangedDocument: _ => Task.FromResult(FixContextIsTooRestricted(context, root, diagnosticSpan)),
                        equivalenceKey: AnalyzerDiagnostics.ContextAccessIsTooRestrictive.Id
                    ),
                    diagnostic
                );
            }
            else if (diagnostic.Id == AnalyzerDiagnostics.ContextIsNotDeclared.Id)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Declare context property",
                        createChangedDocument: _ => Task.FromResult(FixContextNotDeclared(context, root, diagnosticSpan)),
                        equivalenceKey: AnalyzerDiagnostics.ContextIsNotDeclared.Id
                    ),
                    diagnostic
                );
            }
        }

        #endregion

        #region Fix Methods

        private static Document FixContextNotDeclared(CodeFixContext context, SyntaxNode root, TextSpan diagnosticSpan)
        {
            var classDeclaration = GetDeclarationSyntax<ClassDeclarationSyntax>(root, diagnosticSpan);
            if (classDeclaration?.BaseList is null) return context.Document;

            var newPropertyDeclaration = GetStaticContextPropertyDeclaration();
            if (newPropertyDeclaration is null) return context.Document;

            var annotatedNewPropertyDeclaration = newPropertyDeclaration
                .WithLeadingTrivia(SyntaxFactory.ElasticLineFeed)
                .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed)
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

            var newMembers = classDeclaration.Members.Insert(0, annotatedNewPropertyDeclaration);
            var newClassDeclaration = classDeclaration.WithMembers(newMembers);

            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            return GetFormattedDocument(context, newRoot);
        }

        private static Document FixContextIsNotStaticError(CodeFixContext context, SyntaxNode root, TextSpan diagnosticSpan)
        {
            var propertyDeclaration = GetDeclarationSyntax<PropertyDeclarationSyntax>(root, diagnosticSpan);
            if (propertyDeclaration is null) return context.Document;

            var newPropertyDeclaration = FixRestrictivePropertyModifiers(propertyDeclaration).AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

            var newRoot = root.ReplaceNode(propertyDeclaration, newPropertyDeclaration);
            return context.Document.WithSyntaxRoot(newRoot);
        }

        private static Document FixContextIsTooRestricted(CodeFixContext context, SyntaxNode root, TextSpan diagnosticSpan)
        {
            var propertyDeclaration = GetDeclarationSyntax<PropertyDeclarationSyntax>(root, diagnosticSpan);
            if (propertyDeclaration is null) return context.Document;

            var newPropertyDeclaration = FixRestrictivePropertyModifiers(propertyDeclaration);

            var newRoot = root.ReplaceNode(propertyDeclaration, newPropertyDeclaration);
            return context.Document.WithSyntaxRoot(newRoot);
        }

        private static Document FixContextIsAFieldError(CodeFixContext context, SyntaxNode root, TextSpan diagnosticSpan) {
            var fieldDeclaration = GetDeclarationSyntax<FieldDeclarationSyntax>(root, diagnosticSpan);
            if (fieldDeclaration is null) return context.Document;

            var field = fieldDeclaration.Declaration.Variables.First();
            var fieldIdentifier = field.Identifier.ToString();

            var propertyDeclaration = GetStaticContextPropertyDeclaration(fieldIdentifier);
            if (propertyDeclaration is null) return context.Document;

            var annotatedNewPropertyDeclaration = propertyDeclaration
                .WithTriviaFrom(fieldDeclaration)
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

            var newRoot = root.ReplaceNode(fieldDeclaration, annotatedNewPropertyDeclaration);

            return GetFormattedDocument(context, newRoot);
        }

        #region Utils

        private static MemberDeclarationSyntax GetStaticContextPropertyDeclaration(string propertyName = "Context") =>
            SyntaxFactory.ParseMemberDeclaration(
                $"internal static {Constants.PluginContextTypeName} {propertyName} {{ get; private set; }} = null!;"
            );

        private static Document GetFormattedDocument(CodeFixContext context, SyntaxNode root)
        {
            var formattedRoot = Formatter.Format(
                root,
                Formatter.Annotation,
                context.Document.Project.Solution.Workspace
            );

            return context.Document.WithSyntaxRoot(formattedRoot);
        }

        private static PropertyDeclarationSyntax FixRestrictivePropertyModifiers(PropertyDeclarationSyntax propertyDeclaration)
        {
            var newModifiers = SyntaxFactory.TokenList();
            foreach (var modifier in propertyDeclaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PrivateKeyword) || modifier.IsKind(SyntaxKind.ProtectedKeyword))
                {
                    newModifiers = newModifiers.Add(SyntaxFactory.Token(SyntaxKind.InternalKeyword));
                }
                else
                {
                    newModifiers = newModifiers.Add(modifier);
                }
            }

            return propertyDeclaration.WithModifiers(newModifiers);
        }

        private static T GetDeclarationSyntax<T>(SyntaxNode root, TextSpan diagnosticSpan) where T : SyntaxNode =>
            root
                .FindToken(diagnosticSpan.Start)
                .Parent
                ?.AncestorsAndSelf()
                .OfType<T>()
                .First();

        #endregion

        #endregion
    }
}
