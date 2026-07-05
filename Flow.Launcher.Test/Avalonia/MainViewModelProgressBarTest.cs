using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Avalonia
{
    [TestFixture]
    public class MainViewModelProgressBarTest
    {
        private static readonly TimeSpan ProgressBarDelay = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan TimingTolerance = TimeSpan.FromMilliseconds(25);
        private static readonly TimeSpan EventuallyTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

        [Test]
        public async Task IsQueryRunning_WhenStarted_DelaysProgressBarVisibilityAsync()
        {
            var viewModel = new MainViewModel(new Settings());
            var stopwatch = Stopwatch.StartNew();

            try
            {
                viewModel.IsQueryRunning = true;

                ClassicAssert.IsFalse(viewModel.IsProgressBarVisible,
                    "Starting a query should not make the progress bar visible synchronously.");

                await WaitUntilAsync(
                    () => viewModel.IsProgressBarVisible,
                    EventuallyTimeout,
                    "Progress bar did not become visible after the query stayed running past the delay.");

                ClassicAssert.GreaterOrEqual(stopwatch.Elapsed, ProgressBarDelay - TimingTolerance,
                    "Progress bar became visible before the WPF-compatible delay elapsed.");
            }
            finally
            {
                viewModel.IsQueryRunning = false;
            }
        }

        [Test]
        public async Task IsQueryRunning_WhenStoppedBeforeDelay_DoesNotShowProgressBarAsync()
        {
            var viewModel = new MainViewModel(new Settings());

            viewModel.IsQueryRunning = true;
            ClassicAssert.IsFalse(viewModel.IsProgressBarVisible,
                "Starting a query should not make the progress bar visible synchronously.");

            await Task.Delay(TimeSpan.FromMilliseconds(50));

            viewModel.IsQueryRunning = false;

            await Task.Delay(ProgressBarDelay + TimingTolerance + TimeSpan.FromMilliseconds(100));

            ClassicAssert.IsFalse(viewModel.IsProgressBarVisible,
                "Stopping the query before the delay elapses should cancel the pending progress bar display.");
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string failureMessage)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(PollInterval);
            }

            ClassicAssert.IsTrue(condition(), failureMessage);
        }
    }
}
