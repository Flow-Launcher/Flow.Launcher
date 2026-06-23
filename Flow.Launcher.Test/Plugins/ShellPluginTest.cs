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
            var info = new ProcessStartInfo
            {
                FileName = "cmd.exe"
            };

            Main.ConfigureCmdProcessStartInfo(
                info,
                "\"cmd.exe\"",
                leaveShellOpen: false,
                closeShellAfterPress: false,
                notifyStr: "Press any key to close",
                useWindowsTerminal: false);

            ClassicAssert.AreEqual("/c \"cmd.exe\"", info.Arguments);
            ClassicAssert.IsEmpty(info.ArgumentList);
        }

        [Test]
        public void ConfigureCmdProcessStartInfo_ShouldKeepArgumentListForWindowsTerminal()
        {
            var info = new ProcessStartInfo
            {
                FileName = "wt.exe"
            };

            Main.ConfigureCmdProcessStartInfo(
                info,
                "\"cmd.exe\"",
                leaveShellOpen: false,
                closeShellAfterPress: false,
                notifyStr: "Press any key to close",
                useWindowsTerminal: true);

            CollectionAssert.AreEqual(new[] { "cmd", "/c", "\"cmd.exe\"" }, info.ArgumentList);
            ClassicAssert.IsEmpty(info.Arguments);
        }
    }
}
