using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Reads a Flow Launcher WPF theme (.xaml ResourceDictionary) with System.Xml and resolves the colors and
/// shapes the Avalonia main window uses. No WPF types are involved, so this works on every platform.
/// </summary>
public sealed partial class ThemeLoader
{
    private static readonly string ClassName = nameof(ThemeLoader);

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const int MaxDepth = 16;

    // WPF TextBox.SelectionOpacity default; the theme SelectionBrush is drawn at this opacity in WPF.
    private const double WpfSelectionOpacity = 0.4;

    /// <summary>Avalonia resource keys produced by <see cref="Resolve"/> (consumed in Themes/Base.axaml and the settings preview).</summary>
    public static readonly IReadOnlyList<string> OutputKeys =
    [
        "WindowBackgroundBrush", "WindowBorderBrush", "WindowBorderThickness", "WindowCornerRadius",
        "QuerySurfaceBrush", "ResultAreaBackgroundBrush", "QueryBoxForegroundBrush", "QueryBoxCaretBrush",
        "QueryBoxSelectionBrush", "QuerySuggestionForegroundBrush", "SearchIconBrush", "SeparatorBrush",
        "ResultTitleBrush", "ResultSubTitleBrush", "ResultTitleSelectedBrush", "ResultSubTitleSelectedBrush",
        "ResultGlyphBrush", "ResultGlyphSelectedBrush", "SelectedItemBackgroundBrush", "HoveredItemBackgroundBrush",
        "ScrollThumbBrush", "ScrollThumbPointerOverBrush", "HotkeyBadgeBackgroundBrush", "HotkeyTextBrush"
    ];

    private static readonly HashSet<string> OutputKeySet = new(OutputKeys, StringComparer.Ordinal);

    private static readonly Dictionary<ThemeVariant, Dictionary<string, XElement>> SystemPalettes = new();

    // Theme resources, merged dictionaries first so the theme's own keys win (WPF lookup order).
    private readonly Dictionary<string, XElement> _resources = new(StringComparer.Ordinal);

    private ThemeLoader()
    {
    }

    /// <summary>Theme file for <paramref name="themeName"/> in the program or user themes directory, or empty.</summary>
    public static string GetThemePath(string themeName)
    {
        foreach (var directory in new[]
                 {
                     Path.Combine(Constant.ProgramDirectory, Constant.Themes),
                     Path.Combine(DataLocation.DataDirectory(), Constant.Themes)
                 })
        {
            var candidate = Path.Combine(directory, themeName + ".xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>Parses the theme at <paramref name="themePath"/> including its merged dictionaries (Base.xaml).</summary>
    public static ThemeLoader Load(string themePath)
    {
        var loader = new ThemeLoader();
        loader.AddDictionary(themePath, 0);
        return loader;
    }

    public static void ApplyColorScheme(Application application, ColorSchemes scheme)
    {
        application.RequestedThemeVariant = scheme switch
        {
            ColorSchemes.Light => ThemeVariant.Light,
            ColorSchemes.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    public static ColorSchemes ParseColorScheme(string? value) =>
        Enum.TryParse<ColorSchemes>(value, true, out var result) ? result : ColorSchemes.System;

    /// <summary>
    /// Resolves the main window resources for <paramref name="variant"/> (Light or Dark). System-colored themes
    /// reference the Flow light/dark palettes (Resources/Light.xaml, Resources/Dark.xaml), which are picked per variant.
    /// Keys whose values cannot be resolved are omitted so the built-in Light/Dark defaults stay in effect.
    /// </summary>
    public Dictionary<string, object> Resolve(ThemeVariant variant)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        var isDark = variant == ThemeVariant.Dark;

        void SetBrush(string key, Color? color)
        {
            if (color is { } value)
            {
                result[key] = new ImmutableSolidColorBrush(value);
            }
        }

        // Window
        var windowBackground = StyleColor("WindowBorderStyle", "Background", variant);
        if (IsBlurTheme())
        {
            // Blur themes paint the window with LightBG/DarkBG over the system backdrop (Theme.ColorizeWindow on Windows).
            var useDark = ResourceText("SystemBG") switch
            {
                "Dark" => true,
                "Light" => false,
                _ => isDark
            };
            var lightBg = ThemeResourceColor("LightBG", variant) ?? windowBackground;
            var darkBg = ThemeResourceColor("DarkBG", variant) ?? lightBg;
            windowBackground = useDark ? darkBg : lightBg;
        }

        SetBrush("WindowBackgroundBrush", windowBackground);
        SetBrush("WindowBorderBrush", StyleColor("WindowBorderStyle", "BorderBrush", variant));
        if (ParseThickness(StyleText("WindowBorderStyle", "BorderThickness")) is { } borderThickness)
        {
            result["WindowBorderThickness"] = borderThickness;
        }

        if (ParseCornerRadius(StyleText("WindowBorderStyle", "CornerRadius")) is { } cornerRadius)
        {
            result["WindowCornerRadius"] = cornerRadius;
        }

        // Query box: Flow themes draw the query area on the window background unless QueryBoxBgStyle/QueryBoxStyle paints it.
        var querySurface = StyleColor("QueryBoxBgStyle", "Background", variant);
        if (querySurface is not { A: > 0 })
        {
            querySurface = StyleColor("QueryBoxStyle", "Background", variant);
        }

        SetBrush("QuerySurfaceBrush", querySurface ?? Colors.Transparent);
        SetBrush("ResultAreaBackgroundBrush", Colors.Transparent);

        var queryForeground = StyleColor("QueryBoxStyle", "Foreground", variant);
        SetBrush("QueryBoxForegroundBrush", queryForeground);
        SetBrush("QueryBoxCaretBrush", StyleColor("QueryBoxStyle", "CaretBrush", variant) ?? queryForeground);
        var selectionOpacity = ParseDouble(StyleText("QueryBoxStyle", "SelectionOpacity")) ?? WpfSelectionOpacity;
        SetBrush("QueryBoxSelectionBrush", WithOpacity(StyleColor("QueryBoxStyle", "SelectionBrush", variant), selectionOpacity));
        SetBrush("QuerySuggestionForegroundBrush", StyleColor("QuerySuggestionBoxStyle", "Foreground", variant));
        SetBrush("SearchIconBrush", StyleColor("SearchIconStyle", "Fill", variant, applyStyleOpacity: true));
        SetBrush("SeparatorBrush", StyleColor("SeparatorStyle", "Fill", variant));

        // Results
        var title = StyleColor("ItemTitleStyle", "Foreground", variant);
        var subTitle = StyleColor("ItemSubTitleStyle", "Foreground", variant);
        var glyph = StyleColor("ItemGlyph", "Foreground", variant);
        SetBrush("ResultTitleBrush", title);
        SetBrush("ResultSubTitleBrush", subTitle);
        SetBrush("ResultTitleSelectedBrush", StyleColor("ItemTitleSelectedStyle", "Foreground", variant) ?? title);
        SetBrush("ResultSubTitleSelectedBrush", StyleColor("ItemSubTitleSelectedStyle", "Foreground", variant) ?? subTitle);
        SetBrush("ResultGlyphBrush", glyph);
        SetBrush("ResultGlyphSelectedBrush", StyleColor("ItemGlyphSelectedStyle", "Foreground", variant) ?? glyph);

        var selectedBackground = ResourceColor("ItemSelectedBackgroundColor", variant, 0);
        SetBrush("SelectedItemBackgroundBrush", selectedBackground);
        // WPF themes have no hover color; hover uses a lighter version of the selection color.
        SetBrush("HoveredItemBackgroundBrush", WithOpacity(selectedBackground, 0.5));

        var thumb = ThumbColor(variant);
        SetBrush("ScrollThumbBrush", thumb);
        SetBrush("ScrollThumbPointerOverBrush", thumb);

        SetBrush("HotkeyTextBrush", StyleColor("ItemHotkeyStyle", "Foreground", variant, applyStyleOpacity: true));
        SetBrush("HotkeyBadgeBackgroundBrush", StyleColor("ItemHotkeyBGStyle", "Background", variant));

        return result;
    }

    #region Parsing

    private void AddDictionary(string path, int depth)
    {
        if (depth > MaxDepth || !File.Exists(path))
        {
            return;
        }

        var root = XDocument.Load(path).Root;
        if (root == null)
        {
            return;
        }

        foreach (var dictionary in root.Elements()
                     .Where(e => e.Name.LocalName == "ResourceDictionary.MergedDictionaries")
                     .Elements())
        {
            var source = dictionary.Attribute("Source")?.Value;
            if (!string.IsNullOrWhiteSpace(source) && ResolveSource(source, path) is { } mergedPath)
            {
                AddDictionary(mergedPath, depth + 1);
            }
        }

        foreach (var element in root.Elements())
        {
            if (Key(element) is { } key)
            {
                _resources[key] = element;
            }
        }
    }

    private static string? ResolveSource(string source, string referencingFile)
    {
        // pack://application:,,,/Themes/Base.xaml and /Flow.Launcher;component/Themes/Base.xaml point into the app directory.
        string relative;
        var packIndex = source.IndexOf(",,,/", StringComparison.Ordinal);
        var componentIndex = source.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
        if (packIndex >= 0)
        {
            relative = source[(packIndex + 4)..];
        }
        else if (componentIndex >= 0)
        {
            relative = source[(componentIndex + ";component/".Length)..];
        }
        else
        {
            var local = Path.Combine(Path.GetDirectoryName(referencingFile) ?? string.Empty, source);
            return File.Exists(local) ? local : null;
        }

        relative = relative.Replace('/', Path.DirectorySeparatorChar);
        var programPath = Path.Combine(Constant.ProgramDirectory, relative);
        if (File.Exists(programPath))
        {
            return programPath;
        }

        // User themes may ship their own copy next to the theme file.
        var sibling = Path.Combine(Path.GetDirectoryName(referencingFile) ?? string.Empty, Path.GetFileName(relative));
        return File.Exists(sibling) ? sibling : null;
    }

    private static string? Key(XElement element) =>
        element.Attribute(XName.Get("Key", XamlNamespace))?.Value;

    private static Dictionary<string, XElement> SystemPalette(ThemeVariant variant)
    {
        lock (SystemPalettes)
        {
            if (SystemPalettes.TryGetValue(variant, out var palette))
            {
                return palette;
            }

            palette = new Dictionary<string, XElement>(StringComparer.Ordinal);
            var path = Path.Combine(Constant.ProgramDirectory, "Resources", variant == ThemeVariant.Dark ? "Dark.xaml" : "Light.xaml");
            try
            {
                if (XDocument.Load(path).Root is { } root)
                {
                    foreach (var element in root.Elements())
                    {
                        if (Key(element) is { } key)
                        {
                            palette[key] = element;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, $"Failed to load system palette <{path}>", e);
            }

            SystemPalettes[variant] = palette;
            return palette;
        }
    }

    private bool IsBlurTheme() =>
        bool.TryParse(ResourceText("ThemeBlurEnabled"), out var enabled) && enabled;

    private string? ResourceText(string key) =>
        _resources.TryGetValue(key, out var element) ? element.Value.Trim() : null;

    #endregion

    #region Style setters

    /// <summary>A setter value: either the Value attribute text or the element inside &lt;Setter.Value&gt;.</summary>
    private readonly record struct SetterValue(string? Text, XElement? Element);

    private SetterValue? FindSetter(string styleKey, string property, int depth = 0)
    {
        if (depth > MaxDepth || !_resources.TryGetValue(styleKey, out var style) || style.Name.LocalName != "Style")
        {
            return null;
        }

        var setter = style.Elements()
            .Where(e => e.Name.LocalName == "Setter" && IsProperty(e.Attribute("Property")?.Value, property))
            .LastOrDefault();

        if (setter != null)
        {
            if (setter.Attribute("Value") is { } valueAttribute)
            {
                return new SetterValue(valueAttribute.Value, null);
            }

            var element = setter.Elements().FirstOrDefault(e => e.Name.LocalName == "Setter.Value")?.Elements().FirstOrDefault();
            return element != null ? new SetterValue(null, element) : null;
        }

        var basedOn = MarkupKey(style.Attribute("BasedOn")?.Value);
        // A style BasedOn its own key refers to an earlier definition that was overridden; there is nothing more to find.
        return basedOn != null && basedOn != styleKey ? FindSetter(basedOn, property, depth + 1) : null;
    }

    private static bool IsProperty(string? declared, string property) =>
        declared != null && (declared == property || declared.EndsWith("." + property, StringComparison.Ordinal));

    private string? StyleText(string styleKey, string property) => FindSetter(styleKey, property)?.Text;

    private Color? StyleColor(string styleKey, string property, ThemeVariant variant, bool applyStyleOpacity = false)
    {
        if (FindSetter(styleKey, property) is not { } setter)
        {
            return null;
        }

        var color = SetterColor(setter, variant, 0);
        if (applyStyleOpacity && ParseDouble(StyleText(styleKey, "Opacity")) is { } opacity)
        {
            color = WithOpacity(color, opacity);
        }

        return color;
    }

    private Color? ThumbColor(ThemeVariant variant)
    {
        // Thumb colors live in the ThumbStyle template (<Border Background=.../>); some themes set Background directly.
        if (FindSetter("ThumbStyle", "Template") is { Element: { } template } &&
            template.Descendants().FirstOrDefault(e => e.Name.LocalName == "Border" && e.Attribute("Background") != null) is { } border)
        {
            return TextColor(border.Attribute("Background")!.Value, variant, 0);
        }

        return StyleColor("ThumbStyle", "Background", variant);
    }

    #endregion

    #region Color resolution

    [GeneratedRegex(@"^\{\s*(?:\w+:)?(?:DynamicResource|StaticResource|DynamicColor|ThemeResource)\s+(?:ResourceKey\s*=\s*)?([^\s,}]+)\s*\}$")]
    private static partial Regex ResourceReferenceRegex();

    private static string? MarkupKey(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return null;
        }

        var match = ResourceReferenceRegex().Match(markup.Trim());
        return match.Success ? match.Groups[1].Value : null;
    }

    private Color? SetterColor(SetterValue setter, ThemeVariant variant, int depth) =>
        setter.Element != null ? ElementColor(setter.Element, variant, depth) : TextColor(setter.Text, variant, depth);

    private Color? TextColor(string? text, ThemeVariant variant, int depth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        if (text.StartsWith('{'))
        {
            return MarkupKey(text) is { } key ? ResourceColor(key, variant, depth + 1) : null;
        }

        return Color.TryParse(text, out var color) ? color : null;
    }

    private Color? ElementColor(XElement element, ThemeVariant variant, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        switch (element.Name.LocalName)
        {
            case "Color":
                return TextColor(element.Value, variant, depth);
            case "SolidColorBrush":
                var color = TextColor(element.Attribute("Color")?.Value ?? element.Value, variant, depth);
                return ParseDouble(element.Attribute("Opacity")?.Value) is { } opacity ? WithOpacity(color, opacity) : color;
            default:
                // Gradient and other brushes are not mapped; the built-in default stays in effect.
                return null;
        }
    }

    /// <summary>Theme (and Base.xaml) resources only; used for theme metadata colors such as LightBG/DarkBG.</summary>
    private Color? ThemeResourceColor(string key, ThemeVariant variant) =>
        _resources.TryGetValue(key, out var element) ? ElementColor(element, variant, 0) : null;

    /// <summary>
    /// Resolves a resource reference: theme dictionaries first, then the Flow light/dark palette for the variant,
    /// then the WinUI system brushes, then the Avalonia application resources (system accent colors).
    /// </summary>
    private Color? ResourceColor(string key, ThemeVariant variant, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        if (_resources.TryGetValue(key, out var element) || SystemPalette(variant).TryGetValue(key, out element))
        {
            return ElementColor(element, variant, depth + 1);
        }

        if (WinUiSystemColors.TryGetValue(key, out var system))
        {
            return Color.FromUInt32(variant == ThemeVariant.Dark ? system.Dark : system.Light);
        }

        return ApplicationColor(key, variant)
               ?? (key.EndsWith("Brush", StringComparison.Ordinal) ? ApplicationColor(key[..^"Brush".Length], variant) : null);
    }

    // WinUI 2 system brushes that themes reference through iNKORE (m:DynamicColor) on Windows. FluentAvalonia does not
    // define them, so they are resolved from the standard WinUI base/chrome colors (ARGB light, dark).
    private static readonly Dictionary<string, (uint Light, uint Dark)> WinUiSystemColors = new(StringComparer.Ordinal)
    {
        ["SystemControlHighlightBaseHighBrush"] = (0xFF000000, 0xFFFFFFFF),
        ["SystemControlForegroundBaseHighBrush"] = (0xFF000000, 0xFFFFFFFF),
        ["SystemControlPageTextBaseHighBrush"] = (0xFF000000, 0xFFFFFFFF),
        ["SystemControlHighlightBaseMediumBrush"] = (0x99000000, 0x99FFFFFF),
        ["SystemControlForegroundBaseMediumBrush"] = (0x99000000, 0x99FFFFFF),
        ["SystemControlPageTextBaseMediumBrush"] = (0x99000000, 0x99FFFFFF),
        ["SystemControlHighlightBaseMediumLowBrush"] = (0x66000000, 0x66FFFFFF),
        ["SystemControlBackgroundBaseLowBrush"] = (0x33000000, 0x33FFFFFF),
        ["SystemControlBackgroundChromeMediumBrush"] = (0xFFE6E6E6, 0xFF1F1F1F),
        ["SystemControlBackgroundChromeMediumLowBrush"] = (0xFFF2F2F2, 0xFF2B2B2B)
    };

    private static Color? ApplicationColor(string key, ThemeVariant variant)
    {
        if (OutputKeySet.Contains(key) || Application.Current is not { } application ||
            !application.TryGetResource(key, variant, out var value))
        {
            return null;
        }

        return value switch
        {
            Color color => color,
            ISolidColorBrush brush => WithOpacity(brush.Color, brush.Opacity),
            _ => null
        };
    }

    private static Color? WithOpacity(Color? color, double opacity)
    {
        if (color is not { } value)
        {
            return null;
        }

        var alpha = (byte)Math.Round(Math.Clamp(value.A * opacity, 0, 255));
        return Color.FromArgb(alpha, value.R, value.G, value.B);
    }

    #endregion

    #region Shapes

    private static double? ParseDouble(string? text) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static double[]? ParseNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith('{'))
        {
            return null;
        }

        var parts = text.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var numbers = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (ParseDouble(parts[i]) is not { } number)
            {
                return null;
            }

            numbers[i] = number;
        }

        return numbers;
    }

    private static Thickness? ParseThickness(string? text) => ParseNumbers(text) switch
    {
        [var uniform] => new Thickness(uniform),
        [var horizontal, var vertical] => new Thickness(horizontal, vertical),
        [var left, var top, var right, var bottom] => new Thickness(left, top, right, bottom),
        _ => null
    };

    private static CornerRadius? ParseCornerRadius(string? text) => ParseNumbers(text) switch
    {
        [var uniform] => new CornerRadius(uniform),
        [var topLeft, var topRight, var bottomRight, var bottomLeft] => new CornerRadius(topLeft, topRight, bottomRight, bottomLeft),
        _ => null
    };

    #endregion
}

/// <summary>
/// Application-level resource dictionary (merged in App.axaml after the built-in defaults) that carries the
/// selected Flow theme as Light/Dark theme dictionaries. It reloads when <see cref="Settings.Theme"/> changes and
/// applies the saved color scheme when attached to the application.
/// </summary>
public class ThemeResources : ResourceDictionary
{
    private static readonly string ClassName = nameof(ThemeResources);
    private const string ThemeNameKey = "FlowLauncherThemeName";

    private readonly Settings? _settings;

    public ThemeResources()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        _settings = Ioc.Default.GetService<Settings>();
        if (_settings == null)
        {
            return;
        }

        Load(_settings.Theme);
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        OwnerChanged += OnOwnerChanged;
    }

    private void OnOwnerChanged(object? sender, EventArgs e)
    {
        // Apply the saved ColorScheme once the dictionary is attached to the application (startup).
        if (Owner is not Application application || _settings == null)
        {
            return;
        }

        OwnerChanged -= OnOwnerChanged;
        ThemeLoader.ApplyColorScheme(application, ThemeLoader.ParseColorScheme(_settings.ColorScheme));
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Settings.Theme) || _settings == null)
        {
            return;
        }

        var theme = _settings.Theme;
        if (Dispatcher.UIThread.CheckAccess())
        {
            Load(theme);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Load(theme));
        }
    }

    private void Load(string themeName)
    {
        Dictionary<string, object> light = new(), dark = new();
        try
        {
            var path = ThemeLoader.GetThemePath(themeName);
            if (string.IsNullOrEmpty(path))
            {
                Log.Warn(ClassName, $"Theme <{themeName}> not found; using built-in colors");
            }
            else
            {
                var theme = ThemeLoader.Load(path);
                light = theme.Resolve(ThemeVariant.Light);
                dark = theme.Resolve(ThemeVariant.Dark);
                Log.Info(ClassName, $"Theme <{themeName}> applied: {light.Count} light / {dark.Count} dark resources resolved");
            }
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, $"Failed to load theme <{themeName}>; using built-in colors", e);
        }

        ReplaceVariant(ThemeVariant.Light, light);
        ReplaceVariant(ThemeVariant.Dark, dark);
        // Raises a resources-changed notification on the owner so every DynamicResource re-resolves.
        this[ThemeNameKey] = themeName;
    }

    private void ReplaceVariant(ThemeVariant variant, Dictionary<string, object> values)
    {
        var dictionary = new ResourceDictionary();
        foreach (var (key, value) in values)
        {
            dictionary[key] = value;
        }

        ThemeDictionaries.Remove(variant);
        ThemeDictionaries[variant] = dictionary;
    }
}
