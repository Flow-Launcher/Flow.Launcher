using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class CustomExplorerViewModel : BaseModel
    {
        public string Name { get; set; }
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
    }
}
