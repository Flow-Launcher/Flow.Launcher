using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Avalonia
{
    [TestFixture]
    public class MainViewModelQueryDelayTest
    {
        private const int PluginDelayMilliseconds = 2_000;
        private static readonly TimeSpan NoDelayTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan DelayedQueryProbeWindow = TimeSpan.FromMilliseconds(75);
        private static readonly TimeSpan DelayTimingTolerance = TimeSpan.FromMilliseconds(25);

        [OneTimeSetUp]
        public void SetUpPublicApi()
        {
            try
            {
                if (Ioc.Default.GetService<IPublicAPI>() is not null)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
            }

            var api = new Mock<IPublicAPI>();
            api.Setup(x => x.StopwatchLogDebugAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Func<Task>>(),
                    It.IsAny<string>()))
                .Returns<string, string, Func<Task>, string>(async (_, _, action, _) =>
                {
                    await action();
                    return 0L;
                });

            var services = new ServiceCollection();
            services.AddSingleton(api.Object);
            Ioc.Default.ConfigureServices(services.BuildServiceProvider());
        }

        [Test]
        public async Task QueryPluginAsync_WhenSearchDelayIsDisabled_ThenQueriesPluginWithoutWaitingForConfiguredDelayAsync()
        {
            var plugin = new RecordingPlugin();
            var pluginPair = CreatePluginPair(plugin);
            var query = QueryBuilder.Build("test", "test", []);
            using var cancellationTokenSource = new CancellationTokenSource();

            var queryTask = InvokeQueryPluginAsync(pluginPair, query, searchDelay: false, cancellationTokenSource.Token);

            await AssertPluginQueryStartedWithoutConfiguredDelayAsync(plugin, cancellationTokenSource, queryTask,
                "Programmatic/re-query paths pass searchDelay=false and must not wait for per-plugin SearchDelayTime before invoking the plugin query.");
        }

        [Test]
        public async Task QueryPluginAsync_WhenHomeQueryUsesSearchDelay_ThenQueriesPluginWithoutWaitingForConfiguredDelayAsync()
        {
            var plugin = new RecordingPlugin();
            var pluginPair = CreatePluginPair(plugin);
            var query = QueryBuilder.Build(string.Empty, string.Empty, []);
            using var cancellationTokenSource = new CancellationTokenSource();

            var queryTask = InvokeQueryPluginAsync(pluginPair, query, searchDelay: true, cancellationTokenSource.Token);

            await AssertPluginQueryStartedWithoutConfiguredDelayAsync(plugin, cancellationTokenSource, queryTask,
                "Home queries must not wait for per-plugin SearchDelayTime even when the typed-query delay gate is enabled.");
        }

        [Test]
        public async Task QueryPluginAsync_WhenTypedNonHomeQueryUsesSearchDelay_ThenWaitsForConfiguredDelayBeforeQueryingPluginAsync()
        {
            const int configuredDelayMilliseconds = 250;
            var plugin = new RecordingPlugin();
            var pluginPair = CreatePluginPair(plugin, configuredDelayMilliseconds);
            var query = QueryBuilder.Build("test", "test", []);
            var startedAt = DateTimeOffset.UtcNow;

            var queryTask = InvokeQueryPluginAsync(pluginPair, query, searchDelay: true, CancellationToken.None);

            var earlyCompletion = await Task.WhenAny(plugin.QueryStarted.Task, Task.Delay(DelayedQueryProbeWindow));
            ClassicAssert.AreNotSame(plugin.QueryStarted.Task, earlyCompletion,
                "Typed non-home queries should wait for the configured per-plugin delay before invoking the plugin query.");

            await plugin.QueryStarted.Task;
            await queryTask;

            ClassicAssert.GreaterOrEqual(plugin.QueryStartedAt - startedAt,
                TimeSpan.FromMilliseconds(configuredDelayMilliseconds) - DelayTimingTolerance,
                "The plugin query was invoked before the configured search delay elapsed.");
        }

        private static PluginPair CreatePluginPair(RecordingPlugin plugin, int searchDelayMilliseconds = PluginDelayMilliseconds)
        {
            return new PluginPair
            {
                Plugin = plugin,
                Metadata = new PluginMetadata
                {
                    ID = Guid.NewGuid().ToString("N"),
                    Name = "Recording Plugin",
                    ActionKeywords = [Query.GlobalPluginWildcardSign],
                    SearchDelayTime = searchDelayMilliseconds,
                }
            };
        }

        private static async Task<List<ResultViewModel>> InvokeQueryPluginAsync(
            PluginPair pluginPair,
            Query query,
            bool searchDelay,
            CancellationToken cancellationToken)
        {
            var method = typeof(MainViewModel).GetMethod("QueryPluginAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(method);

            var initializedPlugins = GetInitializedPlugins();
            initializedPlugins.TryAdd(pluginPair.Metadata.ID, pluginPair);

            try
            {
                var viewModel = new MainViewModel(new Settings { SearchDelayTime = PluginDelayMilliseconds });
                var invocationResult = method!.Invoke(viewModel, new object[] { pluginPair, query, cancellationToken, searchDelay });
                ClassicAssert.IsNotNull(invocationResult);

                return await (Task<List<ResultViewModel>>)invocationResult!;
            }
            finally
            {
                initializedPlugins.TryRemove(pluginPair.Metadata.ID, out _);
            }
        }

        private static ConcurrentDictionary<string, PluginPair> GetInitializedPlugins()
        {
            var field = typeof(PluginManager).GetField("_allInitializedPlugins", BindingFlags.Static | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(field);

            return (ConcurrentDictionary<string, PluginPair>)field!.GetValue(null)!;
        }

        private static async Task AssertPluginQueryStartedWithoutConfiguredDelayAsync(
            RecordingPlugin plugin,
            CancellationTokenSource cancellationTokenSource,
            Task queryTask,
            string failureMessage)
        {
            var completedTask = await Task.WhenAny(plugin.QueryStarted.Task, Task.Delay(NoDelayTimeout));
            if (completedTask != plugin.QueryStarted.Task)
            {
                await cancellationTokenSource.CancelAsync();
                await queryTask;
                Assert.Fail(failureMessage);
            }

            await queryTask;
        }

        private sealed class RecordingPlugin : IAsyncPlugin
        {
            public TaskCompletionSource<Query> QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTimeOffset QueryStartedAt { get; private set; }

            public Task InitAsync(PluginInitContext context) => Task.CompletedTask;

            public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
            {
                QueryStartedAt = DateTimeOffset.UtcNow;
                QueryStarted.TrySetResult(query);

                return Task.FromResult(new List<Result>
                {
                    new()
                    {
                        Title = "Recorded result",
                        SubTitle = query.Search,
                    }
                });
            }
        }
    }
}
