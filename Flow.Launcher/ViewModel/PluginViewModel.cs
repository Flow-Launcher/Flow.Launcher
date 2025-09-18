using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Resources.Controls;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginViewModel : BaseModel
    {
        private static readonly string ClassName = nameof(PluginViewModel);

        private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

        private static readonly Thickness SettingPanelMargin = (Thickness)Application.Current.FindResource("SettingPanelMargin");
        private static readonly Thickness SettingPanelItemTopBottomMargin = (Thickness)Application.Current.FindResource("SettingPanelItemTopBottomMargin");

        private readonly PluginPair _pluginPair;
        public PluginPair PluginPair
        {
            get => _pluginPair;
            init
            {
                _pluginPair = value;
                value.Metadata.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(PluginPair.Metadata.AvgQueryTime))
                        OnPropertyChanged(nameof(QueryTime));
                };
            }
        }

        private async Task LoadIconAsync()
        {
            Image = await App.API.LoadImageAsync(PluginPair.Metadata.IcoPath);
            OnPropertyChanged(nameof(Image));
        }

        private bool _imageLoaded = false;

        public ImageSource Image
        {
            get
            {
                if (!_imageLoaded)
                {
                    _imageLoaded = true;
                    _ = LoadIconAsync();
                }

                return _image;
            }
            set => _image = value;
        }

        public bool PluginState
        {
            get => !PluginPair.Metadata.Disabled;
            set
            {
                PluginPair.Metadata.Disabled = !value;
                PluginSettingsObject.Disabled = !value;
            }
        }

        public bool PluginHomeState
        {
            get => !PluginPair.Metadata.HomeDisabled;
            set
            {
                PluginPair.Metadata.HomeDisabled = !value;
                PluginSettingsObject.HomeDisabled = !value;
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SettingControl));
            }
        }

        public int Priority
        {
            get => PluginPair.Metadata.Priority;
            set
            {
                PluginPair.Metadata.Priority = value;
                PluginSettingsObject.Priority = value;
            }
        }

        public double PluginSearchDelayTime
        {
            get => PluginPair.Metadata.SearchDelayTime == null ?
                double.NaN :
                PluginPair.Metadata.SearchDelayTime.Value;
            set
            {
                if (double.IsNaN(value))
                {
                    PluginPair.Metadata.SearchDelayTime = null;
                    PluginSettingsObject.SearchDelayTime = null;
                }
                else
                {
                    PluginPair.Metadata.SearchDelayTime = (int)value;
                    PluginSettingsObject.SearchDelayTime = (int)value;
                }
            }
        }

        private Control _settingControl;
        private bool _isExpanded;

        private Control _bottomPart1;
        public Control BottomPart1 => IsExpanded ? _bottomPart1 ??= new InstalledPluginDisplayKeyword() : null;

        private Control _bottomPart2;
        public Control BottomPart2 => IsExpanded ? _bottomPart2 ??= new InstalledPluginDisplayBottomData() : null;

        public bool HasSettingControl => PluginPair.Plugin is ISettingProvider &&
            (PluginPair.Plugin is not JsonRPCPluginBase jsonRPCPluginBase || jsonRPCPluginBase.NeedCreateSettingPanel());
        public Control SettingControl
            => IsExpanded
                ? _settingControl
                    ??= HasSettingControl
                        ? TryCreateSettingPanel(PluginPair)
                        : null
                : null;
        private ImageSource _image = ImageLoader.MissingImage;

        private static Control TryCreateSettingPanel(PluginPair pair)
        {
            try
            {
                // We can safely cast here as we already check this in HasSettingControl
                return ((ISettingProvider)pair.Plugin).CreateSettingPanel();
            }
            catch (Exception e)
            {
                // Log exception
                App.API.LogException(ClassName, $"Failed to create setting panel for {pair.Metadata.Name}", e);

                // Show error message in UI
                var errorMsg = string.Format(App.API.GetTranslation("errorCreatingSettingPanel"),
                    pair.Metadata.Name, Environment.NewLine, e.Message);
                return CreateErrorSettingPanel(errorMsg);
            }
        }

        public Visibility ActionKeywordsVisibility => PluginPair.Metadata.HideActionKeywordPanel ?
            Visibility.Collapsed : Visibility.Visible;
        public string InitializeTime => PluginPair.Metadata.InitTime + "ms";
        public string QueryTime => PluginPair.Metadata.AvgQueryTime + "ms";
        public string Version => App.API.GetTranslation("plugin_query_version") + " " + PluginPair.Metadata.Version;
        public string InitAndQueryTime =>
            App.API.GetTranslation("plugin_init_time") + " " +
            PluginPair.Metadata.InitTime + "ms, " +
            App.API.GetTranslation("plugin_query_time") + " " +
            PluginPair.Metadata.AvgQueryTime + "ms";
        public string ActionKeywordsText => string.Join(Query.ActionKeywordSeparator, PluginPair.Metadata.ActionKeywords);
        public string SearchDelayTimeText => PluginPair.Metadata.SearchDelayTime == null ?
            App.API.GetTranslation("default") :
            App.API.GetTranslation($"SearchDelayTime{PluginPair.Metadata.SearchDelayTime}");
        public Infrastructure.UserSettings.Plugin PluginSettingsObject{ get; init; }
        public bool SearchDelayEnabled => Settings.SearchQueryResultsWithDelay;
        public string DefaultSearchDelay => Settings.SearchDelayTime.ToString();
        public bool HomeEnabled => Settings.ShowHomePage && PluginManager.IsHomePlugin(PluginPair.Metadata.ID);

        public void OnActionKeywordsTextChanged()
        {
            OnPropertyChanged(nameof(ActionKeywordsText));
        }

        [RelayCommand]
        private void OpenPluginDirectory()
        {
            var directory = PluginPair.Metadata.PluginDirectory;
            if (!string.IsNullOrEmpty(directory))
                App.API.OpenDirectory(directory);
        }

        [RelayCommand]
        private void OpenSourceCodeLink()
        {
            App.API.OpenUrl(PluginPair.Metadata.Website);
        }

        [RelayCommand]
        private async Task OpenDeletePluginWindowAsync()
        {
            await PluginInstaller.UninstallPluginAndCheckRestartAsync(PluginPair.Metadata);
        }

        [RelayCommand]
        private void SetActionKeywords()
        {
            var changeKeywordsWindow = new ActionKeywords(this);
            changeKeywordsWindow.ShowDialog();
        }

        private static UserControl CreateErrorSettingPanel(string text)
        {
            var grid = new Grid()
            {
                Margin = SettingPanelMargin
            };
            var textBox = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
                Margin = SettingPanelItemTopBottomMargin
            };
            textBox.SetResourceReference(TextBox.ForegroundProperty, "Color04B");
            grid.Children.Add(textBox);
            return new UserControl
            {
                Content = grid
            };
        }
    }
}
