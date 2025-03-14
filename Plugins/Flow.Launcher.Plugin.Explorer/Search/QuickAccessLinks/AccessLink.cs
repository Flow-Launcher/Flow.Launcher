using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    public class AccessLink
    {
        public string Path { get; set; }

        public ResultType Type { get; set; } = ResultType.Folder;

        [JsonIgnore]
        public string Name
        {
            get
            {
                var path = Path.EndsWith(Constants.DirectorySeparator) ? Path[0..^1] : Path;

                if (path.EndsWith(':'))
                    return path[0..^1] + " Drive";

                return path.Split(new[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.None)
                           .Last();
            }
        }
    }
    
}
