using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Sys;

public class Command : BaseModel
{
    public string Key { get; set; }

    private string name;
    [JsonIgnore]
    public string Name
    {
        get => name;
        set
        {
            name = value;
            OnPropertyChanged();
        }
    }

    private string description;
    [JsonIgnore]
    public string Description
    {
        get => description;
        set
        {
            description = value;
            OnPropertyChanged();
        }
    }

    private string keyword;
    public string Keyword
    {
        get => keyword;
        set
        {
            keyword = value;
            OnPropertyChanged();
        }
    }
}
