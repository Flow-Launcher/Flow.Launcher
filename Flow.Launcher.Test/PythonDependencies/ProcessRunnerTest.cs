using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class ProcessRunnerTest
    {
        private static (string FileName, string[] Arguments) LongRunningCommand()
        {
            return OperatingSystem.IsWindows()
                ? ("cmd.exe", new[] { "/c", "ping", "-n", "60", "127.0.0.1" })
                : ("sleep", new[] { "60" });
        }

        [Test]
        public void Cancelled_ThrowsPromptly()
        {
            var (fileName, arguments) = LongRunningCommand();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var stopwatch = Stopwatch.StartNew();

            Assert.That(
                async () => await new ProcessRunner().RunAsync(fileName, arguments, new Dictionary<string, string>(), cancellation.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(20)));
        }
    }
}
