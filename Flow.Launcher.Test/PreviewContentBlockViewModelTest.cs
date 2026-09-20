using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class PreviewContentBlockViewModelTest
    {
        [Test]
        public async Task GivenSupersededLoadStillRunning_WhenItsContentArrivesAfterNewerLoad_ThenStaleContentIsDiscarded_Async()
        {
            // A fresh load can start while a cancelled one is still unwinding.
            // The late result of that superseded load must be discarded.
            var staleContent = "stale content";
            var currentContent = "current content";

            // Both loads read through the same delegate and are given a deferred read.
            // The reads ignore cancellation to model a file read finishing after its load was superseded.
            var supersededRead = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var currentRead = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reads = new Queue<TaskCompletionSource<string>>(
            [
                supersededRead,
                currentRead
            ]);

            var viewModel = new PreviewContentBlockViewModel(
                new MarkdownPreviewBlock { FilePath = "preview.md" },
                (_, _) => reads.Dequeue().Task);
            
            using var supersededCancellation = new CancellationTokenSource();
            using var currentCancellation = new CancellationTokenSource();

            // The first load starts and is cancelled while its read is still pending.
            var supersededLoad = viewModel.LoadAsync(string.Empty, supersededCancellation.Token);
            await supersededCancellation.CancelAsync();

            // Selecting the result again starts a fresh load that supersedes the cancelled one.
            var currentLoad = viewModel.LoadAsync(string.Empty, currentCancellation.Token);

            // The current read finishes first, so its content is published.
            currentRead.SetResult(currentContent);
            await currentLoad;
            ClassicAssert.AreEqual(PreviewContentLoadState.Ready, viewModel.LoadState);
            ClassicAssert.AreEqual(currentContent, viewModel.RenderedContent);

            // The superseded read finishes after the newer load has already completed.
            // Its late content must not replace the newer load's content.
            supersededRead.SetResult(staleContent);
            await supersededLoad;
            ClassicAssert.AreEqual(PreviewContentLoadState.Ready, viewModel.LoadState);
            ClassicAssert.AreEqual(currentContent, viewModel.RenderedContent);
        }
    }
}