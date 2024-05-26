using Microsoft.CodeAnalysis;

namespace Flow.Launcher.SourceGenerators
{
    public static class SourceGeneratorDiagnostics
    {
        public static readonly DiagnosticDescriptor CouldNotFindResourceDictionaries = new DiagnosticDescriptor(
            "FLSG0001",
            "Could not find resource dictionaries",
            "Could not find resource dictionaries. There must be a file named [LANG].xaml file (for example, en.xaml), and it must be specified in <AdditionalFiles /> in your .csproj file.",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor CouldNotFindPluginEntryClass = new DiagnosticDescriptor(
            "FLSG0002",
            "Could not find the main class of plugin",
            "Could not find the main class of your plugin. It must implement IPluginI18n.",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor CouldNotFindContextProperty = new DiagnosticDescriptor(
            "FLSG0003",
            "Could not find plugin context property",
            "Could not find a property of type PluginInitContext in {0}. It must be a public static or internal static property of the main class of your plugin.",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextPropertyNotStatic = new DiagnosticDescriptor(
            "FLSG0004",
            "Plugin context property is not static",
            "Context property {0} is not static. It must be static.",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextPropertyIsPrivate = new DiagnosticDescriptor(
            "FLSG0005",
            "Plugin context property is private",
            "Context property {0} is private. It must be either internal or public.",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor ContextPropertyIsProtected = new DiagnosticDescriptor(
            "FLSG0006",
            "Plugin context property is protected",
            "Context property {0} is protected. It must be either internal or public.",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor LocalizationKeyUnused = new DiagnosticDescriptor(
            "FLSG0007",
            "Localization key is unused",
            "Method 'Localize.{0}' is never used",
            "Localization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
    }
}
