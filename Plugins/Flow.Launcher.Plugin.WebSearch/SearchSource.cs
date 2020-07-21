using System.IO;
using System.Windows.Media;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Flow.Launcher.Infrastructure.Image;

namespace Flow.Launcher.Plugin.WebSearch
{
    public class SearchSource : BaseModel
    {
        public const string DefaultIcon = "web_search.png";
        public string Title { get; set; }
        public string ActionKeyword { get; set; }

        [NotNull]
        public string Icon { get; set; } = DefaultIcon;

        /// <summary>
        /// Default icons are placed in Images directory in the app location. 
        /// Custom icons are placed in the user data directory
        /// </summary>
        [NotNull]
        public string IconPath 
        { 
            get
            {
                if (string.IsNullOrEmpty(iconPath))
                    return Path.Combine(Main.ImagesDirectory, Icon);

                return iconPath;
            }
            set
            {
                iconPath = value;
            }
        }

        private string iconPath;

        [JsonIgnore]
        public ImageSource Image => ImageLoader.Load(IconPath);

        public string Url { get; set; }
        public bool Enabled { get; set; }

        public SearchSource DeepCopy()
        {
            var webSearch = new SearchSource
            {
                Title = string.Copy(Title),
                ActionKeyword = string.Copy(ActionKeyword),
                Url = string.Copy(Url),
                Icon = string.Copy(Icon),
                Enabled = Enabled
            };
            return webSearch;
        }
    }
}