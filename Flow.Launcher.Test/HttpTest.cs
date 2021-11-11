using NUnit.Framework;
using System;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Infrastructure.Http;

namespace Flow.Launcher.Test
{
    [TestFixture]
    class HttpTest
    {
        [Test]
        public void GivenHttpProxy_WhenUpdated_ThenWebProxyShouldAlsoBeUpdatedToTheSame()
        {
            HttpProxy proxy = new HttpProxy();
            Http.Proxy = proxy;

            proxy.Enabled = true;
            proxy.Server = "127.0.0.1";
            Assert.AreEqual(Http.WebProxy.Address, new Uri($"http://{proxy.Server}:{proxy.Port}"));
            Assert.IsNull(Http.WebProxy.Credentials);

            proxy.UserName = "test";
            Assert.NotNull(Http.WebProxy.Credentials);
            Assert.AreEqual(Http.WebProxy.Credentials.GetCredential(Http.WebProxy.Address, "Basic").UserName, proxy.UserName);
            Assert.AreEqual(Http.WebProxy.Credentials.GetCredential(Http.WebProxy.Address, "Basic").Password, "");

            proxy.Password = "test password";
            Assert.AreEqual(Http.WebProxy.Credentials.GetCredential(Http.WebProxy.Address, "Basic").Password, proxy.Password);
        }
    }
}
