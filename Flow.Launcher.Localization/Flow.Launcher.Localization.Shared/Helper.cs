using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Flow.Launcher.Localization.Shared
{
    public static class Helper
    {
        #region Build Properties

        public static bool GetFLLUseDependencyInjection(this AnalyzerConfigOptionsProvider configOptions)
        {
            if (!configOptions.GlobalOptions.TryGetValue("build_property.FLLUseDependencyInjection", out var result) ||
                !bool.TryParse(result, out var useDI))
            {
                return false; // Default to false
            }
            return useDI;
        }

        #endregion

        #region Plugin Class Info

        /// <summary>
        /// If cannot find the class that implements IPluginI18n, return null.
        /// If cannot find the context property, return PluginClassInfo with null context property name.
        /// </summary>
        public static PluginClassInfo GetPluginClassInfo(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);
            if (!IsPluginEntryClass(classSymbol))
            {
                // Cannot find class that implements IPluginI18n
                return null;
            }

            var property = GetContextProperty(classDecl);
            var location = GetLocation(semanticModel.SyntaxTree, classDecl);
            if (property is null)
            {
                // Cannot find context
                return new PluginClassInfo(location, classDecl.Identifier.Text, null, false, false, false, null);
            }

            var modifiers = property.Modifiers;
            var codeFixLocation = GetCodeFixLocation(property, semanticModel);
            return new PluginClassInfo(
                location,
                classDecl.Identifier.Text,
                property.Identifier.Text,
                modifiers.Any(SyntaxKind.StaticKeyword),
                modifiers.Any(SyntaxKind.PrivateKeyword),
                modifiers.Any(SyntaxKind.ProtectedKeyword),
                codeFixLocation);
        }

        private static bool IsPluginEntryClass(INamedTypeSymbol namedTypeSymbol)
        {
            return namedTypeSymbol?.Interfaces.Any(i => i.Name == Constants.PluginInterfaceName) ?? false;
        }

        private static PropertyDeclarationSyntax GetContextProperty(ClassDeclarationSyntax classDecl)
        {
            return classDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Type.ToString() == Constants.PluginContextTypeName);
        }

        private static Location GetLocation(SyntaxTree syntaxTree, CSharpSyntaxNode classDeclaration)
        {
            return Location.Create(syntaxTree, classDeclaration.GetLocation().SourceSpan);
        }

        private static Location GetCodeFixLocation(PropertyDeclarationSyntax property, SemanticModel semanticModel)
        {
            return semanticModel.GetDeclaredSymbol(property).DeclaringSyntaxReferences[0].GetSyntax().GetLocation();
        }

        #endregion

        #region Tab String

        public static string Spacing(int n)
        {
            return new string(' ', n * 4);
        }

        #endregion
    }
}
