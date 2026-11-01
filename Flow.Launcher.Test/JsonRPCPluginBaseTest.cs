using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using Moq;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class JsonRPCPluginBaseTest
    {
        /// <summary>
        /// Minimal concrete JsonRPCPluginBase exposing the protected API
        /// dispatcher for testing.
        /// </summary>
        private class TestRpcPlugin : JsonRPCPluginBase
        {
            public void Call(string method, object[] parameters)
            {
                ExecuteFlowLauncherAPI(method, parameters);
            }

            public void SetContext(PluginInitContext context)
            {
                Context = context;
            }

            public override List<Result> LoadContextMenus(Result selectedResult) => new List<Result>();

            protected override Task<bool> ExecuteResultAsync(JsonRPCResult result) => Task.FromResult(true);

            public override Task<List<Result>> QueryAsync(Query query, CancellationToken token) =>
                Task.FromResult(new List<Result>());

        }

        private Mock<IPublicAPI> _apiMock;
        private TestRpcPlugin _plugin;

        [SetUp]
        public void SetUp()
        {
            _apiMock = new Mock<IPublicAPI>();
            _plugin = new TestRpcPlugin();
            _plugin.SetContext(new PluginInitContext { API = _apiMock.Object });
        }

        [Test]
        public void GivenOptionalParameterOmitted_WhenCallingAPI_ThenMethodIsInvoked()
        {
            // OpenDirectory(string directoryPath, string fileNameOrFilePath = null)
            _plugin.Call("OpenDirectory", new object[] { @"C:\" });

            _apiMock.Verify(api => api.OpenDirectory(@"C:\", null), Times.Once);
        }

        [Test]
        public void GivenNullParameter_WhenCallingAPI_ThenMethodIsInvoked()
        {
            _plugin.Call("OpenDirectory", new object[] { @"C:\", null });

            _apiMock.Verify(api => api.OpenDirectory(@"C:\", null), Times.Once);
        }

        [Test]
        public void GivenAllParameters_WhenCallingAPI_ThenMethodIsInvoked()
        {
            _plugin.Call("OpenDirectory", new object[] { @"C:\", @"C:\file.txt" });

            _apiMock.Verify(api => api.OpenDirectory(@"C:\", @"C:\file.txt"), Times.Once);
        }

        [Test]
        public void GivenUnknownMethod_WhenCallingAPI_ThenNothingIsInvoked()
        {
            _plugin.Call("ThisMethodDoesNotExist", new object[] { "x" });

            _apiMock.VerifyNoOtherCalls();
        }

        [Test]
        public void GivenIncompatibleArgument_WhenCallingAPI_ThenNothingIsInvoked()
        {
            // no IPublicAPI method named OpenDirectory takes an int argument
            _plugin.Call("OpenDirectory", new object[] { 42 });

            _apiMock.VerifyNoOtherCalls();
        }
    }
}
