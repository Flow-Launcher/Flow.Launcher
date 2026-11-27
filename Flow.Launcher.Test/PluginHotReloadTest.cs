using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class PluginHotReloadTest
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            var api = new Mock<IPublicAPI>();
            api.Setup(m => m.StopwatchLogDebugAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>(), It.IsAny<string>()))
                .Returns<string, string, Func<Task>, string>(async (_, _, action, _) =>
                {
                    await action();
                    return 0L;
                });

            try
            {
                Ioc.Default.ConfigureServices(new ServiceCollection()
                    .AddSingleton(api.Object)
                    .AddSingleton(new Settings())
                    .BuildServiceProvider());
            }
            catch (InvalidOperationException)
            {
                // Ioc.Default can only be configured once per process; another fixture got there first
            }
        }

        private class FakePlugin : IAsyncPlugin, IContextMenu, IAsyncHomeQuery
        {
            public Task InitAsync(PluginInitContext context) => Task.CompletedTask;

            public Task<List<Result>> QueryAsync(Query query, CancellationToken token) => Task.FromResult(new List<Result>());

            public List<Result> LoadContextMenus(Result selectedResult) => new();

            public Task<List<Result>> HomeQueryAsync(CancellationToken token) => Task.FromResult(new List<Result>());
        }

        private static PluginPair CreateFakePluginPair(string id, string actionKeyword)
        {
            return new PluginPair
            {
                Plugin = new FakePlugin(),
                Metadata = new PluginMetadata
                {
                    ID = id,
                    Name = "HotReloadFakePlugin",
                    Language = AllowedLanguage.Executable,
                    ActionKeywords = new List<string> { actionKeyword },
                    ActionKeyword = actionKeyword,
                    IcoPath = string.Empty,
                    // Must be set before PluginDirectory, whose setter combines it into ExecuteFilePath
                    ExecuteFileName = "run.exe",
                    PluginDirectory = Path.GetTempPath()
                }
            };
        }

        [Test]
        public async Task GivenInitializedPluginWhenUnloadedThenAllRegistrationsShouldBeRemovedAsync()
        {
            // Given
            const string id = "HOTRELOAD00000000000000000000001";
            const string keyword = "hotreloadtest";
            var pair = CreateFakePluginPair(id, keyword);

            await PluginManager.InitializePluginAsync(pair);

            Assert.That(PluginManager.GetAllInitializedPlugins(includeFailed: true).Any(p => p.Metadata.ID == id), Is.True);
            Assert.That(PluginManager.IsHomePlugin(id), Is.True);
            Assert.That(PluginManager.GetNonGlobalPlugins().TryGetValue(keyword, out var registered), Is.True);
            Assert.That(registered.Any(p => p.Metadata.ID == id), Is.True);

            // When
            var unloaded = await PluginManager.UnloadPluginAsync(pair);

            // Then
            Assert.That(unloaded, Is.True);
            Assert.That(PluginManager.GetAllInitializedPlugins(includeFailed: true).Any(p => p.Metadata.ID == id), Is.False);
            Assert.That(PluginManager.IsHomePlugin(id), Is.False);
            Assert.That(PluginManager.GetNonGlobalPlugins().ContainsKey(keyword), Is.False);
        }

        [Test]
        public async Task GivenInitializedPluginWhenUnloadedThenUnloadingMessageShouldBeSentAsync()
        {
            // Given
            const string id = "HOTRELOAD00000000000000000000003";
            var pair = CreateFakePluginPair(id, "hotreloadmessage");
            await PluginManager.InitializePluginAsync(pair);
            var recipient = new object();
            PluginMetadata received = null;
            WeakReferenceMessenger.Default.Register<object, PluginUnloadingMessage>(recipient, (_, message) => received = message.Metadata);

            try
            {
                // When
                await PluginManager.UnloadPluginAsync(pair);
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(recipient);
            }

            // Then
            Assert.That(received?.ID, Is.EqualTo(id));
        }

        [Test]
        public void GivenDelegatesWhenCheckedAgainstPluginLoadContextThenOnlyPluginBoundOnesShouldBelongToIt()
        {
            // Given
            var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Flow.Launcher.Plugin.Shell.dll");
            var loader = new PluginAssemblyLoader(assemblyPath);
            try
            {
                var settingsType = loader.LoadAssemblyAndDependencies().GetType("Flow.Launcher.Plugin.Shell.Settings", throwOnError: true);
                var pluginObject = Activator.CreateInstance(settingsType);
                Func<string> boundToPluginObject = pluginObject.ToString;
                Func<string> flowDelegate = () => "flow";

                // When
                var pluginDelegateBelongs = PluginAssemblyLoader.BelongsTo(boundToPluginObject, loader);
                var flowDelegateBelongs = PluginAssemblyLoader.BelongsTo(flowDelegate, loader);

                // Then
                Assert.That(pluginDelegateBelongs, Is.True);
                Assert.That(flowDelegateBelongs, Is.False);
            }
            finally
            {
                loader.Unload();
            }
        }

        [Test]
        public void GivenOpenedResultWhenAddedToHistoryThenHistoryShouldNotKeepPluginResultAlive()
        {
            // Given a plugin result whose action references plugin state, as every real plugin result does
            var weakResult = CreateHistoryFromPluginResult(out var history);

            // When
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Then
            Assert.That(weakResult.IsAlive, Is.False, "History entry keeps the plugin result alive");
            Assert.That(history.Query, Is.EqualTo("hrhistory query"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateHistoryFromPluginResult(out LastOpenedHistoryResult history)
        {
            var pluginState = new object();
            var result = new Result
            {
                Title = "title",
                PluginID = "HOTRELOAD00000000000000000000004",
                OriginQuery = QueryBuilder.Build("hrhistory query", "hrhistory query", new Dictionary<string, List<PluginPair>>()),
                Action = _ => pluginState != null
            };
            history = new LastOpenedHistoryResult(result);
            return new WeakReference(result);
        }

        [Test]
        public async Task GivenDotNetPluginAssemblyWhenUnloadedThenLoadContextShouldBeCollectedAsync()
        {
            // Given an assembly that is present in the test output but not loaded into the default context
            var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Flow.Launcher.Plugin.Shell.dll");
            Assert.That(File.Exists(assemblyPath), Is.True, $"Test assembly not found: {assemblyPath}");

            // When
            var weakReference = LoadAndUnload(assemblyPath);
            var unloaded = await PluginAssemblyLoader.WaitForUnloadAsync(weakReference);

            // Then
            Assert.That(unloaded, Is.True, "Collectible load context was not collected after unload");
        }

        [Test]
        public async Task GivenPluginSettingsSerializedWhenJsonCachesClearedThenLoadContextShouldBeCollectedAsync()
        {
            // Given a plugin settings type that went through System.Text.Json the way PluginJsonStorage does
            var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Flow.Launcher.Plugin.Shell.dll");
            Assert.That(File.Exists(assemblyPath), Is.True, $"Test assembly not found: {assemblyPath}");
            var weakReference = LoadSerializeSettingsAndUnload(assemblyPath);

            // When
            PluginAssemblyLoader.ClearJsonSerializerCaches();
            var unloaded = await PluginAssemblyLoader.WaitForUnloadAsync(weakReference);

            // Then
            Assert.That(unloaded, Is.True, "System.Text.Json caches kept the collectible load context alive");
        }

        // Kept in a separate non-inlined method so no strong reference to the load context or its
        // assembly survives in the test method's stack frame, which would prevent collection
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference LoadAndUnload(string assemblyPath)
        {
            var loader = new PluginAssemblyLoader(assemblyPath);
            var assembly = loader.LoadAssemblyAndDependencies();
            Assert.That(assembly, Is.Not.Null);
            return PluginAssemblyLoader.UnloadAndGetWeakReference(loader);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference LoadSerializeSettingsAndUnload(string assemblyPath)
        {
            var loader = new PluginAssemblyLoader(assemblyPath);
            var settingsType = loader.LoadAssemblyAndDependencies().GetType("Flow.Launcher.Plugin.Shell.Settings", throwOnError: true);
            var settings = JsonSerializer.Deserialize("{}", settingsType);
            JsonSerializer.Serialize(settings, settingsType, new JsonSerializerOptions { WriteIndented = true });
            return PluginAssemblyLoader.UnloadAndGetWeakReference(loader);
        }

        [Test]
        public void GivenPluginDirectoryWhenParsedThenMetadataShouldBeLoaded()
        {
            // Given
            var pluginDirectory = Path.Combine(Path.GetTempPath(), $"HotReloadMetadataTest-{Guid.NewGuid()}");
            Directory.CreateDirectory(pluginDirectory);
            try
            {
                const string executeFileName = "run.exe";
                File.WriteAllText(Path.Combine(pluginDirectory, executeFileName), string.Empty);
                File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), $$"""
                    {
                        "ID": "HOTRELOAD00000000000000000000002",
                        "ActionKeyword": "hrmeta",
                        "Name": "HotReloadMetadataTest",
                        "Author": "test",
                        "Version": "1.0.0",
                        "Language": "Executable",
                        "Website": "",
                        "IcoPath": "icon.png",
                        "ExecuteFileName": "{{executeFileName}}"
                    }
                    """);

                // When
                var metadata = PluginConfig.GetPluginMetadata(pluginDirectory);

                // Then
                Assert.That(metadata, Is.Not.Null);
                Assert.That(metadata.ID, Is.EqualTo("HOTRELOAD00000000000000000000002"));
                Assert.That(metadata.PluginDirectory, Is.EqualTo(pluginDirectory));
                Assert.That(metadata.ActionKeywords, Is.EqualTo(new List<string> { "hrmeta" }));
                Assert.That(metadata.Language, Is.EqualTo("Executable"));
            }
            finally
            {
                Directory.Delete(pluginDirectory, true);
            }
        }
    }
}
