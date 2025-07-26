using Flow.Launcher.Localization.Shared;
using Microsoft.CodeAnalysis;

namespace Flow.Launcher.Localization.Analyzers
{
    public static class AnalyzerDiagnostics
    {
        public static readonly DiagnosticDescriptor OldLocalizationApiUsed = new DiagnosticDescriptor(
            "FLAN0001",
            "Old localization API used",
            $"Use `{Constants.ClassName}.{{0}}({{1}})` instead",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextIsAField = new DiagnosticDescriptor(
            "FLAN0002",
            "Plugin context is a field",
            "Plugin context must be at least internal static property",
            "Localization",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextIsNotStatic = new DiagnosticDescriptor(
            "FLAN0003",
            "Plugin context is not static",
            "Plugin context must be at least internal static property",
            "Localization",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextAccessIsTooRestrictive = new DiagnosticDescriptor(
            "FLAN0004",
            "Plugin context property access modifier is too restrictive",
            "Plugin context property must be at least internal static property",
            "Localization",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextIsNotDeclared = new DiagnosticDescriptor(
            "FLAN0005",
            "Plugin context is not declared",
            $"Plugin context must be at least internal static property of type `{Constants.PluginContextTypeName}`",
            "Localization",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }
}
