using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomBrowserViewModel : BaseModel
    {
        public string Name { get; set; }
        [JsonIgnore]
        public string DisplayName => Name == "Default" ? Localize.defaultBrowser_default() : Name;
        public string Path { get; set; }
        public string PrivateArg { get; set; }
        public bool EnablePrivate { get; set; }
        public bool OpenInTab { get; set; } = true;
        [JsonIgnore]
        public bool OpenInNewWindow => !OpenInTab;
        public bool Editable { get; set; } = true;

        public CustomBrowserViewModel Copy()
        {
            return new CustomBrowserViewModel
            {
                Name = Name,
                Path = Path,
                OpenInTab = OpenInTab,
                PrivateArg = PrivateArg,
                EnablePrivate = EnablePrivate,
                Editable = Editable
            };
        }

        public void OnDisplayNameChanged()
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
