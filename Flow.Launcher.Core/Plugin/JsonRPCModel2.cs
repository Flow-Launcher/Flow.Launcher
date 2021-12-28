using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Core.Plugin
{

    public record JsonRPCRequest : JsonRPCBase
    {
        public string Method { get; init; }
        public object[] Parameters { get; init; }
    }

    public record JsonRPCResponse : JsonRPCBase
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JsonRPCResopnseActionType ActionType { get; init; }
        
        public List<JsonRPCResult> Result { get; init; }
        
        public string MethodName { get; init; }
    }

    public enum JsonRPCResopnseActionType
    {
        Result,
        RequestAPI
    }
}