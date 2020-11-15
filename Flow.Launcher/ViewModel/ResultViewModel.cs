using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;


namespace Flow.Launcher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        public ResultViewModel(Result result, Settings settings)
        {
            if (result != null)
            {
                Result = result;
                Image = new Lazy<ImageSource>(() => SetImage);
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

        public Lazy<ImageSource> Image { get; set; }

        private ImageSource SetImage
        {
            get
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
                    // will get here either when icoPath has value\icon delegate is null\when had exception in delegate
                    return ImageLoader.Load(imagePath);
                else
                {
                    Task.Run(() =>
                    {
                        Image = new Lazy<ImageSource>(() => ImageLoader.Load(imagePath));
                        OnPropertyChanged(nameof(Image));
                    });

                    return ImageLoader.LoadDefault();
                }
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
