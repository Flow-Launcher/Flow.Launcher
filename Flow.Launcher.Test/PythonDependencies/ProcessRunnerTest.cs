using System;
using System.Collections.Generic;
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

            var run = new ProcessRunner().RunAsync(fileName, arguments, new Dictionary<string, string>(), cancellation.Token);

            // A hang surfaces as TimeoutException, which fails the constraint.
            Assert.That(
                async () => await run.WaitAsync(TimeSpan.FromSeconds(20)),
                Throws.InstanceOf<OperationCanceledException>());
        }
    }
}
