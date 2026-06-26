using System.Diagnostics;
using Flow.Launcher.Plugin.Shell;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ShellPluginTest
    {
        [Test]
        public void ConfigureCmdProcessStartInfo_ShouldPreserveQuotedCommands()
        {
            var info = new ProcessStartInfo();

            Main.ConfigureCmdProcessStartInfo(
                info,
                "\"cmd.exe\"",
                leaveShellOpen: false,
                closeShellAfterPress: false,
                notifyStr: "Press any key to close",
                useWindowsTerminal: false);

            ClassicAssert.AreEqual("cmd.exe", info.FileName);
            ClassicAssert.AreEqual("/c \"cmd.exe\"", info.Arguments);
            ClassicAssert.IsEmpty(info.ArgumentList);
        }

        [Test]
        public void ConfigureCmdProcessStartInfo_ShouldKeepArgumentListForWindowsTerminal()
        {
            var info = new ProcessStartInfo();

            Main.ConfigureCmdProcessStartInfo(
                info,
                "\"cmd.exe\"",
                leaveShellOpen: false,
                closeShellAfterPress: false,
                notifyStr: "Press any key to close",
                useWindowsTerminal: true);

            ClassicAssert.AreEqual("wt.exe", info.FileName);
            CollectionAssert.AreEqual(new[] { "cmd", "/c", "\"cmd.exe\"" }, info.ArgumentList);
            ClassicAssert.IsEmpty(info.Arguments);
        }
    }
}
