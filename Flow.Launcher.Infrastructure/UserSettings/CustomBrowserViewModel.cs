using Flow.Launcher.Plugin;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomBrowserViewModel : BaseModel
    {
        public string Name { get; set; }
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
    }
}



