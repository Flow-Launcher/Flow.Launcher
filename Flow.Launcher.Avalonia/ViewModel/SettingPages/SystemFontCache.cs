using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

/// <summary>
/// Sorted, distinct system font family names, enumerated once per process and shared
/// by all settings pages (the list is never mutated after creation).
/// </summary>
public static class SystemFontCache
{
    private static readonly Lazy<List<string>> s_fontNames = new(
        () => FontManager.Current.SystemFonts.OrderBy(font => font.Name).Select(font => font.Name).Distinct().ToList(),
        LazyThreadSafetyMode.PublicationOnly);

    public static List<string> FontNames => s_fontNames.Value;

    /// <summary>Starts enumeration on a background thread so the first page visit doesn't pay for it.</summary>
    public static void Warm()
    {
        if (!s_fontNames.IsValueCreated)
        {
            _ = Task.Run(() => _ = s_fontNames.Value);
        }
    }
}
