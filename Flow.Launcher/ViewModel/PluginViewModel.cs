using System.Linq;
using System.Collections.Generic;
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

        private static string PluginManagerActionKeyword
        {
            get
            {
                var keyword = PluginManager
                    .GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7")
                    .Metadata.ActionKeywords.FirstOrDefault();
                return keyword switch
                {
                    null or "*" => string.Empty,
                    _ => keyword
                };
            }
        }

        private async Task LoadIconAsync()
        {
            Image = await ImageLoader.LoadAsync(PluginPair.Metadata.IcoPath);
            OnPropertyChanged(nameof(Image));
        }

        public ImageSource Image
        {
            get
            {
                if (_image == ImageLoader.MissingImage)
                    _ = LoadIconAsync();

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

        public IEnumerable<int> PluginSearchDelayRange { get; } =
            Ioc.Default.GetRequiredService<Settings>().PluginSearchDelayRange;

        public int PluginSearchDelay
        {
            get => PluginPair.Metadata.SearchDelay;
            set
            {
                PluginPair.Metadata.SearchDelay = value;
                PluginSettingsObject.SearchDelay = value;
            }
        }

        private Control _settingControl;
        private bool _isExpanded;

        private Control _bottomPart1;
        public Control BottomPart1 => IsExpanded ? _bottomPart1 ??= new InstalledPluginDisplayKeyword() : null;

        private Control _bottomPart2;
        public Control BottomPart2 => IsExpanded ? _bottomPart2 ??= new InstalledPluginSearchDelay() : null;

        private Control _bottomPart3;
        public Control BottomPart3 => IsExpanded ? _bottomPart3 ??= new InstalledPluginDisplayBottomData() : null;

        public bool HasSettingControl => PluginPair.Plugin is ISettingProvider && (PluginPair.Plugin is not JsonRPCPluginBase jsonRPCPluginBase || jsonRPCPluginBase.NeedCreateSettingPanel());
        public Control SettingControl
            => IsExpanded
                ? _settingControl
                    ??= HasSettingControl
                        ? ((ISettingProvider)PluginPair.Plugin).CreateSettingPanel()
                        : null
                : null;
        private ImageSource _image = ImageLoader.MissingImage;

        public Visibility ActionKeywordsVisibility => PluginPair.Metadata.HideActionKeywordPanel ?
            Visibility.Collapsed : Visibility.Visible;
        public string InitilizaTime => PluginPair.Metadata.InitTime + "ms";
        public string QueryTime => PluginPair.Metadata.AvgQueryTime + "ms";
        public string Version => App.API.GetTranslation("plugin_query_version") + " " + PluginPair.Metadata.Version;
        public string InitAndQueryTime =>
            App.API.GetTranslation("plugin_init_time") + " " +
            PluginPair.Metadata.InitTime + "ms, " +
            App.API.GetTranslation("plugin_query_time") + " " +
            PluginPair.Metadata.AvgQueryTime + "ms";
        public string ActionKeywordsText => string.Join(Query.ActionKeywordSeparator, PluginPair.Metadata.ActionKeywords);
        public int Priority => PluginPair.Metadata.Priority;
        public Infrastructure.UserSettings.Plugin PluginSettingsObject { get; set; }

        public void OnActionKeywordsChanged()
        {
            OnPropertyChanged(nameof(ActionKeywordsText));
        }

        public void ChangePriority(int newPriority)
        {
            PluginPair.Metadata.Priority = newPriority;
            PluginSettingsObject.Priority = newPriority;
            OnPropertyChanged(nameof(Priority));
        }

        [RelayCommand]
        private void EditPluginPriority()
        {
            PriorityChangeWindow priorityChangeWindow = new PriorityChangeWindow(PluginPair. Metadata.ID, this);
            priorityChangeWindow.ShowDialog();
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
        private void OpenDeletePluginWindow()
        {
            App.API.ChangeQuery($"{PluginManagerActionKeyword} uninstall {PluginPair.Metadata.Name}".Trim(), true);
            App.API.ShowMainWindow();
        }

        [RelayCommand]
        private void SetActionKeywords()
        {
            ActionKeywords changeKeywordsWindow = new ActionKeywords(this);
            changeKeywordsWindow.ShowDialog();
        }
    }
}
