using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Flow.Launcher.Plugin.Program.Programs;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class Win32ProgramTest
    {
        [Test]
        public void GivenMissingExePath_WhenResolvingProgram_ThenReturnsInvalidProgramWithoutErrorLog()
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.exe");
            var previousConfig = LogManager.Configuration;
            var memoryTarget = new MemoryTarget("win32-program-memory") { Layout = "${level}|${message}" };
            var config = new LoggingConfiguration();
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, memoryTarget);
            LogManager.Configuration = config;

            try
            {
                var program = ResolveExeProgram(path);
                LogManager.Flush();

                ClassicAssert.IsFalse(program.Valid);
                ClassicAssert.IsFalse(
                    memoryTarget.Logs.Any(log => log.Contains("|Win32|ExeProgram|", StringComparison.Ordinal)),
                    string.Join(Environment.NewLine, memoryTarget.Logs));
            }
            finally
            {
                LogManager.Configuration = previousConfig;
            }
        }

        [Test]
        public void GivenMalformedShortcut_WhenResolvingProgram_ThenReturnsInvalidProgramWithoutErrorLog()
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.lnk");
            File.WriteAllText(path, "not a shell link");
            var previousConfig = LogManager.Configuration;
            var memoryTarget = new MemoryTarget("win32-lnk-memory") { Layout = "${level}|${message}" };
            var config = new LoggingConfiguration();
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, memoryTarget);
            LogManager.Configuration = config;

            try
            {
                var program = ResolveLnkProgram(path);
                LogManager.Flush();

                ClassicAssert.IsFalse(program.Valid);
                ClassicAssert.IsFalse(
                    memoryTarget.Logs.Any(log => log.Contains("Calling method: LnkProgram", StringComparison.Ordinal)),
                    string.Join(Environment.NewLine, memoryTarget.Logs));
            }
            finally
            {
                LogManager.Configuration = previousConfig;
            }
        }

        private static Win32 ResolveExeProgram(string path)
        {
            var method = typeof(Win32).GetMethod("ExeProgram", BindingFlags.Static | BindingFlags.NonPublic);
            return (Win32)method.Invoke(null, [path]);
        }

        private static Win32 ResolveLnkProgram(string path)
        {
            var method = typeof(Win32).GetMethod("LnkProgram", BindingFlags.Static | BindingFlags.NonPublic);
            return (Win32)method.Invoke(null, [path]);
        }
    }
}
