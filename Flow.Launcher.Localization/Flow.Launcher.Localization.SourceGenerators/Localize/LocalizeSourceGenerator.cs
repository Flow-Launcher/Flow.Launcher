using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Flow.Launcher.Localization.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Flow.Launcher.Localization.SourceGenerators.Localize
{
    /// <summary>
	/// Generates properties for strings based on resource files.
	/// </summary>
    [Generator]
    public partial class LocalizeSourceGenerator : IIncrementalGenerator
    {
        #region Fields

        private static readonly Version PackageVersion = typeof(LocalizeSourceGenerator).Assembly.GetName().Version;

        private static readonly ImmutableArray<LocalizableString> _emptyLocalizableStrings = ImmutableArray<LocalizableString>.Empty;
        private static readonly ImmutableArray<LocalizableStringParam> _emptyLocalizableStringParams = ImmutableArray<LocalizableStringParam>.Empty;

        #endregion

        #region Incremental Generator

        /// <summary>
        /// Initializes the generator and registers source output based on resource files.
        /// </summary>
        /// <param name="context">The initialization context.</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var xamlFiles = context.AdditionalTextsProvider
                .Where(file => Constants.LanguagesXamlRegex.IsMatch(file.Path));

            var localizedStrings = xamlFiles
                .Select((file, ct) => ParseXamlFile(file, ct))
                .Collect()
                .SelectMany((files, _) => files);

            var invocationKeys = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (n, _) => n is InvocationExpressionSyntax,
                    transform: GetLocalizationKeyFromInvocation)
                .Where(key => !string.IsNullOrEmpty(key))
                .Collect()
                .Select((keys, _) => keys.Distinct().ToImmutableHashSet());

            var pluginClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (n, _) => n is ClassDeclarationSyntax,
                    transform: (c, t) => Helper.GetPluginClassInfo((ClassDeclarationSyntax)c.Node, c.SemanticModel, t))
                .Where(info => info != null)
                .Collect();

            var compilation = context.CompilationProvider;

            var configOptions = context.AnalyzerConfigOptionsProvider;
            
            var combined = localizedStrings.Combine(invocationKeys).Combine(pluginClasses).Combine(configOptions).Combine(compilation).Combine(xamlFiles.Collect());

            context.RegisterSourceOutput(combined, Execute);
        }

        /// <summary>
        /// Executes the generation of string properties based on the provided data.
        /// </summary>
        /// <param name="spc">The source production context.</param>
        /// <param name="data">The provided data.</param>
        private void Execute(SourceProductionContext spc, 
            (((((ImmutableArray<LocalizableString> LocalizableStrings, 
            ImmutableHashSet<string> InvocationKeys),
            ImmutableArray<PluginClassInfo> PluginClassInfos),
            AnalyzerConfigOptionsProvider ConfigOptionsProvider),
            Compilation Compilation),
            ImmutableArray<AdditionalText> AdditionalTexts) data)
        {
            var xamlFiles = data.AdditionalTexts;
            if (xamlFiles.Length == 0)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    SourceGeneratorDiagnostics.CouldNotFindResourceDictionaries,
                    Location.None
                ));
                return;
            }

            var compilation = data.Item1.Compilation;
            var configOptions = data.Item1.Item1.ConfigOptionsProvider;
            var pluginClasses = data.Item1.Item1.Item1.PluginClassInfos;
            var usedKeys = data.Item1.Item1.Item1.Item1.InvocationKeys;
            var localizedStrings = data.Item1.Item1.Item1.Item1.LocalizableStrings;

            var assemblyNamespace = compilation.AssemblyName ?? Constants.DefaultNamespace;
            var useDI = configOptions.GetFLLUseDependencyInjection();

            PluginClassInfo pluginInfo;
            if (useDI)
            {
                // If we use dependency injection, we do not need to check if there is a valid plugin context
                pluginInfo = null;
            }
            else
            {
                pluginInfo = PluginInfoHelper.GetValidPluginInfoAndReportDiagnostic(pluginClasses, spc);
                if (pluginInfo == null)
                {
                    // If we cannot find a valid plugin info, we do not need to generate the source
                    return;
                }
            }
            
            GenerateSource(
                spc,
                xamlFiles[0],
                localizedStrings,
                assemblyNamespace,
                useDI,
                pluginInfo,
                usedKeys);
        }

        #endregion

        #region Parse Xaml File

        private static ImmutableArray<LocalizableString> ParseXamlFile(AdditionalText file, CancellationToken ct)
        {
            var content = file.GetText(ct)?.ToString();
            if (content is null)
            {
                return _emptyLocalizableStrings;
            }

            var doc = XDocument.Parse(content);
            var root = doc.Root;
            if (root is null)
            {
                return _emptyLocalizableStrings;
            }

            // Find prefixes for the target URIs
            string systemPrefix = null;
            string xamlPrefix = null;

            foreach (var attr in root.Attributes())
            {
                // Check if the attribute is a namespace declaration (xmlns:...)
                if (attr.Name.NamespaceName == XNamespace.Xmlns.NamespaceName)
                {
                    string uri = attr.Value;
                    string prefix = attr.Name.LocalName;

                    if (uri == Constants.SystemPrefixUri)
                    {
                        systemPrefix = prefix;
                    }
                    else if (uri == Constants.XamlPrefixUri)
                    {
                        xamlPrefix = prefix;
                    }
                }
            }

            if (systemPrefix is null || xamlPrefix is null)
            {
                return _emptyLocalizableStrings;
            }

            var systemNs = doc.Root?.GetNamespaceOfPrefix(systemPrefix);
            var xNs = doc.Root?.GetNamespaceOfPrefix(xamlPrefix);
            if (systemNs is null || xNs is null)
            {
                return _emptyLocalizableStrings;
            }

            var localizableStrings = new List<LocalizableString>();
            foreach (var element in doc.Descendants(systemNs + Constants.XamlTag)) // "String" elements in system namespace
            {
                if (ct.IsCancellationRequested)
                {
                    return _emptyLocalizableStrings;
                }

                var key = element.Attribute(xNs + Constants.KeyAttribute)?.Value; // "Key" attribute in xaml namespace
                var value = element.Value;
                var comment = element.PreviousNode as XComment;

                if (key != null)
                {
                    var formatParams = GetParameters(value);
                    var (summary, updatedFormatParams) = ParseCommentAndUpdateParameters(comment, formatParams);
                    localizableStrings.Add(new LocalizableString(key, value, summary, updatedFormatParams));
                }
            }

            return localizableStrings.ToImmutableArray();
        }

        /// <summary>
        /// Analyzes the format string and returns a list of its parameters.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        private static List<LocalizableStringParam> GetParameters(string format)
        {
            var parameters = new Dictionary<int, string>();
            int maxIndex = -1;
            int i = 0;
            int len = format.Length;

            while (i < len)
            {
                if (format[i] == '{')
                {
                    if (i + 1 < len && format[i + 1] == '{')
                    {
                        // Escaped '{', skip both
                        i += 2;
                        continue;
                    }
                    else
                    {
                        // Start of a format item, parse index and format
                        i++; // Move past '{'
                        int index = 0;
                        bool hasIndex = false;

                        // Parse index
                        while (i < len && char.IsDigit(format[i]))
                        {
                            hasIndex = true;
                            index = index * 10 + (format[i] - '0');
                            i++;
                        }

                        if (!hasIndex)
                        {
                            // Skip invalid format item
                            while (i < len && format[i] != '}')
                            {
                                i++;
                            }
                            if (i < len)
                            {
                                i++; // Move past '}'
                            }
                            continue;
                        }

                        // Check for alignment (comma followed by optional sign and digits)
                        if (i < len && format[i] == ',')
                        {
                            i++; // Skip comma and optional sign
                            if (i < len && (format[i] == '+' || format[i] == '-'))
                            {
                                i++;
                            }
                            // Skip digits
                            while (i < len && char.IsDigit(format[i]))
                            {
                                i++;
                            }
                        }

                        string formatPart = null;

                        // Check for format (after colon)
                        if (i < len && format[i] == ':')
                        {
                            i++; // Move past ':'
                            int start = i;
                            while (i < len && format[i] != '}')
                            {
                                i++;
                            }
                            formatPart = i < len ? format.Substring(start, i - start) : format.Substring(start);
                            if (i < len)
                            {
                                i++; // Move past '}'
                            }
                        }
                        else if (i < len && format[i] == '}')
                        {
                            // No format part
                            i++; // Move past '}'
                        }
                        else
                        {
                            // Invalid characters after index, skip to '}'
                            while (i < len && format[i] != '}')
                            {
                                i++;
                            }
                            if (i < len)
                            {
                                i++; // Move past '}'
                            }
                        }

                        parameters[index] = formatPart;
                        if (index > maxIndex)
                        {
                            maxIndex = index;
                        }
                    }
                }
                else if (format[i] == '}')
                {
                    // Handle possible escaped '}}'
                    if (i + 1 < len && format[i + 1] == '}')
                    {
                        i += 2; // Skip escaped '}}'
                    }
                    else
                    {
                        i++; // Move past '}'
                    }
                }
                else
                {
                    i++;
                }
            }

            // Generate the result list from 0 to maxIndex
            var result = new List<LocalizableStringParam>();
            if (maxIndex == -1)
            {
                return result;
            }

            for (int idx = 0; idx <= maxIndex; idx++)
            {
                var formatValue = parameters.TryGetValue(idx, out var value) ? value : null;
                result.Add(new LocalizableStringParam { Index = idx, Format = formatValue, Name = $"arg{idx}", Type = "object?" });
            }

            return result;
        }

        /// <summary>
        /// Parses the comment and updates the format parameter names and types.
        /// </summary>
        /// <param name="comment"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static (string Summary, ImmutableArray<LocalizableStringParam> Parameters) ParseCommentAndUpdateParameters(XComment comment, List<LocalizableStringParam> parameters)
        {
            if (comment == null || comment.Value == null || parameters.Count == 0)
            {
                return (null, _emptyLocalizableStringParams);
            }

            try
            {
                var doc = XDocument.Parse($"<root>{comment.Value}</root>");
                var summary = doc.Descendants(Constants.SummaryElementName).FirstOrDefault()?.Value.Trim();

                // Update parameter names and types of the format string
                foreach (var p in doc.Descendants(Constants.ParamElementName))
                {
                    var index = int.TryParse(p.Attribute(Constants.IndexAttribute).Value, out var intValue) ? intValue : -1;
                    var name = p.Attribute(Constants.NameAttribute).Value;
                    var type = p.Attribute(Constants.TypeAttribute).Value;
                    if (index >= 0 && index < parameters.Count)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            parameters[index].Name = name;
                        }
                        if (!string.IsNullOrEmpty(type))
                        {
                            parameters[index].Type = type;
                        }
                    }
                }
                return (summary, parameters.ToImmutableArray());
            }
            catch
            {
                return (null, _emptyLocalizableStringParams);
            }
        }

        #endregion

        #region Get Used Localization Keys

        private static string GetLocalizationKeyFromInvocation(GeneratorSyntaxContext context, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return null;
            }

            var invocation = (InvocationExpressionSyntax)context.Node;
            var expression = invocation.Expression;
            var parts = new List<string>();

            // Traverse the member access hierarchy
            while (expression is MemberAccessExpressionSyntax memberAccess)
            {
                parts.Add(memberAccess.Name.Identifier.Text);
                expression = memberAccess.Expression;
            }

            // Add the leftmost identifier
            if (expression is IdentifierNameSyntax identifier)
            {
                parts.Add(identifier.Identifier.Text);
            }
            else
            {
                return null;
            }

            // Reverse to get [ClassName, SubClass, Method] from [Method, SubClass, ClassName]
            parts.Reverse();

            // Check if the first part is ClassName and there's at least one more part
            if (parts.Count < 2 || parts[0] != Constants.ClassName)
            {
                return null;
            }

            return parts[1];
        }

        #endregion

        #region Generate Source

        private static void GenerateSource(
            SourceProductionContext spc,
            AdditionalText xamlFile,
            ImmutableArray<LocalizableString> localizedStrings,
            string assemblyNamespace,
            bool useDI,
            PluginClassInfo pluginInfo,
            IEnumerable<string> usedKeys)
        {
            // Report unusedKeys
            var unusedKeys = localizedStrings
                    .Select(ls => ls.Key)
                    .ToImmutableHashSet()
                    .Except(usedKeys);
            foreach (var key in unusedKeys)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    SourceGeneratorDiagnostics.LocalizationKeyUnused,
                    Location.None,
                    key));
            }

            var sourceBuilder = new StringBuilder();

            // Generate header
            GeneratedHeaderFromPath(sourceBuilder, xamlFile.Path);
            sourceBuilder.AppendLine();

            // Generate nullable enable
            sourceBuilder.AppendLine("#nullable enable");
            sourceBuilder.AppendLine();

            // Generate namespace
            sourceBuilder.AppendLine($"namespace {assemblyNamespace};");
            sourceBuilder.AppendLine();

            // Uncomment them for debugging
            //sourceBuilder.AppendLine("/*");
            /*// Generate all localization strings
            sourceBuilder.AppendLine("localizedStrings");
            foreach (var ls in localizedStrings)
            {
                sourceBuilder.AppendLine($"{ls.Key} - {ls.Value}");
            }
            sourceBuilder.AppendLine();

            // Generate all unused keys
            sourceBuilder.AppendLine("unusedKeys");
            foreach (var key in unusedKeys)
            {
                sourceBuilder.AppendLine($"{key}");
            }
            sourceBuilder.AppendLine();

            // Generate all used keys
            sourceBuilder.AppendLine("usedKeys");
            foreach (var key in usedKeys)
            {
                sourceBuilder.AppendLine($"{key}");
            }*/
            //sourceBuilder.AppendLine("*/");

            // Generate class
            sourceBuilder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"{nameof(LocalizeSourceGenerator)}\", \"{PackageVersion}\")]");
            sourceBuilder.AppendLine($"public static class {Constants.ClassName}");
            sourceBuilder.AppendLine("{");

            var tabString = Helper.Spacing(1);

            // Generate API instance
            string getTranslation = null;
            if (useDI)
            {
                // Use instance from PublicApiSourceGenerator
                getTranslation = $"{assemblyNamespace}.{Constants.PublicApiClassName}.{Constants.PublicApiInternalPropertyName}.GetTranslation";
            }
            else if (pluginInfo?.IsValid == true)
            {
                getTranslation = $"{pluginInfo.ContextAccessor}.API.GetTranslation";
            }

            // Generate localization methods
            foreach (var ls in localizedStrings)
            {
                var isLast = ls.Equals(localizedStrings.Last());
                GenerateDocComments(sourceBuilder, ls, tabString);
                GenerateLocalizationMethod(sourceBuilder, ls, getTranslation, tabString, isLast);
            }

            sourceBuilder.AppendLine("}");

            // Add source to context
            spc.AddSource($"{Constants.ClassName}.{assemblyNamespace}.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }

        private static void GeneratedHeaderFromPath(StringBuilder sb, string xamlFilePath)
        {
            if (string.IsNullOrEmpty(xamlFilePath))
            {
                sb.AppendLine("/// <auto-generated/>");
            }
            else
            {
                sb.AppendLine("/// <auto-generated>")
                    .AppendLine($"/// From: {xamlFilePath}")
                    .AppendLine("/// </auto-generated>");
            }
        }

        private static void GenerateDocComments(StringBuilder sb, LocalizableString ls, string tabString)
        {
            if (!string.IsNullOrEmpty(ls.Summary))
            {
                var summaryLines = ls.Summary.Split('\n');
                if (summaryLines.Length > 0)
                {
                    sb.AppendLine($"{tabString}/// <summary>");
                    foreach (var line in summaryLines)
                    {
                        sb.AppendLine($"{tabString}/// {line.Trim()}");
                    }
                    sb.AppendLine($"{tabString}/// </summary>");
                }
            }

            var lines = ls.Value.Split('\n');
            if (lines.Length > 0)
            {
                sb.AppendLine($"{tabString}/// <remarks>");
                sb.AppendLine($"{tabString}/// e.g.: <code>");
                foreach (var line in lines)
                {
                    sb.AppendLine($"{tabString}/// {line.Trim()}");
                }
                sb.AppendLine($"{tabString}/// </code>");
                sb.AppendLine($"{tabString}/// </remarks>");
            }
        }

        private static void GenerateLocalizationMethod(
            StringBuilder sb,
            LocalizableString ls,
            string getTranslation,
            string tabString,
            bool last)
        {
            sb.Append($"{tabString}public static string {ls.Key}(");

            // Get parameter string
            var parameters = ls.Params.ToList();
            sb.Append(string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}")));
            sb.Append(") => ");
            var formatArgs = parameters.Count > 0
                ? $", {string.Join(", ", parameters.Select(p => p.Name))}"
                : string.Empty;

            if (!(string.IsNullOrEmpty(getTranslation)))
            {
                sb.AppendLine(parameters.Count > 0
                    ? !ls.Format ? 
                        $"string.Format({getTranslation}(\"{ls.Key}\"){formatArgs});"
                        : $"string.Format(System.Globalization.CultureInfo.CurrentCulture, {getTranslation}(\"{ls.Key}\"){formatArgs});"
                    : $"{getTranslation}(\"{ls.Key}\");");
            }
            else
            {
                sb.AppendLine("\"LOCALIZATION_ERROR\";");
            }

            if (!last)
            {
                sb.AppendLine();
            }
        }

        #endregion

        #region Classes

        public class LocalizableStringParam
        {
            public int Index { get; set; }
            public string Format { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
        }

        public class LocalizableString
        {
            public string Key { get; }
            public string Value { get; }
            public string Summary { get; }
            public IEnumerable<LocalizableStringParam> Params { get; }
            
            public bool Format => Params.Any(p => !string.IsNullOrEmpty(p.Format));

            public LocalizableString(string key, string value, string summary, IEnumerable<LocalizableStringParam> @params)
            {
                Key = key;
                Value = value;
                Summary = summary;
                Params = @params;
            }
        }

        #endregion
    }
}
