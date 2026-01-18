using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;

namespace Flow.Launcher.Avalonia.Resource;

/// <summary>
/// Markup extension for accessing localized strings in XAML.
/// Usage: Text="{i18n:Localize queryTextBoxPlaceholder}"
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// The translation key to look up.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Fallback value if translation is not found.
    /// </summary>
    public string? Fallback { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
        {
            return Fallback ?? "[No Key]";
        }

        try
        {
            // Try to get I18n service from DI
            var i18n = Ioc.Default.GetService<Internationalization>();
            if (i18n != null && i18n.HasTranslation(Key))
            {
                return i18n.GetTranslation(Key);
            }
        }
        catch
        {
            // Ioc.Default might throw if not configured yet
        }

        return Fallback ?? $"[{Key}]";
    }
}

/// <summary>
/// Static helper class for accessing translations from code-behind.
/// </summary>
public static class Translator
{
    /// <summary>
    /// Get a translated string by key.
    /// </summary>
    /// <param name="key">The translation key</param>
    /// <returns>The translated string or the key in brackets if not found</returns>
    public static string GetString(string key)
    {
        try
        {
            var i18n = Ioc.Default.GetService<Internationalization>();
            if (i18n == null)
            {
                return $"[{key}]";
            }

            return i18n.GetTranslation(key);
        }
        catch
        {
            return $"[{key}]";
        }
    }

    /// <summary>
    /// Get a translated string with format arguments.
    /// </summary>
    /// <param name="key">The translation key</param>
    /// <param name="args">Format arguments</param>
    /// <returns>The formatted translated string</returns>
    public static string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }
}
