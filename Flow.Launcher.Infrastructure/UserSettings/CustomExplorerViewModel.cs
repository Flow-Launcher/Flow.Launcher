using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class CustomExplorerViewModel : BaseModel
    {
        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        public string Name { get; set; }
        [JsonIgnore]
        public string DisplayName => Name == "Explorer" ? API.GetTranslation("fileManagerExplorer") : Name;
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
