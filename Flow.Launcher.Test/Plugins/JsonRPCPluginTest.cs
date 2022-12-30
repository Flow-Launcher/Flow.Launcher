using NUnit;
using NUnit.Framework;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Linq;
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

            Assert.IsNotNull(results);

            foreach (var result in results)
            {
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AsyncAction);
                Assert.IsNotNull(result.Title);
            }

        }

        public static List<JsonRPCQueryResponseModel> ResponseModelsSource = new()
        {
            new()
            {
                Result = new()
            },
            new()
            {
                Result = new()
                {
                    new JsonRPCResult
                    {
                        Title = "Test1",
                        SubTitle = "Test2"
                    }
                }
            }
        };

        [TestCaseSource(typeof(JsonRPCPluginTest), nameof(ResponseModelsSource))]
        public async Task GivenModel_WhenSerializeWithDifferentNamingPolicy_ThenExpectSameResult_Async(JsonRPCQueryResponseModel reference)
        {
            var camelText = JsonSerializer.Serialize(reference, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var pascalText = JsonSerializer.Serialize(reference);

            var results1 = await QueryAsync(new Query { Search = camelText }, default);
            var results2 = await QueryAsync(new Query { Search = pascalText }, default);

            Assert.IsNotNull(results1);
            Assert.IsNotNull(results2);

            foreach (var ((result1, result2), referenceResult) in results1.Zip(results2).Zip(reference.Result))
            {
                Assert.AreEqual(result1, result2);
                Assert.AreEqual(result1, referenceResult);

                Assert.IsNotNull(result1);
                Assert.IsNotNull(result1.AsyncAction);
            }
        }

    }
}