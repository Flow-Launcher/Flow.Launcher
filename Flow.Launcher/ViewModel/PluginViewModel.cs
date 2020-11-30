using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Core.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class PluginViewModel : BaseModel
    {
        public PluginPair PluginPair { get; set; }

        private readonly Internationalization _translator = InternationalizationManager.Instance;

        public ImageSource Image => ImageLoader.Load(PluginPair.Metadata.IcoPath);
        public bool PluginState
        {
            get { return !PluginPair.Metadata.Disabled; }
            set 
            {
                PluginPair.Metadata.Disabled = !value; 
            }
        }
        public Visibility ActionKeywordsVisibility => PluginPair.Metadata.ActionKeywords.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
        public string InitilizaTime => PluginPair.Metadata.InitTime.ToString() + "ms";
        public string QueryTime => PluginPair.Metadata.AvgQueryTime + "ms";
        public string ActionKeywordsText => string.Join(Query.ActionKeywordSeperater, PluginPair.Metadata.ActionKeywords);

        public void ChangeActionKeyword(string newActionKeyword, string oldActionKeyword)
        {
            PluginManager.ReplaceActionKeyword(PluginPair.Metadata.ID, oldActionKeyword, newActionKeyword);
            
            OnPropertyChanged(nameof(ActionKeywordsText));
        }

    }
}
