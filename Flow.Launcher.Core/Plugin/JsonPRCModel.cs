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

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;
using System.Text.Json;

namespace Flow.Launcher.Core.Plugin
{
    public record JsonRPCBase(int Id, JsonRPCErrorModel Error = default);
    public record JsonRPCErrorModel(int Code, string Message, string Data);

    public record JsonRPCResponseModel(int Id, JsonRPCErrorModel Error = default) : JsonRPCBase(Id, Error);
    public record JsonRPCQueryResponseModel(int Id,
        [property: JsonPropertyName("result")] List<JsonRPCResult> Result,
        IReadOnlyDictionary<string, object> SettingsChange = null,
        string DebugMessage = "",
        JsonRPCErrorModel Error = default) : JsonRPCResponseModel(Id, Error);

    public record JsonRPCRequestModel(int Id,
        string Method,
        object[] Parameters,
        IReadOnlyDictionary<string, object> Settings = default,
        JsonRPCErrorModel Error = default) : JsonRPCBase(Id, Error);


    /// <summary>
    /// Json RPC Request(in query response) that client sent to Flow Launcher
    /// </summary>
    public record JsonRPCClientRequestModel(
        int Id,
        string Method,
        object[] Parameters,
        IReadOnlyDictionary<string, object> Settings,
        bool DontHideAfterAction = false,
        JsonRPCErrorModel Error = default) : JsonRPCRequestModel(Id, Method, Parameters, Settings, Error);
    
    
    /// <summary>
    /// Represent the json-rpc result item that client send to Flow Launcher
    /// Typically, we will send back this request model to client after user select the result item
    /// But if the request method starts with "Flow Launcher.", we will invoke the public APIs we expose.
    /// </summary>
    public class JsonRPCResult : Result
    {
        public JsonRPCClientRequestModel JsonRPCAction { get; set; }

        public Dictionary<string, object> SettingsChange { get; set; }
    }
}
