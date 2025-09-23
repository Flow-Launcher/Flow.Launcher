using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomExplorerViewModel : BaseModel
    {
        public string Name { get; set; }
        [JsonIgnore]
        public string DisplayName => Name == "Explorer" ? Localize.fileManagerExplorer(): Name;
        public string Path { get; set; }
        public string FileArgument { get; set; } = "\"%d\"";
        public string DirectoryArgument { get; set; } = "\"%d\"";
        public bool Editable { get; init; } = true;

        public CustomExplorerViewModel Copy()
        {
            return new CustomExplorerViewModel
            {
                Name = Name,
                Path = Path,
                FileArgument = FileArgument,
                DirectoryArgument = DirectoryArgument,
                Editable = Editable
            };
        }

        public void OnDisplayNameChanged()
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
