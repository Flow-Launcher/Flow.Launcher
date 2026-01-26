using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.Resource;

/// <summary>
/// Internationalization service for Avalonia that parses WPF XAML language files.
/// </summary>
public class Internationalization
{
    private static readonly string ClassName = nameof(Internationalization);
    private const string LanguagesFolder = "Languages";
    private const string DefaultLanguageCode = "en";
    private const string Extension = ".xaml";

    private readonly Settings _settings;
    private readonly Dictionary<string, string> _translations = new();
    private readonly List<string> _languageDirectories = [];

    // WPF XAML namespace for system:String
    private static readonly XNamespace SystemNs = "clr-namespace:System;assembly=mscorlib";
    private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    public Internationalization(Settings settings)
    {
        _settings = settings;
        Initialize();
    }

    /// <summary>
    /// Initialize language resources based on settings.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Add Flow Launcher language directory
            AddFlowLauncherLanguageDirectory();
            
            // Add plugin language directories
            AddPluginLanguageDirectories();

            // Load English as base/fallback
            LoadLanguageFile(DefaultLanguageCode);

            // Load the configured language on top if different from English
            var languageCode = GetActualLanguageCode();
            if (!string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                LoadLanguageFile(languageCode);
            }

            // Update culture info
            ChangeCultureInfo(languageCode);

            Log.Info(ClassName, $"Loaded {_translations.Count} translations for language '{languageCode}'");
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to initialize internationalization", e);
        }
    }

    /// <summary>
    /// Update plugin metadata name & description and call OnCultureInfoChanged.
    /// </summary>
    public void UpdatePluginMetadataTranslations()
    {
        foreach (var p in PluginManager.GetTranslationPlugins())
        {
            if (p.Plugin is not IPluginI18n pluginI18N) continue;
            try
            {
                p.Metadata.Name = pluginI18N.GetTranslatedPluginTitle();
                p.Metadata.Description = pluginI18N.GetTranslatedPluginDescription();
                pluginI18N.OnCultureInfoChanged(CultureInfo.CurrentCulture);
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, $"Failed for <{p.Metadata.Name}>", e);
            }
        }
    }

    private string GetActualLanguageCode()
    {
        var languageCode = _settings.Language;
        
        // Handle "system" language setting
        if (languageCode == Constant.SystemLanguageCode)
        {
            languageCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        }

        return languageCode ?? DefaultLanguageCode;
    }

    private void AddFlowLauncherLanguageDirectory()
    {
        var directory = Path.Combine(Constant.ProgramDirectory, LanguagesFolder);
        if (Directory.Exists(directory))
        {
            _languageDirectories.Add(directory);
            Log.Debug(ClassName, $"Added language directory: {directory}");
        }
        else
        {
            Log.Warn(ClassName, $"Language directory not found: {directory}");
        }
    }

    private void AddPluginLanguageDirectories()
    {
        // Add plugin language directories (similar to WPF version)
        var pluginsDir = Path.Combine(Constant.ProgramDirectory, "Plugins");
        if (!Directory.Exists(pluginsDir)) return;

        foreach (var dir in Directory.GetDirectories(pluginsDir))
        {
            var pluginLanguageDir = Path.Combine(dir, LanguagesFolder);
            if (Directory.Exists(pluginLanguageDir))
            {
                _languageDirectories.Add(pluginLanguageDir);
                Log.Debug(ClassName, $"Added plugin language directory: {pluginLanguageDir}");
            }
        }
    }

    private void LoadLanguageFile(string languageCode)
    {
        var filename = $"{languageCode}{Extension}";
        
        foreach (var dir in _languageDirectories)
        {
            var filePath = Path.Combine(dir, filename);
            if (!File.Exists(filePath))
            {
                // Try fallback to English if specific language not found
                if (!string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    filePath = Path.Combine(dir, $"{DefaultLanguageCode}{Extension}");
                }
                
                if (!File.Exists(filePath))
                {
                    continue;
                }
            }

            try
            {
                ParseWpfXamlFile(filePath);
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, $"Failed to parse language file: {filePath}", e);
            }
        }
    }

    /// <summary>
    /// Parse a WPF XAML ResourceDictionary file and extract string resources.
    /// </summary>
    private void ParseWpfXamlFile(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Root;
        if (root == null) return;

        var count = 0;
        // Find all system:String elements - WPF XAML uses clr-namespace:System;assembly=mscorlib
        foreach (var element in root.Descendants())
        {
            // Check if this is a system:String element (namespace doesn't matter, just check local name)
            if (element.Name.LocalName == "String")
            {
                // Get the x:Key attribute
                var keyAttr = element.Attribute(XNs + "Key");
                if (keyAttr != null)
                {
                    var key = keyAttr.Value;
                    var value = element.Value;
                    _translations[key] = value;
                    count++;
                }
            }
        }
        
        Log.Debug(ClassName, $"Parsed {count} strings from {filePath}");
    }

    /// <summary>
    /// Inject all translations into Avalonia's Application.Resources so {DynamicResource} works.
    /// This enables plugin settings to use {DynamicResource key} for localization.
    /// Must be called after Application is initialized.
    /// </summary>
    public void InjectIntoApplicationResources()
    {
        try
        {
            var app = Application.Current;
            if (app == null)
            {
                Log.Warn(ClassName, "Cannot inject resources: Application.Current is null");
                return;
            }

            var count = 0;
            foreach (var kvp in _translations)
            {
                app.Resources[kvp.Key] = kvp.Value;
                count++;
            }

            Log.Info(ClassName, $"Injected {count} translations into Application.Resources");
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to inject translations into Application.Resources", e);
        }
    }

    /// <summary>
    /// Get a translated string by key.
    /// </summary>
    public string GetTranslation(string key)
    {
        if (_translations.TryGetValue(key, out var translation))
        {
            return translation;
        }

        Log.Warn(ClassName, $"Translation not found for key: {key}");
        return $"[{key}]";
    }

    /// <summary>
    /// Check if a translation exists for the given key.
    /// </summary>
    public bool HasTranslation(string key) => _translations.ContainsKey(key);

    /// <summary>
    /// Get all available translations (for debugging).
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllTranslations() => _translations;

    private static void ChangeCultureInfo(string languageCode)
    {
        try
        {
            var culture = CultureInfo.CreateSpecificCulture(languageCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            Log.Warn(ClassName, $"Culture not found for language code: {languageCode}");
        }
    }

    /// <summary>
    /// Change language at runtime.
    /// </summary>
    public void ChangeLanguage(string languageCode)
    {
        _translations.Clear();
        
        // Reload English as base
        LoadLanguageFile(DefaultLanguageCode);
        
        // Load new language on top
        if (!string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            LoadLanguageFile(languageCode);
        }

        ChangeCultureInfo(languageCode);
        
        // Re-inject into Application.Resources for DynamicResource bindings
        InjectIntoApplicationResources();
        
        Log.Info(ClassName, $"Language changed to: {languageCode}");
    }
}
