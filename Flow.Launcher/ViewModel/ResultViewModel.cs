﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.IO;

namespace Flow.Launcher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
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
                        var fontPath = Result.Glyph.FontFamily;
                        Glyph = Path.IsPathRooted(fontPath)
                            ? Result.Glyph
                            : Result.Glyph with
                            {
                                FontFamily = Path.Combine(Result.PluginDirectory, fontPath)
                            };
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
            Settings.ShowOpenResultHotkey ? Visibility.Visible : Visibility.Hidden;

        public Visibility ShowIcon => Result.IcoPath != null || Result.Icon is not null || Glyph == null
            ? Visibility.Visible
            : Visibility.Hidden;

        public Visibility ShowGlyph => Glyph is not null ? Visibility.Visible : Visibility.Hidden;
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
