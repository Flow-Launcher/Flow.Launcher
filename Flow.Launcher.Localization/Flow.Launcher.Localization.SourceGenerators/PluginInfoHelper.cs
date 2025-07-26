using System.Collections.Immutable;
using System.Linq;
using Flow.Launcher.Localization.Shared;
using Microsoft.CodeAnalysis;

namespace Flow.Launcher.Localization.SourceGenerators
{
    internal class PluginInfoHelper
    {
        public static PluginClassInfo GetValidPluginInfoAndReportDiagnostic(
            ImmutableArray<PluginClassInfo> pluginClasses,
            SourceProductionContext context)
        {
            // If p is null, this class does not implement IPluginI18n
            var iPluginI18nClasses = pluginClasses.Where(p => p != null).ToArray();
            if (iPluginI18nClasses.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SourceGeneratorDiagnostics.CouldNotFindPluginEntryClass,
                    Location.None
                ));
                return null;
            }

            // If p.PropertyName is null, this class does not have PluginInitContext property
            var iPluginI18nClassesWithContext = iPluginI18nClasses.Where(p => p.PropertyName != null).ToArray();
            if (iPluginI18nClassesWithContext.Length == 0)
            {
                foreach (var pluginClass in iPluginI18nClasses)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.CouldNotFindContextProperty,
                        pluginClass.Location,
                        pluginClass.ClassName
                    ));
                }
                return null;
            }

            // Rest classes have implemented IPluginI18n and have PluginInitContext property
            // Check if the property is valid
            foreach (var pluginClass in iPluginI18nClassesWithContext)
            {
                if (pluginClass.IsValid == true)
                {
                    return pluginClass;
                }

                if (!pluginClass.IsStatic)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.ContextPropertyNotStatic,
                        pluginClass.Location,
                        pluginClass.PropertyName
                    ));
                }

                if (pluginClass.IsPrivate)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.ContextPropertyIsPrivate,
                        pluginClass.Location,
                        pluginClass.PropertyName
                    ));
                }

                if (pluginClass.IsProtected)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.ContextPropertyIsProtected,
                        pluginClass.Location,
                        pluginClass.PropertyName
                    ));
                }
            }

            return null;
        }
    }
}
