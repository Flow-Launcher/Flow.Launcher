namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Text with FontFamily specified
    /// </summary>
    /// <param name="FontFamily">Font Family of this Glyph</param>
    /// <param name="Glyph">Text/Unicode of the Glyph</param>
    public record GlyphInfo(string FontFamily, string Glyph);
}
