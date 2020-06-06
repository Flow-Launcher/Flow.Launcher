using Newtonsoft.Json;
using System;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.FolderLinks
{
    [JsonObject(MemberSerialization.OptIn)]
    public class FolderLink
    {
        [JsonProperty]
        public string Path { get; set; }

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
