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
    public class ContextAvailabilityAnalyzer : DiagnosticAnalyzer
    {
        #region DiagnosticAnalyzer

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            AnalyzerDiagnostics.ContextIsAField,
            AnalyzerDiagnostics.ContextIsNotStatic,
            AnalyzerDiagnostics.ContextAccessIsTooRestrictive,
            AnalyzerDiagnostics.ContextIsNotDeclared
        );

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        #endregion

        #region Analyze Methods

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var configOptions = context.Options.AnalyzerConfigOptionsProvider;
            var useDI = configOptions.GetFLLUseDependencyInjection();
            if (useDI)
            {
                // If we use dependency injection, we don't need to check for this context property
                return;
            }

            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var pluginClassInfo = Helper.GetPluginClassInfo(classDeclaration, semanticModel, context.CancellationToken);
            if (pluginClassInfo == null)
            {
                // Cannot find class that implements IPluginI18n
                return;
            }

            // Context property is found, check if it's a valid property
            if (pluginClassInfo.PropertyName != null)
            {
                if (!pluginClassInfo.IsStatic)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDiagnostics.ContextIsNotStatic,
                        pluginClassInfo.CodeFixLocation
                    ));
                    return;
                }

                if (pluginClassInfo.IsPrivate || pluginClassInfo.IsProtected)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDiagnostics.ContextAccessIsTooRestrictive,
                        pluginClassInfo.CodeFixLocation
                    ));
                    return;
                }

                // If the context property is valid, we don't need to check for anything else
                return;
            }

            // Context property is not found, check if it's declared as a field
            var fieldDeclaration = classDeclaration.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Select(f => semanticModel.GetDeclaredSymbol(f))
                .FirstOrDefault(f => f is IFieldSymbol fs && fs.Type.Name is Constants.PluginContextTypeName);
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

            // Context property is not found, report an error
            context.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDiagnostics.ContextIsNotDeclared,
                classDeclaration.Identifier.GetLocation()
            ));
        }

        #endregion
    }
}
