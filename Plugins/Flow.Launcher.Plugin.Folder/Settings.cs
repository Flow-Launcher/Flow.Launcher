using System.Collections.Generic;
using Newtonsoft.Json;
using Flow.Launcher.Infrastructure.Storage;

namespace Flow.Launcher.Plugin.Folder
{
    public class Settings
    {
        [JsonProperty]
        public List<FolderLink> FolderLinks { get; set; } = new List<FolderLink>();
    }
}
