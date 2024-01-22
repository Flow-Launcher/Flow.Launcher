using System;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.IO;
using System.Drawing.Text;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Fonts.Inter;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using FontFamily = Avalonia.Media.FontFamily;

namespace Flow.Launcher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        private static PrivateFontCollection fontCollection = new();
        private static Dictionary<string, string> fonts = new();

        public ResultViewModel(Result result, Settings settings)
        {
            Settings = settings;

            if (result == null)
            {
                return;
            }

            Result = result;

            if (Result.Glyph is { FontFamily: not null } glyph)
            {
                // Checks if it's a system installed font, which does not require path to be provided. 
                if (glyph.FontFamily.EndsWith(".ttf") || glyph.FontFamily.EndsWith(".otf"))
                {
                    string fontFamilyPath = glyph.FontFamily;

                    if (!Path.IsPathRooted(fontFamilyPath))
                    {
                        fontFamilyPath = Path.Combine(Result.PluginDirectory, fontFamilyPath);
                    }

                    if (fonts.ContainsKey(fontFamilyPath))
                    {
                        Glyph = glyph with { FontFamily = fonts[fontFamilyPath] };
                    }
                    else
                    {
                        fontCollection.AddFontFile(fontFamilyPath);
                        fonts[fontFamilyPath] =
                            $"{Path.GetDirectoryName(fontFamilyPath)}/#{fontCollection.Families[^1].Name}";
                        Glyph = glyph with { FontFamily = fonts[fontFamilyPath] };
                    }
                }
                else
                {
                    Glyph = glyph;
                }
            }
        }


        private Settings Settings { get; }


        public bool ShowOpenResultHotkey => Settings.ShowOpenResultHotkey;

        public IImage DefaultImage => ImageLoader.LoadingImage;

        public bool ShowDefaultPreview => Result.PreviewPanel != null;

        public bool ShowCustomizedPreview => Result.PreviewPanel != null;

        public bool ShowIcon
        {
            get
            {
                // If both glyph and image icons are not available, it will then be the default icon
                if (!ImgIconAvailable && !GlyphAvailable)
                    return true;

                // Although user can choose to use glyph icons, plugins may choose to supply only image icons.
                // In this case we ignore the setting because otherwise icons will not display as intended
                if (Settings.UseGlyphIcons && !GlyphAvailable && ImgIconAvailable)
                    return true;

                return !Settings.UseGlyphIcons && ImgIconAvailable;
            }
        }

        public FontFamily GlyphFontFamily
        {
            get
            {
                if (Glyph is null) return FontFamily.DefaultFontFamilyName;

                if (Application.Current!.TryGetResource(Glyph.FontFamily, ThemeVariant.Default, out var fontfamily))
                {
                    return fontfamily as FontFamily;
                }

                return FontFamily.DefaultFontFamilyName;
            }
        }

        public bool ShowPreviewImage
        {
            get
            {
                if (PreviewImageAvailable)
                {
                    return true;
                }
                else
                {
                    // Fall back to icon
                    return ShowIcon;
                }
            }
        }

        public double IconRadius
        {
            get
            {
                if (Result.RoundedIcon)
                {
                    return IconXY / 2;
                }

                return IconXY;
            }
        }

        public bool ShowGlyph
        {
            get
            {
                // Although user can choose to not use glyph icons, plugins may choose to supply only glyph icons.
                // In this case we ignore the setting because otherwise icons will not display as intended
                if (!Settings.UseGlyphIcons && !ImgIconAvailable && GlyphAvailable)
                    return true;

                return Settings.UseGlyphIcons && GlyphAvailable;
            }
        }

        private bool GlyphAvailable => Glyph is not null;

        private bool ImgIconAvailable => !string.IsNullOrEmpty(Result.IcoPath) || Result.Icon is not null;

        private bool PreviewImageAvailable => !string.IsNullOrEmpty(Result.Preview.PreviewImagePath) ||
                                              Result.Preview.PreviewDelegate != null;

        public string OpenResultModifiers => Settings.OpenResultModifiers;

        public string ShowTitleToolTip => string.IsNullOrEmpty(Result.TitleToolTip)
            ? Result.Title
            : Result.TitleToolTip;

        public string ShowSubTitleToolTip => string.IsNullOrEmpty(Result.SubTitleToolTip)
            ? Result.SubTitle
            : Result.SubTitleToolTip;

        public Task<IImage> Image => LoadImageInternalAsync(Result.IcoPath, Result.Icon, false);

        public Task<IImage> PreviewImage => LoadImageInternalAsync(Result.IcoPath, Result.Icon, false);

        /// <summary>
        /// Determines if to use the full width of the preview panel
        /// </summary>
        public bool UseBigThumbnail => Result.Preview.IsMedia;

        public GlyphInfo Glyph { get; set; }

        private async Task<IImage> LoadImageInternalAsync(string imagePath, Result.IconDelegate icon,
            bool loadFullImage)
        {
            if (string.IsNullOrEmpty(imagePath) && icon != null)
            {
                try
                {
                    // var image = icon();
                    return default; // image;
                }
                catch (Exception e)
                {
                    Log.Exception(
                        $"|ResultViewModel.LoadImageInternalAsync|IcoPath is empty and exception when calling IconDelegate for result <{Result.Title}> of plugin <{Result.PluginDirectory}>",
                        e);
                }
            }

            return await ImageLoader.LoadAsync(imagePath, loadFullImage).ConfigureAwait(false);
        }



        public Result Result { get; }

        public int ResultProgress
        {
            get
            {
                return Result.ProgressBar ?? 0;
            }
        }

        public string QuerySuggestionText { get; set; }

        public double IconXY { get; set; } = 32;

        public override bool Equals(object obj)
        {
            return obj is ResultViewModel r && Result.Equals(r.Result);
        }

        public override int GetHashCode()
        {
            return Result.GetHashCode();
        }

        public override string ToString()
        {
            return Result.ToString();
        }
    }
}
