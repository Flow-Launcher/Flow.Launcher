using NUnit.Framework;
using NUnit.Framework.Legacy;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    internal class JsonRPCPluginTest : JsonRPCPlugin
    {
        protected override string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            throw new System.NotImplementedException();
        }

        protected override Task<Stream> RequestAsync(JsonRPCRequestModel request, CancellationToken token = default)
        {
            var byteInfo = Encoding.UTF8.GetBytes(request.Parameters[0] as string ?? string.Empty);

            var resultStream = new MemoryStream(byteInfo);
            return Task.FromResult((Stream)resultStream);
        }

        [TestCase("{\"result\":[],\"DebugMessage\":null}", Description = "Empty Result")]
        [TestCase("{\"result\":[{\"JsonRPCAction\":null,\"Title\":\"something\",\"SubTitle\":\"\",\"ActionKeywordAssigned\":null,\"IcoPath\":null}],\"DebugMessage\":null}", Description = "One Result with Pascal Case")]
        [TestCase("{\"result\":[{\"jsonRPCAction\":null,\"title\":\"something\",\"subTitle\":\"\",\"actionKeywordAssigned\":null,\"icoPath\":null}],\"debugMessage\":null}", Description = "One Result with camel Case")]
        [TestCase("{\"result\":[{\"JsonRPCAction\":null,\"Title\":\"iii\",\"SubTitle\":\"\",\"ActionKeywordAssigned\":null,\"IcoPath\":null},{\"JsonRPCAction\":null,\"Title\":\"iii\",\"SubTitle\":\"\",\"ActionKeywordAssigned\":null,\"IcoPath\":null}],\"DebugMessage\":null}", Description = "Two Result with Pascal Case")]
        [TestCase("{\"result\":[{\"jsonrpcAction\":null,\"TItLE\":\"iii\",\"Subtitle\":\"\",\"Actionkeywordassigned\":null,\"icoPath\":null},{\"jsonRPCAction\":null,\"tiTle\":\"iii\",\"subTitle\":\"\",\"ActionKeywordAssigned\":null,\"IcoPath\":null}],\"DebugMessage\":null}", Description = "Two Result with Weird Case")]
        public async Task GivenVariousJsonText_WhenVariousNamingCase_ThenExpectNotNullResults_Async(string resultText)
        {
            var results = await QueryAsync(new Query
            {
                Search = resultText
            }, default);

            ClassicAssert.IsNotNull(results);

            foreach (var result in results)
            {
                ClassicAssert.IsNotNull(result);
                ClassicAssert.IsNotNull(result.AsyncAction);
                ClassicAssert.IsNotNull(result.Title);
            }

        }

        public static List<JsonRPCQueryResponseModel> ResponseModelsSource = new()
        {
            new JsonRPCQueryResponseModel(0, new List<JsonRPCResult>()),
            new JsonRPCQueryResponseModel(0, new List<JsonRPCResult>
            {
                new()
                {
                    Title = "Test1", SubTitle = "Test2"
                }
            })
        };
    }
}
