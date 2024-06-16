using System.Linq;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Plugin;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Core.Plugin;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Resource;
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

        private string PluginManagerActionKeyword
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


        private async void LoadIconAsync()
        {
            Image = await ImageLoader.LoadAsync(PluginPair.Metadata.IcoPath);
        }

        public ImageSource Image
        {
            get
            {
                if (_image == ImageLoader.MissingImage)
                    LoadIconAsync();

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

        private Control _settingControl;
        private bool _isExpanded;

        private Control _bottomPart1;
        public Control BottomPart1 => IsExpanded ? _bottomPart1 ??= new InstalledPluginDisplayKeyword() : null;

        private Control _bottomPart2;
        public Control BottomPart2 => IsExpanded ? _bottomPart2 ??= new InstalledPluginDisplayBottomData() : null;

        public bool HasSettingControl => PluginPair.Plugin is ISettingProvider;
        public Control SettingControl
            => IsExpanded
                ? _settingControl
                    ??= PluginPair.Plugin is not ISettingProvider settingProvider
                        ? null
                        : settingProvider.CreateSettingPanel()
                : null;
        private ImageSource _image = ImageLoader.MissingImage;

        public Visibility ActionKeywordsVisibility => PluginPair.Metadata.ActionKeywords.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
        public string InitilizaTime => PluginPair.Metadata.InitTime + "ms";
        public string QueryTime => PluginPair.Metadata.AvgQueryTime + "ms";
        public string Version => InternationalizationManager.Instance.GetTranslation("plugin_query_version") + " " + PluginPair.Metadata.Version;
        public string InitAndQueryTime => InternationalizationManager.Instance.GetTranslation("plugin_init_time") + " " + PluginPair.Metadata.InitTime + "ms, " + InternationalizationManager.Instance.GetTranslation("plugin_query_time") + " " + PluginPair.Metadata.AvgQueryTime + "ms";
        public string ActionKeywordsText => string.Join(Query.ActionKeywordSeparator, PluginPair.Metadata.ActionKeywords);
        public int Priority => PluginPair.Metadata.Priority;
        public Infrastructure.UserSettings.Plugin PluginSettingsObject { get; set; }

        public void ChangeActionKeyword(string newActionKeyword, string oldActionKeyword)
        {
            PluginManager.ReplaceActionKeyword(PluginPair.Metadata.ID, oldActionKeyword, newActionKeyword);
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
                PluginManager.API.OpenDirectory(directory);
        }

        [RelayCommand]
        private void OpenSourceCodeLink()
        {
            PluginManager.API.OpenUrl(PluginPair.Metadata.Website);
        }

        [RelayCommand]
        private void OpenDeletePluginWindow()
        {
            PluginManager.API.ChangeQuery($"{PluginManagerActionKeyword} uninstall {PluginPair.Metadata.Name}".Trim(), true);
            PluginManager.API.ShowMainWindow();
        }

        public static bool IsActionKeywordRegistered(string newActionKeyword) => PluginManager.ActionKeywordRegistered(newActionKeyword);

        [RelayCommand]
        private void SetActionKeywords()
        {
            ActionKeywords changeKeywordsWindow = new ActionKeywords(this);
            changeKeywordsWindow.ShowDialog();
        }
    }

}
