using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.WebSearch;
using Moq;
using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class WebSearchPluginTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task InitRegistersOnlyKeywordsOfEnabledSourcesAsync(bool enableGlobalSearch)
        {
            var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, nameof(WebSearchPluginTest));
            Directory.CreateDirectory(Path.Combine(directory, "Images"));
            var metadata = new PluginMetadata
            {
                ID = "web-search",
                ExecuteFileName = "WebSearch.dll",
                IcoPath = "web_search.png",
                PluginDirectory = directory,
                PluginSettingsDirectoryPath = directory,
                ActionKeywords = ["*", "disabled", "shared", "stale"],
            };
            var settings = new Settings
            {
                SearchSources =
                [
                    new() { ActionKeyword = "*", Enabled = enableGlobalSearch },
                    new() { ActionKeyword = "disabled", Enabled = false },
                    new() { ActionKeyword = "shared", Enabled = false },
                    new() { ActionKeyword = "shared", Enabled = true },
                    new() { ActionKeyword = "custom", Enabled = true },
                ],
            };
            var api = new Mock<IPublicAPI>();
            api.Setup(x => x.LoadSettingJsonStorage<Settings>()).Returns(settings);
            api.Setup(x => x.AddActionKeyword(metadata.ID, It.IsAny<string>()))
                .Callback<string, string>((_, keyword) => metadata.ActionKeywords.Add(keyword));
            api.Setup(x => x.RemoveActionKeyword(metadata.ID, It.IsAny<string>()))
                .Callback<string, string>((_, keyword) => metadata.ActionKeywords.Remove(keyword));

            await new Main().InitAsync(new PluginInitContext(metadata, api.Object));

            string[] expected = enableGlobalSearch ? ["*", "shared", "custom"] : ["shared", "custom"];
            Assert.That(metadata.ActionKeywords, Is.EquivalentTo(expected));
        }
    }
}
