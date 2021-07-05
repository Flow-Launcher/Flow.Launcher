/* We basically follow the Json-RPC 2.0 spec (http://www.jsonrpc.org/specification) to invoke methods between Flow Launcher and other plugins, 
 * like python or other self-execute program. But, we added addtional infos (proxy and so on) into rpc request. Also, we didn't use the
 * "id" and "jsonrpc" in the request, since it's not so useful in our request model.
 * 
 * When execute a query:
 *      Flow Launcher -------JsonRPCServerRequestModel--------> client
 *      Flow Launcher <------JsonRPCQueryResponseModel--------- client
 *      
 * When execute a action (which mean user select an item in reulst item):
 *      Flow Launcher -------JsonRPCServerRequestModel--------> client
 *      Flow Launcher <------JsonRPCResponseModel-------------- client
 * 
 */

using Flow.Launcher.Core.Resource;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;
using System.Text.Json;

namespace Flow.Launcher.Core.Plugin
{
    public class JsonRPCErrorModel
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }
    }


    public class JsonRPCResponseModel
    {
        public string Result { get; set; }

        public JsonRPCErrorModel Error { get; set; }
    }

    public class JsonRPCQueryResponseModel : JsonRPCResponseModel
    {
        [JsonPropertyName("result")]
        public new List<JsonRPCResult> Result { get; set; }

        public string DebugMessage { get; set; }
    }
    
    public class JsonRPCRequestModel
    {
        public string Method { get; set; }

        public object[] Parameters { get; set; }

        private static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        public override string ToString()
        {
            return JsonSerializer.Serialize(this, options);
        }
    }

    /// <summary>
    /// Json RPC Request that Flow Launcher sent to client
    /// </summary>
    public class JsonRPCServerRequestModel : JsonRPCRequestModel
    {

    }

    /// <summary>
    /// Json RPC Request(in query response) that client sent to Flow Launcher
    /// </summary>
    public class JsonRPCClientRequestModel : JsonRPCRequestModel
    {
        public bool DontHideAfterAction { get; set; }
    }

    /// <summary>
    /// Represent the json-rpc result item that client send to Flow Launcher
    /// Typically, we will send back this request model to client after user select the result item
    /// But if the request method starts with "Flow Launcher.", we will invoke the public APIs we expose.
    /// </summary>
    public class JsonRPCResult : Result
    {
        public JsonRPCClientRequestModel JsonRPCAction { get; set; }
    }
}