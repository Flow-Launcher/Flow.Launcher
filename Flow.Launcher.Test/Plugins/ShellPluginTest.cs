using System;
using System.Diagnostics;
using System.Linq;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Shell;
using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ShellPluginTest
    {
        private const string ClosePrompt = "Press any key to close...";

        private static ProcessStartInfo Create(
            string command = "test",
            Shell shell = Shell.Cmd,
            bool leaveShellOpen = false,
            bool closeShellAfterPress = false,
            bool useWindowsTerminal = false,
            bool runAsAdmin = false,
            CustomTemplateShellConfig customConfig = null)
            => Main.CreateProcessStartInfo(
                command,
                shell,
                leaveShellOpen,
                closeShellAfterPress,
                useWindowsTerminal,
                runAsAdmin,
                ClosePrompt,
                customConfig);

        #region CMD

        [Test]
        public void Cmd_ShouldPreserveQuotedCommands()
        {
            var info = Create(
                command: "\"cmd.exe\"",
                shell: Shell.Cmd);

            Assert.That(info.FileName, Is.EqualTo("cmd.exe"));
            Assert.That(info.Arguments, Is.EqualTo("/c \"cmd.exe\""));
            Assert.That(info.ArgumentList, Is.Empty);
        }

        [Test]
        public void Cmd_ShouldUseArgumentListForWindowsTerminal()
        {
            var info = Create(
                command: "\"cmd.exe\"",
                shell: Shell.Cmd,
                useWindowsTerminal: true);

            Assert.That(info.FileName, Is.EqualTo("wt.exe"));
            Assert.That(info.ArgumentList, Is.EqualTo(["cmd", "/c", "\"cmd.exe\""]));
            Assert.That(info.Arguments, Is.Empty);
        }

        [TestCase(true, "/k")]
        [TestCase(false, "/c")]
        public void Cmd_UsesCorrectShellSwitch(bool leaveShellOpen, string expectedSwitch)
        {
            const string command = "test";
            var info = Create(
                command: command,
                leaveShellOpen: leaveShellOpen,
                shell: Shell.Cmd);

            Assert.That(info.Arguments, Is.EqualTo($"{expectedSwitch} {command}"));
        }

        [Test]
        public void Cmd_CloseShellAfterPress_AppendsPause()
        {
            const string command = "test";
            var info = Create(
                command: command,
                closeShellAfterPress: true,
                shell: Shell.Cmd);

            Assert.That(info.Arguments, Is.EqualTo($"/c {command} && echo {ClosePrompt} && pause > nul"));
        }

        [Test]
        public void Cmd_TrimsCommandWhitespace()
        {
            var info = Create(
                command: "  dir  ",
                shell: Shell.Cmd);

            Assert.That(info.Arguments, Is.EqualTo("/c dir"));
        }

        [Test]
        public void Cmd_WithSpacesAndQuotes_PassesThroughUnchanged()
        {
            var info = Create(
                command: "\"C:\\Program Files\\app.exe\" --flag",
                shell: Shell.Cmd);

            Assert.That(info.Arguments, Is.EqualTo("/c \"C:\\Program Files\\app.exe\" --flag"));
        }

        #endregion

        #region PowerShell

        [Test]
        public void Powershell_DirectExecution()
        {
            var info = Create(shell: Shell.Powershell);

            Assert.That(info.FileName, Is.EqualTo("powershell.exe"));
            Assert.That(info.ArgumentList, Is.EqualTo(["-Command", "test;"]));
        }

        [Test]
        public void Powershell_WithSpecialCharacters_PassesThrough()
        {
            var info = Create(
                command: "$env:USERNAME",
                shell: Shell.Powershell);

            Assert.That(info.ArgumentList, Is.EqualTo(["-Command", "$env:USERNAME;"]));
        }

        [Test]
        public void Powershell_CloseShellAfterPress_AppendsPrompt()
        {
            var info = Create(
                shell: Shell.Powershell,
                closeShellAfterPress: true);

            var commandArg = info.ArgumentList.Last();
            Assert.That(commandArg, Does.Contain($"Write-Host '{ClosePrompt}'"));
        }

        [Test]
        public void Powershell_WT_UsesWindowsTerminal()
        {
            var info = Create(
                shell: Shell.Powershell,
                useWindowsTerminal: true);

            Assert.That(info.FileName, Is.EqualTo("wt.exe"));
            Assert.That(info.ArgumentList, Does.Contain("powershell"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Powershell_AddsNoExitOnlyWhenLeaveShellOpen(bool leaveShellOpen)
        {
            var info = Create(
                shell: Shell.Powershell,
                leaveShellOpen: leaveShellOpen);

            Assert.That(info.ArgumentList, leaveShellOpen
                ? Does.Contain("-NoExit")
                : Does.Not.Contain("-NoExit"));
        }

        [Test]
        public void Powershell_LeaveShellOpen_OmitsCommandSwitch()
        {
            var info = Create(
                shell: Shell.Powershell,
                leaveShellOpen: true);

            Assert.That(info.ArgumentList, Does.Not.Contain("-Command"));
        }

        #endregion

        #region Pwsh

        [Test]
        public void Pwsh_AlwaysAddsCommandSwitch()
        {
            var info = Create(shell: Shell.Pwsh);

            Assert.That(info.FileName, Is.EqualTo("pwsh.exe"));
            Assert.That(info.ArgumentList, Does.Contain("-Command"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pwsh_AddsNoExitOnlyWhenLeaveShellOpen(bool leaveShellOpen)
        {
            var info = Create(
                shell: Shell.Pwsh,
                leaveShellOpen: leaveShellOpen);

            Assert.That(
                info.ArgumentList, 
                leaveShellOpen
                    ? Does.Contain("-NoExit")
                    : Does.Not.Contain("-NoExit")
                );
        }

        [Test]
        public void Pwsh_CloseShellAfterPress_AppendsPrompt()
        {
            var info = Create(
                shell: Shell.Pwsh,
                closeShellAfterPress: true);

            var commandArg = info.ArgumentList.Last();
            Assert.That(commandArg, Does.Contain($"Write-Host '{ClosePrompt}'"));
        }

        #endregion

        #region RunCommand

        [Test]
        public void RunCommand_SingleWord_SetsFileName()
        {
            var info = Create(
                command: "notepad",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo("notepad"));
            Assert.That(info.Arguments, Is.Empty);
        }

        [Test]
        public void RunCommand_UnknownExecutable_SetsWholeCommandAsFileName()
        {
            var info = Create(
                command: "nonexistentapp123 argument",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo("nonexistentapp123 argument"));
        }

        [Test]
        public void RunCommand_UnknownQuotedExecutable_SetsWholeCommandAsFileName()
        {
            var info = Create(
                command: "\"C:\\nonexistent\\app.exe\" --flag",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo("\"C:\\nonexistent\\app.exe\" --flag"));
        }

        [Test]
        public void RunCommand_QuotedPathNoArgs_ExtractsFileName()
        {
            var systemDir = Environment.SystemDirectory;
            var info = Create(
                command: $"\"{systemDir}\\cmd.exe\"",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo($"{systemDir}\\cmd.exe"));
            Assert.That(info.Arguments, Is.Empty);
        }

        [Test]
        public void RunCommand_QuotedPath_WithQuotedArgs_Preserved()
        {
            var systemDir = Environment.SystemDirectory;
            var info = Create(
                command: $"\"{systemDir}\\cmd.exe\" /c echo \"hello world\"",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo($"{systemDir}\\cmd.exe"));
            Assert.That(info.Arguments, Is.EqualTo("/c echo \"hello world\""));
        }

        [Test]
        public void RunCommand_UsesArgumentsForCommandTail()
        {
            var info = Create(
                command: "cmd /c echo hello",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo("cmd"));
            Assert.That(info.Arguments, Is.EqualTo("/c echo hello"));
        }

        [Test]
        public void RunCommand_QuotedExecutablePath_ExtractsFileName()
        {
            var systemDir = Environment.SystemDirectory;
            var info = Create(
                command: $"\"{systemDir}\\cmd.exe\" /c echo hello",
                shell: Shell.RunCommand);

            Assert.That(info.FileName, Is.EqualTo($"{systemDir}\\cmd.exe"));
            Assert.That(info.Arguments, Is.EqualTo("/c echo hello"));
        }

        #endregion

        #region CustomTemplate

        [Test]
        public void CustomTemplate_NullConfig_LeavesFileNameAsEmptyString()
        {
            var info = Create(
                command: "notepad",
                shell: Shell.CustomTemplate);

            Assert.That(info.FileName, Is.Empty);
        }

        [TestCase("")]
        [TestCase("  ")]
        public void CustomTemplate_EmptyExecutablePath_LeavesFileNameAsEmptyString(string exePath)
        {
            var config = new CustomTemplateShellConfig { ExecutablePath = exePath };
            var info = Create(
                command: "notepad",
                shell: Shell.CustomTemplate,
                customConfig: config);

            Assert.That(info.FileName, Is.Empty);
        }


        [TestCase("cmd.exe", "/c \"{command}\"", "dir", "/c \"dir\"")]
        [TestCase("powershell.exe", "-Command \"{command};\"", "Get-Process", "-Command \"Get-Process;\"")]
        [TestCase("wsl.exe", "{command}", "ls", "ls")]
        public void CustomTemplate_ReplacesCommandPlaceholder(
            string exePath, string template, string command, string expectedArgs)
        {
            var config = new CustomTemplateShellConfig
            {
                ExecutablePath = exePath,
                ArgumentsTemplate = template
            };
            var info = Create(
                command: command,
                shell: Shell.CustomTemplate,
                customConfig: config);

            Assert.That(info.FileName, Is.EqualTo(exePath));
            Assert.That(info.Arguments, Is.EqualTo(expectedArgs));
        }

        [TestCase("")]
        [TestCase("  ")]
        public void CustomTemplate_EmptyTemplate_DoesNotSetArguments(string template)
        {
            var config = new CustomTemplateShellConfig
            {
                ExecutablePath = "pwsh.exe",
                ArgumentsTemplate = template
            };
            var info = Create(
                command: "Get-Process",
                shell: Shell.CustomTemplate,
                customConfig: config);

            Assert.That(info.FileName, Is.EqualTo("pwsh.exe"));
            Assert.That(info.Arguments, Is.Empty);
        }

        [Test]
        public void CustomTemplate_TrimsExecutablePath()
        {
            var config = new CustomTemplateShellConfig
            {
                ExecutablePath = "  pwsh.exe  ",
                ArgumentsTemplate = "-Command \"{command};\""
            };
            var info = Create(
                command: "Get-Process",
                shell: Shell.CustomTemplate,
                customConfig: config);

            Assert.That(info.FileName, Is.EqualTo("pwsh.exe"));
        }

        [Test]
        public void CustomTemplate_StripsQuotesFromExecutablePath()
        {
            var config = new CustomTemplateShellConfig
            {
                ExecutablePath = "\"C:\\Program Files\\pwsh.exe\"",
                ArgumentsTemplate = "-Command \"{command};\""
            };
            var info = Create(
                command: "Get-Process",
                shell: Shell.CustomTemplate,
                customConfig: config);

            Assert.That(info.FileName, Is.EqualTo("C:\\Program Files\\pwsh.exe"));
        }

        [Test]
        public void CustomTemplate_ExpandsEnvironmentVariablesInExecutablePath()
        {
            var config = new CustomTemplateShellConfig
            {
                ExecutablePath = "%USERPROFILE%\\pwsh.exe",
                ArgumentsTemplate = "-Command \"{command};\""
            };
            var info = Create(
                command: "Get-Process",
                shell: Shell.CustomTemplate,
                customConfig: config);

            var expectedPath = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\pwsh.exe");
            Assert.That(info.FileName, Is.EqualTo(expectedPath));
        }

        [Test]
        public void CustomTemplate_ExpandsEnvironmentVariablesInTemplate()
        {
            var config = new CustomTemplateShellConfig
            {
                ExecutablePath = "cmd.exe",
                ArgumentsTemplate = "/c \"%USERPROFILE%\\{command}\""
            };
            var info = Create(
                command: "test.cmd",
                shell: Shell.CustomTemplate,
                customConfig: config);

            var expectedArg = $"/c \"{Environment.ExpandEnvironmentVariables("%USERPROFILE%")}\\test.cmd\"";
            Assert.That(info.Arguments, Is.EqualTo(expectedArg));
        }

        #endregion

        #region Common

        [TestCase(false, "")]
        [TestCase(true, "runas")]
        public void SetsRunAsAdminVerb(bool runAsAdmin, string expectedVerb)
        {
            var info = Create(runAsAdmin: runAsAdmin);

            Assert.That(info.Verb, Is.EqualTo(expectedVerb));
        }

        [TestCase(Shell.Cmd)]
        [TestCase(Shell.Powershell)]
        [TestCase(Shell.Pwsh)]
        [TestCase(Shell.RunCommand)]
        [TestCase(Shell.CustomTemplate)]
        public void SetsWorkingDirectory(Shell shell)
        {
            var info = Create(shell: shell);

            var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Assert.That(info.WorkingDirectory, Is.EqualTo(expected));
        }

        [TestCase(Shell.Cmd)]
        [TestCase(Shell.Powershell)]
        [TestCase(Shell.Pwsh)]
        [TestCase(Shell.RunCommand)]
        [TestCase(Shell.CustomTemplate)]
        public void SetsUseShellExecute(Shell shell)
        {
            var info = Create(shell: shell);

            Assert.That(info.UseShellExecute, Is.True);
        }

        [TestCase(Shell.Cmd)]
        [TestCase(Shell.Powershell)]
        [TestCase(Shell.Pwsh)]
        [TestCase(Shell.RunCommand)]
        [TestCase(Shell.CustomTemplate)]
        public void ExpandsEnvironmentVariables(Shell shell)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\test");

            CustomTemplateShellConfig config = null;
            if (shell == Shell.CustomTemplate)
            {
                config = new CustomTemplateShellConfig
                {
                    ExecutablePath = "cmd.exe",
                    ArgumentsTemplate = "/c \"{command}\""
                };
            }

            var info = Create(
                command: "%USERPROFILE%\\test",
                shell: shell,
                customConfig: config);

            switch (shell)
            {
                case Shell.Cmd:
                    Assert.That(info.Arguments, Is.EqualTo($"/c {expandedPath}"));
                    break;
                case Shell.Powershell:
                case Shell.Pwsh:
                    Assert.That(info.ArgumentList, Does.Contain(expandedPath + ";"));
                    break;
                case Shell.RunCommand:
                    Assert.That(info.FileName, Is.EqualTo(expandedPath));
                    break;
                case Shell.CustomTemplate:
                    Assert.That(info.Arguments, Is.EqualTo($"/c \"{expandedPath}\""));
                    break;
            }
        }

        #endregion


    }
}
