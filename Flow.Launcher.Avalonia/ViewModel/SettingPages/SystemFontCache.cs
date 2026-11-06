using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

/// <summary>
/// Sorted, distinct system font family names, enumerated once per process and shared by all settings pages.
/// </summary>
public static class SystemFontCache
{
    private static readonly Lazy<IReadOnlyList<string>> s_fontNames = new(
        () => FontManager.Current.SystemFonts.OrderBy(font => font.Name).Select(font => font.Name).Distinct().ToList());

    public static IReadOnlyList<string> FontNames => s_fontNames.Value;
}
