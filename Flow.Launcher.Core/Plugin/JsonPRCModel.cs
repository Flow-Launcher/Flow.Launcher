﻿
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
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public class JsonRPCErrorModel
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }
    }

    public class JsonRPCModelBase
    {
        public int Id { get; set; }
    }

    public class JsonRPCResponseModel : JsonRPCModelBase
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

    public class JsonRPCRequestModel : JsonRPCModelBase
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("parameters")]
        public object[] Parameters { get; set; }

        public override string ToString()
        {
            string rpc = string.Empty;
            if (Parameters != null && Parameters.Length > 0)
            {
                string parameters = $"[{string.Join(',', Parameters.Select(GetParameterByType))}]";
                rpc = $@"{{\""method\"":\""{Method}\"",\""parameters\"":{parameters}";
            }
            else
            {
                rpc = $@"{{\""method\"":\""{Method}\"",\""parameters\"":[]";
            }

            return rpc;

        }

        private string GetParameterByType(object parameter)
        => parameter switch
        {
            null => "null",
            string _ => $@"\""{ReplaceEscapes(parameter.ToString())}\""",
            bool _ => $@"{parameter.ToString().ToLower()}",
            _ => parameter.ToString()
        };


    private string ReplaceEscapes(string str)
        {
            return str.Replace(@"\", @"\\") //Escapes in ProcessStartInfo
                .Replace(@"\", @"\\") //Escapes itself when passed to client
                .Replace(@"""", @"\\""""");
        }
    }

    /// <summary>
    /// Json RPC Request that Flow Launcher sent to client
    /// </summary>
    public class JsonRPCServerRequestModel : JsonRPCRequestModel
    {
        public override string ToString()
        {
            string rpc = base.ToString();
            return rpc + "}";
        }
    }

    /// <summary>
    /// Json RPC Request(in query response) that client sent to Flow Launcher
    /// </summary>
    public class JsonRPCClientRequestModel : JsonRPCRequestModel
    {
        public bool DontHideAfterAction { get; set; }

        public override string ToString()
        {
            string rpc = base.ToString();
            return rpc + "}";
        }
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
