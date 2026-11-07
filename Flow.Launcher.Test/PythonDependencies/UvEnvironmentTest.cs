using System;
using System.Collections.Generic;
using System.Net;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class UvEnvironmentTest
    {
        [Test]
        public void ProxyUrl_NoProxy_ReturnsNull()
        {
            Assert.That(UvEnvironment.ProxyUrl(null, null), Is.Null);
        }

        [Test]
        public void ProxyUrl_WithoutCredentials_KeepsHostAndPort()
        {
            Assert.That(UvEnvironment.ProxyUrl(new Uri("http://proxy.local:8080"), null), Is.EqualTo("http://proxy.local:8080"));
        }

        [Test]
        public void ProxyUrl_WithCredentials_EscapesThem()
        {
            var url = UvEnvironment.ProxyUrl(new Uri("http://proxy.local:8080"), new NetworkCredential("us@er", "p:ss/word"));

            Assert.That(url, Is.EqualTo("http://us%40er:p%3Ass%2Fword@proxy.local:8080"));
        }

        [Test]
        public void Build_WithProxy_SetsBothProxyVariables()
        {
            var environment = UvEnvironment.Build(false, "cache", "http://proxy.local:8080");

            Assert.That(environment, Is.EqualTo(new Dictionary<string, string>
            {
                ["UV_NO_CONFIG"] = "1",
                ["HTTP_PROXY"] = "http://proxy.local:8080",
                ["HTTPS_PROXY"] = "http://proxy.local:8080"
            }));
        }

        [Test]
        public void Build_WithoutProxy_SetsNoProxyVariables()
        {
            var environment = UvEnvironment.Build(true, "cache", null);

            Assert.That(environment, Is.EqualTo(new Dictionary<string, string>
            {
                ["UV_NO_CONFIG"] = "1",
                ["UV_CACHE_DIR"] = "cache"
            }));
        }
    }
}
