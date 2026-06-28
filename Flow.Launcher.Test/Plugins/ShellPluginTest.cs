using System.Diagnostics;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Shell;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ShellPluginTest
    {
        [Test]
        public void CreateProcessStartInfo_Cmd_ShouldPreserveQuotedCommands()
        {
            var info = Main.CreateProcessStartInfo(
                "\"cmd.exe\"",
                Shell.Cmd,
                leaveShellOpen: false,
                closeShellAfterPress: false,
                useWindowsTerminal: false,
                runAsAdmin: false,
                closePrompt: "Press any key to close");

            ClassicAssert.AreEqual("cmd.exe", info.FileName);
            ClassicAssert.AreEqual("/c \"cmd.exe\"", info.Arguments);
            ClassicAssert.IsEmpty(info.ArgumentList);
        }

        [Test]
        public void CreateProcessStartInfo_Cmd_ShouldUseArgumentListForWindowsTerminal()
        {
            var info = Main.CreateProcessStartInfo(
                "\"cmd.exe\"",
                Shell.Cmd,
                leaveShellOpen: false,
                closeShellAfterPress: false,
                useWindowsTerminal: true,
                runAsAdmin: false,
                closePrompt: "Press any key to close");

            ClassicAssert.AreEqual("wt.exe", info.FileName);
            CollectionAssert.AreEqual(new[] { "cmd", "/c", "\"cmd.exe\"" }, info.ArgumentList);
            ClassicAssert.IsEmpty(info.Arguments);
        }
    }
}
