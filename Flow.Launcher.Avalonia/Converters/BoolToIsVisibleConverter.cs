using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Flow.Launcher.Avalonia.Converters;

/// <summary>
/// Converts a boolean value to IsVisible (Avalonia uses bool for visibility, not Visibility enum)
/// </summary>
public class BoolToIsVisibleConverter : IValueConverter
{
    /// <summary>
    /// If true, inverts the boolean value (true becomes false, false becomes true)
    /// </summary>
    public bool Invert { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return Invert ? !boolValue : boolValue;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return Invert ? !boolValue : boolValue;
        }
        return false;
    }
}
