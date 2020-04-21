using NUnit.Framework;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Exception;

namespace Flow.Launcher.Test.Plugins
{

    [TestFixture]
    public class PluginInitTest
    {
        [Test]
        public void PublicAPIIsNullTest()
        {
            //Assert.Throws(typeof(Flow.LauncherFatalException), () => PluginManager.Initialize(null));
        }
    }
}
