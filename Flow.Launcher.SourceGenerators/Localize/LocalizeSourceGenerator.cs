using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Flow.Launcher.SourceGenerators.Localize
{
    [Generator]
    public partial class LocalizeSourceGenerator : ISourceGenerator
    {
        private OptimizationLevel _optimizationLevel;

        private const string CoreNamespace1 = "Flow.Launcher";
        private const string CoreNamespace2 = "Flow.Launcher.Core";
        private const string DefaultNamespace = "Flow.Launcher";
        private const string ClassName = "Localize";
        private const string PluginInterfaceName = "IPluginI18n";
        private const string PluginContextTypeName = "PluginInitContext";
        private const string KeywordStatic = "static";
        private const string KeywordPrivate = "private";
        private const string KeywordProtected = "protected";
        private const string XamlPrefix = "system";
        private const string XamlTag = "String";

        private const string DefaultLanguageFilePathEndsWith = @"\Languages\en.xaml";
        private const string XamlCustomPathPropertyKey = "build_property.localizegeneratorlangfiles";
        private readonly char[] _xamlCustomPathPropertyDelimiters = { '\n', ';' };
        private readonly Regex _languagesXamlRegex = new Regex(@"\\Languages\\[^\\]+\.xaml$", RegexOptions.IgnoreCase);

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            _optimizationLevel = context.Compilation.Options.OptimizationLevel;

            context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(
                XamlCustomPathPropertyKey,
                out var langFilePathEndsWithStr
            );

            var allLanguageKeys = new List<string>();
            context.Compilation.SyntaxTrees
                .SelectMany(v => v.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                .ToList()
                .ForEach(
                    v =>
                    {
                        var split = v.Expression.ToString().Split('.');
                        if (split.Length < 2) return;
                        if (!(split[0] is ClassName)) return;
                        allLanguageKeys.Add(split[1]);
                    });

            var allXamlFiles = context.AdditionalFiles
                .Where(v => _languagesXamlRegex.IsMatch(v.Path))
                .ToArray();
            AdditionalText[] resourceDictionaries;
            if (allXamlFiles.Length is 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SourceGeneratorDiagnostics.CouldNotFindResourceDictionaries,
                    Location.None
                ));
                return;
            }

            if (string.IsNullOrEmpty(langFilePathEndsWithStr))
            {
                if (allXamlFiles.Length is 1)
                {
                    resourceDictionaries = allXamlFiles;
                }
                else
                {
                    resourceDictionaries = allXamlFiles.Where(v => v.Path.EndsWith(DefaultLanguageFilePathEndsWith)).ToArray();
                    if (resourceDictionaries.Length is 0)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            SourceGeneratorDiagnostics.CouldNotFindResourceDictionaries,
                            Location.None
                        ));
                        return;
                    }
                }
            }
            else
            {
                var langFilePathEndings = langFilePathEndsWithStr
                    .Trim()
                    .Split(_xamlCustomPathPropertyDelimiters)
                    .Select(v => v.Trim())
                    .ToArray();
                resourceDictionaries = allXamlFiles.Where(v => langFilePathEndings.Any(v.Path.EndsWith)).ToArray();
                if (resourceDictionaries.Length is 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.CouldNotFindResourceDictionaries,
                        Location.None
                    ));
                    return;
                }
            }

            var ns = context.Compilation.AssemblyName ?? DefaultNamespace;

            var localizedStrings = LoadLocalizedStrings(resourceDictionaries);

            var unusedLocalizationKeys = localizedStrings.Keys.Except(allLanguageKeys).ToArray();

            foreach (var key in unusedLocalizationKeys)
                context.ReportDiagnostic(Diagnostic.Create(
                    SourceGeneratorDiagnostics.LocalizationKeyUnused,
                    Location.None,
                    key
                ));

            var sourceCode = GenerateSourceCode(localizedStrings, context, unusedLocalizationKeys);

            context.AddSource($"{ClassName}.{ns}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }

        private static Dictionary<string, LocalizableString> LoadLocalizedStrings(AdditionalText[] files)
        {
            var result = new Dictionary<string, LocalizableString>();

            foreach (var file in files)
            {
                ProcessXamlFile(file, result);
            }

            return result;
        }

        private static void ProcessXamlFile(AdditionalText file, Dictionary<string, LocalizableString> result) {
            var content = file.GetText()?.ToString();
            if (content is null) return;
            var doc = XDocument.Parse(content);
            var ns = doc.Root?.GetNamespaceOfPrefix(XamlPrefix);
            if (ns is null) return;
            foreach (var element in doc.Descendants(ns + XamlTag))
            {
                var name = element.FirstAttribute?.Value;
                var value = element.Value;

                if (name is null) continue;

                string summary = null;
                var paramsList = new List<LocalizableStringParam>();
                var commentNode = element.PreviousNode;

                if (commentNode is XComment comment)
                    summary = ProcessXamlFileComment(comment, paramsList);

                result[name] = new LocalizableString(name, value, summary, paramsList);
            }
        }

        private static string ProcessXamlFileComment(XComment comment, List<LocalizableStringParam> paramsList) {
            string summary = null;
            try
            {
                if (CommentIncludesDocumentationMarkup(comment))
                {
                    var commentDoc = XDocument.Parse($"<root>{comment.Value}</root>");
                    summary = ExtractDocumentationCommentSummary(commentDoc);
                    foreach (var param in commentDoc.Descendants("param"))
                    {
                        var index = int.Parse(param.Attribute("index")?.Value ?? "-1");
                        var paramName = param.Attribute("name")?.Value;
                        var paramType = param.Attribute("type")?.Value;
                        if (index < 0 || paramName is null || paramType is null) continue;
                        paramsList.Add(new LocalizableStringParam(index, paramName, paramType));
                    }
                }
            }
            catch
            {
                // ignore
            }

            return summary;
        }

        private static string ExtractDocumentationCommentSummary(XDocument commentDoc) {
            return commentDoc.Descendants("summary").FirstOrDefault()?.Value.Trim();
        }

        private static bool CommentIncludesDocumentationMarkup(XComment comment) {
            return comment.Value.Contains("<summary>") || comment.Value.Contains("<param ");
        }

        private string GenerateSourceCode(
            Dictionary<string, LocalizableString> localizedStrings,
            GeneratorExecutionContext context,
            string[] unusedLocalizationKeys
        )
        {
            var ns = context.Compilation.AssemblyName;

            var sb = new StringBuilder();
            if (ns is CoreNamespace1 || ns is CoreNamespace2)
            {
                GenerateFileHeader(sb, context);
                GenerateClass(sb, localizedStrings, unusedLocalizationKeys);
                return sb.ToString();
            }

            string contextPropertyName = null;
            var mainClassFound = false;
            foreach (var (syntaxTree, classDeclaration) in GetClasses(context))
            {
                if (!DoesClassImplementInterface(classDeclaration, PluginInterfaceName))
                    continue;

                mainClassFound = true;

                var property = GetPluginContextProperty(classDeclaration);
                if (property is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.CouldNotFindContextProperty,
                        GetLocation(syntaxTree, classDeclaration),
                        classDeclaration.Identifier
                    ));
                    return string.Empty;
                }

                var propertyModifiers = GetPropertyModifiers(property);

                if (!propertyModifiers.Static)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.ContextPropertyNotStatic,
                        GetLocation(syntaxTree, property),
                        property.Identifier
                    ));
                    return string.Empty;
                }

                if (propertyModifiers.Private)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.ContextPropertyIsPrivate,
                        GetLocation(syntaxTree, property),
                        property.Identifier
                    ));
                    return string.Empty;
                }

                if (propertyModifiers.Protected)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SourceGeneratorDiagnostics.ContextPropertyIsProtected,
                        GetLocation(syntaxTree, property),
                        property.Identifier
                    ));
                    return string.Empty;
                }

                contextPropertyName = $"{classDeclaration.Identifier}.{property.Identifier}";
                break;
            }

            if (mainClassFound is false)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SourceGeneratorDiagnostics.CouldNotFindPluginEntryClass,
                    Location.None
                ));
                return string.Empty;
            }

            GenerateFileHeader(sb, context, true);
            GenerateClass(sb, localizedStrings, unusedLocalizationKeys, contextPropertyName);
            return sb.ToString();
        }

        private static void GenerateFileHeader(StringBuilder sb, GeneratorExecutionContext context, bool isPlugin = false)
        {
            var rootNamespace = context.Compilation.AssemblyName;
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");

            if (!isPlugin)
                sb.AppendLine("using Flow.Launcher.Core.Resource;");

            sb.AppendLine($"namespace {rootNamespace};");
        }

        private void GenerateClass(
            StringBuilder sb,
            Dictionary<string, LocalizableString> localizedStrings,
            string[] unusedLocalizationKeys,
            string propertyName = null
        )
        {
            sb.AppendLine();
            sb.AppendLine($"public static class {ClassName}");
            sb.AppendLine("{");
            foreach (var localizedString in localizedStrings)
            {
                if (_optimizationLevel == OptimizationLevel.Release && unusedLocalizationKeys.Contains(localizedString.Key))
                    continue;

                GenerateDocCommentForMethod(sb, localizedString.Value);
                GenerateMethod(sb, localizedString.Value, propertyName);
            }

            sb.AppendLine("}");
        }

        private static void GenerateDocCommentForMethod(StringBuilder sb, LocalizableString localizableString)
        {
            sb.AppendLine("/// <summary>");
            if (!(localizableString.Summary is null))
            {
                sb.AppendLine(string.Join("\n", localizableString.Summary.Trim().Split('\n').Select(v => $"/// {v}")));
            }

            sb.AppendLine("/// <code>");
            var value = localizableString.Value;
            foreach (var p in localizableString.Params)
            {
                value = value.Replace($"{{{p.Index}}}", $"{{{p.Name}}}");
            }
            sb.AppendLine(string.Join("\n", value.Split('\n').Select(v => $"/// {v}")));
            sb.AppendLine("/// </code>");
            sb.AppendLine("/// </summary>");
        }

        private static void GenerateMethod(StringBuilder sb, LocalizableString localizableString, string contextPropertyName)
        {
            sb.Append($"public static string {localizableString.Key}(");
            var declarationArgs = new List<string>();
            var callArgs = new List<string>();
            for (var i = 0; i < 10; i++)
            {
                if (localizableString.Value.Contains($"{{{i}}}"))
                {
                    var param = localizableString.Params.FirstOrDefault(v => v.Index == i);
                    if (!(param is null))
                    {
                        declarationArgs.Add($"{param.Type} {param.Name}");
                        callArgs.Add(param.Name);
                    }
                    else
                    {
                        declarationArgs.Add($"object? arg{i}");
                        callArgs.Add($"arg{i}");
                    }
                }
                else
                {
                    break;
                }
            }

            string callArray;
            switch (callArgs.Count)
            {
                case 0:
                    callArray = "";
                    break;
                case 1:
                    callArray = callArgs[0];
                    break;
                default:
                    callArray = $"new object?[] {{ {string.Join(", ", callArgs)} }}";
                    break;
            }

            sb.Append(string.Join(", ", declarationArgs));
            sb.Append(") => ");
            if (contextPropertyName is null)
            {
                if (string.IsNullOrEmpty(callArray))
                {
                    sb.AppendLine($"InternationalizationManager.Instance.GetTranslation(\"{localizableString.Key}\");");
                }
                else
                {
                    sb.AppendLine(
                        $"string.Format(InternationalizationManager.Instance.GetTranslation(\"{localizableString.Key}\"), {callArray});"
                    );
                }
            }
            else
            {
                if (string.IsNullOrEmpty(callArray))
                {
                    sb.AppendLine($"{contextPropertyName}.API.GetTranslation(\"{localizableString.Key}\");");
                }
                else
                {
                    sb.AppendLine($"string.Format({contextPropertyName}.API.GetTranslation(\"{localizableString.Key}\"), {callArray});");
                }
            }

            sb.AppendLine();
        }

        private static Location GetLocation(SyntaxTree syntaxTree, CSharpSyntaxNode classDeclaration)
        {
            return Location.Create(syntaxTree, classDeclaration.GetLocation().SourceSpan);
        }

        private static IEnumerable<(SyntaxTree, ClassDeclarationSyntax)> GetClasses(GeneratorExecutionContext context)
        {
            foreach (var syntaxTree in context.Compilation.SyntaxTrees)
            {
                var classDeclarations = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classDeclaration in classDeclarations)
                {
                    yield return (syntaxTree, classDeclaration);
                }
            }
        }

        private static bool DoesClassImplementInterface(ClassDeclarationSyntax classDeclaration, string interfaceName)
        {
            return classDeclaration.BaseList?.Types.Any(v => interfaceName == v.ToString()) is true;
        }

        private static PropertyDeclarationSyntax GetPluginContextProperty(ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(v => v.Type.ToString() is PluginContextTypeName);
        }

        private static Modifiers GetPropertyModifiers(PropertyDeclarationSyntax property)
        {
            var isStatic = property.Modifiers.Any(v => v.Text is KeywordStatic);
            var isPrivate = property.Modifiers.Any(v => v.Text is KeywordPrivate);
            var isProtected = property.Modifiers.Any(v => v.Text is KeywordProtected);

            return new Modifiers(isStatic, isPrivate, isProtected);
        }

        private class Modifiers
        {
            public bool Static { get; }
            public bool Private { get; }
            public bool Protected { get; }

            public Modifiers(bool isStatic = false, bool isPrivate = false, bool isProtected = false)
            {
                Static = isStatic;
                Private = isPrivate;
                Protected = isProtected;
            }
        }
    }

    public class LocalizableStringParam
    {
        public int Index { get; }
        public string Name { get; }
        public string Type { get; }

        public LocalizableStringParam(int index, string name, string type)
        {
            Index = index;
            Name = name;
            Type = type;
        }
    }

    public class LocalizableString
    {
        public string Key { get; }
        public string Value { get; }
        public string Summary { get; }
        public IEnumerable<LocalizableStringParam> Params { get; }

        public LocalizableString(string key, string value, string summary, IEnumerable<LocalizableStringParam> @params)
        {
            Key = key;
            Value = value;
            Summary = summary;
            Params = @params;
        }
    }
}
