using System.Text.Json.Serialization;

namespace Flow.Launcher.Core.Plugin
{
    // TODO: After Upgrading to .Net 7, adding Source Generating Context for IAsyncEnumerable JsonRPCMessage

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
