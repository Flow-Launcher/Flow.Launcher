using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Explorer.Search.FolderLinks
{
    public class FolderLink
    {
        public string Path { get; set; }

        [JsonIgnore]
        public string Nickname
        {
            get
            {
                var path = Path.EndsWith(Constants.DirectorySeperator) ? Path[0..^1] : Path;

                if (path.EndsWith(':'))
                    return path[0..^1] + " Drive";

                return path.Split(new[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.None)
                            .Last()
                            + " (" + System.IO.Path.GetDirectoryName(Path) + ")";
            }
        }
    }
    
}
