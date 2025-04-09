using System;

namespace Flow.Launcher.Plugin.SharedModels;

/// <summary>
/// Theme data model
/// </summary>
public class ThemeData
{
    /// <summary>
    /// Theme file name without extension
    /// </summary>
    public string FileNameWithoutExtension { get; private init; }

    /// <summary>
    /// Theme name
    /// </summary>
    public string Name { get; private init; }

    /// <summary>
    /// Indicates whether the theme supports dark mode
    /// </summary>
    public bool? IsDark { get; private init; }

    /// <summary>
    /// Indicates whether the theme supports blur effects
    /// </summary>
    public bool? HasBlur { get; private init; }

    /// <summary>
    /// Theme data constructor
    /// </summary>
    public ThemeData(string fileNameWithoutExtension, string name, bool? isDark = null, bool? hasBlur = null)
    {
        FileNameWithoutExtension = fileNameWithoutExtension;
        Name = name;
        IsDark = isDark;
        HasBlur = hasBlur;
    }

    /// <inheritdoc />
    public static bool operator ==(ThemeData left, ThemeData right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    /// <inheritdoc />
    public static bool operator !=(ThemeData left, ThemeData right)
    {
        return !(left == right);
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        if (obj is not ThemeData other)
            return false;
        return FileNameWithoutExtension == other.FileNameWithoutExtension &&
            Name == other.Name;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(FileNameWithoutExtension, Name);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
