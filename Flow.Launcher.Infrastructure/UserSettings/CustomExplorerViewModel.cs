using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
