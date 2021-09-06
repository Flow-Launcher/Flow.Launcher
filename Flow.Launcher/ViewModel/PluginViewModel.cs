using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Plugin;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Core.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class PluginViewModel : BaseModel
    {
        public PluginPair PluginPair { get; set; }

        public ImageSource Image => ImageLoader.Load(PluginPair.Metadata.IcoPath);
        public bool PluginState
        {
            get { return !PluginPair.Metadata.Disabled; }
            set 
            {
                PluginPair.Metadata.Disabled = !value; 
            }
        }
        public Visibility ActionKeywordsVisibility => PluginPair.Metadata.ActionKeywords.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
        public string InitilizaTime => PluginPair.Metadata.InitTime.ToString() + "ms";
        public string QueryTime => PluginPair.Metadata.AvgQueryTime + "ms";
        public string ActionKeywordsText => string.Join(Query.ActionKeywordSeparator, PluginPair.Metadata.ActionKeywords);
        public int Priority => PluginPair.Metadata.Priority;

        public void ChangeActionKeyword(string newActionKeyword, string oldActionKeyword)
        {
            PluginManager.ReplaceActionKeyword(PluginPair.Metadata.ID, oldActionKeyword, newActionKeyword);
            
            OnPropertyChanged(nameof(ActionKeywordsText));
        }

        public void ChangePriority(int newPriority)
        {
            PluginPair.Metadata.Priority = newPriority;
            OnPropertyChanged(nameof(Priority));
        }

        public bool IsActionKeywordRegistered(string newActionKeyword) => PluginManager.ActionKeywordRegistered(newActionKeyword);
    }
}
