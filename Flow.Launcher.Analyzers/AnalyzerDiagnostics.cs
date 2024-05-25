using Microsoft.CodeAnalysis;

namespace Flow.Launcher.Analyzers;

public static class AnalyzerDiagnostics
{
    public static readonly DiagnosticDescriptor OldLocalizationApiUsed = new(
        "FLAN0001",
        "Old localization API used",
        "Use `Localize.{0}({1})` instead",
        "Localization",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ContextIsAField = new(
        "FLAN0002",
        "Plugin context is a field",
        "Plugin context must be a static property instead",
        "Localization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ContextIsNotStatic = new(
        "FLAN0003",
        "Plugin context is not static",
        "Plugin context must be a static property",
        "Localization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ContextAccessIsTooRestrictive = new(
        "FLAN0004",
        "Plugin context property access modifier is too restrictive",
        "Plugin context property must be internal or public",
        "Localization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ContextIsNotDeclared = new(
        "FLAN0005",
        "Plugin context is not declared",
        "Plugin context must be a static property of type `PluginInitContext`",
        "Localization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
