using Newtonsoft.Json;
using System;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickFolderLinks
{
    [JsonObject(MemberSerialization.OptIn)]
    public class FolderLink
    {
        [JsonProperty]
        public string Path { get; set; }

        public string Nickname =>
           Path.Split(new[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.None)
               .Last()
           + " (" + System.IO.Path.GetDirectoryName(Path) + ")";
    }
}
