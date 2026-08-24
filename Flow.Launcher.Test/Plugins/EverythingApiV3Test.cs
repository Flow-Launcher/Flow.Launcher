using System;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class EverythingApiV3Test
    {
        [Test]
        public void EnsureConnected_ReusesExistingClient()
        {
            var connectCount = 0;
            var api = new EverythingApiV3(
                string.Empty,
                _ =>
                {
                    connectCount++;
                    return new IntPtr(123);
                },
                _ => true,
                _ => true);

            var firstAttempt = api.EnsureConnected();
            var secondAttempt = api.EnsureConnected();

            ClassicAssert.IsTrue(firstAttempt);
            ClassicAssert.IsTrue(secondAttempt);
            ClassicAssert.IsTrue(api.HasConnectedClient);
            ClassicAssert.AreEqual(1, connectCount);
        }

        [Test]
        public void DisconnectClient_ClearsCachedClientAndCleansUpOnce()
        {
            var shutdownCount = 0;
            var destroyCount = 0;
            var api = new EverythingApiV3(
                "1.5a",
                _ => new IntPtr(456),
                _ =>
                {
                    shutdownCount++;
                    return true;
                },
                _ =>
                {
                    destroyCount++;
                    return true;
                });

            _ = api.EnsureConnected();

            api.DisconnectClient();
            api.DisconnectClient();

            ClassicAssert.IsFalse(api.HasConnectedClient);
            ClassicAssert.AreEqual(1, shutdownCount);
            ClassicAssert.AreEqual(1, destroyCount);
        }
    }
}
