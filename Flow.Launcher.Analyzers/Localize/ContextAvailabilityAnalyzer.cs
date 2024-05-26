using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Flow.Launcher.Analyzers.Localize
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ContextAvailabilityAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            AnalyzerDiagnostics.ContextIsAField,
            AnalyzerDiagnostics.ContextIsNotStatic,
            AnalyzerDiagnostics.ContextAccessIsTooRestrictive,
            AnalyzerDiagnostics.ContextIsNotDeclared
        );

        private const string PluginContextTypeName = "PluginInitContext";

        private const string PluginInterfaceName = "IPluginI18n";

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            if (!IsPluginEntryClass(classSymbol)) return;

            var contextProperty = classDeclaration.Members.OfType<PropertyDeclarationSyntax>()
                .Select(p => semanticModel.GetDeclaredSymbol(p))
                .FirstOrDefault(p => p?.Type.Name is PluginContextTypeName);

            if (contextProperty != null)
            {
                if (!contextProperty.IsStatic)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDiagnostics.ContextIsNotStatic,
                        contextProperty.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()
                    ));
                    return;
                }

                if (contextProperty.DeclaredAccessibility is Accessibility.Private || contextProperty.DeclaredAccessibility is Accessibility.Protected)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDiagnostics.ContextAccessIsTooRestrictive,
                        contextProperty.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()
                    ));
                    return;
                }

                return;
            }

            var fieldDeclaration = classDeclaration.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Select(f => semanticModel.GetDeclaredSymbol(f))
                .FirstOrDefault(f => f is IFieldSymbol fs && fs.Type.Name is PluginContextTypeName);
            var parentSyntax = fieldDeclaration
                ?.DeclaringSyntaxReferences[0]
                .GetSyntax()
                .FirstAncestorOrSelf<FieldDeclarationSyntax>();

            if (parentSyntax != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AnalyzerDiagnostics.ContextIsAField,
                    parentSyntax.GetLocation()
                ));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDiagnostics.ContextIsNotDeclared,
                classDeclaration.Identifier.GetLocation()
            ));
        }

        private static bool IsPluginEntryClass(INamedTypeSymbol namedTypeSymbol) =>
            namedTypeSymbol?.Interfaces.Any(i => i.Name == PluginInterfaceName) ?? false;
    }
}
