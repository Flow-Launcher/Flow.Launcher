using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        public class LazyAsync<T> : Lazy<Task<T>>
        {
            private T defaultValue;

            private readonly Action _updateCallback;
            public new T Value
            {
                get
                {
                    if (!IsValueCreated)
                    {
                        base.Value.ContinueWith(_ =>
                        {
                            _updateCallback();
                        });

                        return defaultValue;
                    }
                    
                    if (!base.Value.IsCompleted || base.Value.IsFaulted)
                        return defaultValue;

                    return base.Value.Result;
                }
            }
            public LazyAsync(Func<Task<T>> factory, T defaultValue, Action updateCallback) : base(factory)
            {
                if (defaultValue != null)
                {
                    this.defaultValue = defaultValue;
                }

                _updateCallback = updateCallback;
            }
        }

        public ResultViewModel(Result result, Settings settings)
        {
            if (result != null)
            {
                Result = result;

                Image = new LazyAsync<ImageSource>(
                            SetImage, 
                            ImageLoader.DefaultImage,
                            () =>
                                {
                                    OnPropertyChanged(nameof(Image));
                                });
            }

            Settings = settings;
        }

        public Settings Settings { get; private set; }

        public Visibility ShowOpenResultHotkey => Settings.ShowOpenResultHotkey ? Visibility.Visible : Visibility.Hidden;

        public string OpenResultModifiers => Settings.OpenResultModifiers;

        public string ShowTitleToolTip => string.IsNullOrEmpty(Result.TitleToolTip)
                                            ? Result.Title
                                            : Result.TitleToolTip;

        public string ShowSubTitleToolTip => string.IsNullOrEmpty(Result.SubTitleToolTip)
                                                ? Result.SubTitle
                                                : Result.SubTitleToolTip;

        public LazyAsync<ImageSource> Image { get; set; }

        private async Task<ImageSource> SetImage()
        {
            var imagePath = Result.IcoPath;
            if (string.IsNullOrEmpty(imagePath) && Result.Icon != null)
            {
                try
                {
                    return Result.Icon();
                }
                catch (Exception e)
                {
                    Log.Exception($"|ResultViewModel.Image|IcoPath is empty and exception when calling Icon() for result <{Result.Title}> of plugin <{Result.PluginDirectory}>", e);
                    imagePath = Constant.MissingImgIcon;
                }
            }

            if (ImageLoader.CacheContainImage(imagePath))
            {
                // will get here either when icoPath has value\icon delegate is null\when had exception in delegate
                return ImageLoader.Load(imagePath);
            }
            else
            {
                return await Task.Run(() => ImageLoader.Load(imagePath));
            }
        }

        public Result Result { get; }

        public override bool Equals(object obj)
        {
            var r = obj as ResultViewModel;
            if (r != null)
            {
                return Result.Equals(r.Result);
            }
            else
            {
                return false;
            }
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
