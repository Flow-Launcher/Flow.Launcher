using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Sys
{
    public class Command : BaseModel
    {
        public string Key { get; set; }

        [JsonIgnore]
        public string Name { get; set; }

        [JsonIgnore]
        public string Description { get; set; }

        public string Keyword { get; set; }
    }
}
