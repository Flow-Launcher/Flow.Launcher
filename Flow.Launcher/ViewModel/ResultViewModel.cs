﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.IO;
using System.Drawing.Text;
using System.Collections.Generic;

namespace Flow.Launcher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        private static PrivateFontCollection fontCollection = new();
        private static Dictionary<string, string> fonts = new(); 

        public ResultViewModel(Result result, Settings settings)
        {
            if (result != null)
            {
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
                            Glyph = glyph with
                            {
                                FontFamily = fonts[fontFamilyPath]
                            };
                        }
                        else
                        {
                            fontCollection.AddFontFile(fontFamilyPath);
                            fonts[fontFamilyPath] = $"{Path.GetDirectoryName(fontFamilyPath)}/#{fontCollection.Families[^1].Name}";
                            Glyph = glyph with
                            {
                                FontFamily = fonts[fontFamilyPath]
                            };
                        }
                    }
                    else
                    {
                        Glyph = glyph;
                    }
                }
            }

            Settings = settings;
        }

        private Settings Settings { get; }

        public Visibility ShowOpenResultHotkey =>
            Settings.ShowOpenResultHotkey ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ShowIcon
        {
            get
            {
                // If both glyph and image icons are not available, it will then be the default icon
                if (!ImgIconAvailable && !GlyphAvailable)
                    return Visibility.Visible;

                // Although user can choose to use glyph icons, plugins may choose to supply only image icons.
                // In this case we ignore the setting because otherwise icons will not display as intended
                if (Settings.UseGlyphIcons && !GlyphAvailable && ImgIconAvailable)
                    return Visibility.Visible;

                return !Settings.UseGlyphIcons && ImgIconAvailable ? Visibility.Visible : Visibility.Hidden;
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

        public Visibility ShowGlyph
        {
            get
            {
                // Although user can choose to not use glyph icons, plugins may choose to supply only glyph icons.
                // In this case we ignore the setting because otherwise icons will not display as intended
                if (!Settings.UseGlyphIcons && !ImgIconAvailable && GlyphAvailable)
                    return Visibility.Visible;

                return Settings.UseGlyphIcons && GlyphAvailable ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private bool GlyphAvailable => Glyph is not null;

        private bool ImgIconAvailable => !string.IsNullOrEmpty(Result.IcoPath) || Result.Icon is not null;

        public string OpenResultModifiers => Settings.OpenResultModifiers;

        public string ShowTitleToolTip => string.IsNullOrEmpty(Result.TitleToolTip)
            ? Result.Title
            : Result.TitleToolTip;

        public string ShowSubTitleToolTip => string.IsNullOrEmpty(Result.SubTitleToolTip)
            ? Result.SubTitle
            : Result.SubTitleToolTip;

        private volatile bool ImageLoaded;

        private ImageSource image = ImageLoader.DefaultImage;

        public ImageSource Image
        {
            get
            {
                if (!ImageLoaded)
                {
                    ImageLoaded = true;
                    _ = LoadImageAsync();
                }

                return image;
            }
            private set => image = value;
        }

        public GlyphInfo Glyph { get; set; }

        private async ValueTask LoadImageAsync()
        {
            var imagePath = Result.IcoPath;
            if (string.IsNullOrEmpty(imagePath) && Result.Icon != null)
            {
                try
                {
                    image = Result.Icon();
                    return;
                }
                catch (Exception e)
                {
                    Log.Exception(
                        $"|ResultViewModel.Image|IcoPath is empty and exception when calling Icon() for result <{Result.Title}> of plugin <{Result.PluginDirectory}>",
                        e);
                }
            }

            if (ImageLoader.CacheContainImage(imagePath))
            {
                // will get here either when icoPath has value\icon delegate is null\when had exception in delegate
                image = ImageLoader.Load(imagePath);
                return;
            }

            // We need to modify the property not field here to trigger the OnPropertyChanged event
            Image = await Task.Run(() => ImageLoader.Load(imagePath)).ConfigureAwait(false);
        }

        public Result Result { get; }
        public int ResultProgress
        {
            get
            {
                if (Result.ProgressBar == null)
                    return 0;

                return Result.ProgressBar.Value;
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
