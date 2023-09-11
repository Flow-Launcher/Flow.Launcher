using System.Text.Json.Serialization;

namespace Flow.Launcher.Core.Plugin
{

    [JsonSerializable(typeof(JsonRPCQueryResponseModel))]
    public partial class JsonRPCQueryResponseModelContext : JsonSerializerContext
    {
    }
    
    [JsonSerializable(typeof(JsonRPCRequestModel))]
    public partial class JsonRPCRequestModelContext : JsonSerializerContext
    {
    }
    
    [JsonSerializable(typeof(JsonRPCClientRequestModel))]
    public partial class JsonRPCClientRequestModelContext : JsonSerializerContext
    {
    }
}
