using System.Linq;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Avalonia
{
    [TestFixture]
    public class ResultsViewModelTest
    {
        [Test]
        public void ReplaceResults_WhenDifferentPluginsReturnSameDisplayText_KeepsBothRows()
        {
            using var viewModel = new ResultsViewModel(new Settings());

            viewModel.ReplaceResults(
            [
                CreatePluginResult("plugin-a", "record-a", "Shared", "same subtitle", 20),
                CreatePluginResult("plugin-b", "record-b", "Shared", "same subtitle", 10)
            ]);

            ClassicAssert.AreEqual(2, viewModel.Results.Count);
            CollectionAssert.AreEquivalent(
                new[] { "plugin-a", "plugin-b" },
                viewModel.Results.Select(result => result.PluginResult!.PluginID).ToArray());
            ClassicAssert.IsTrue(viewModel.Results.All(result => result.Title == "Shared"));
            ClassicAssert.IsTrue(viewModel.Results.All(result => result.SubTitle == "same subtitle"));
        }

        [Test]
        public void ReplaceResults_WhenSameIdentityRefreshesQuerySpecificData_UpdatesTheDisplayedRow()
        {
            using var viewModel = new ResultsViewModel(new Settings());
            var initial = CreatePluginResult(
                "program",
                "chrome",
                "Chrome",
                "Browser",
                10,
                titleHighlightData: [0, 1, 2],
                iconPath: "old.png");
            viewModel.ReplaceResults([initial]);

            var refreshed = CreatePluginResult(
                "program",
                "chrome",
                "Chrome",
                "Browser",
                99,
                titleHighlightData: [0, 1, 2, 3, 4, 5],
                iconPath: "new.png");
            viewModel.ReplaceResults([refreshed]);

            ClassicAssert.AreEqual(1, viewModel.Results.Count);
            var row = viewModel.Results[0];
            ClassicAssert.AreEqual(99, row.Score);
            ClassicAssert.AreEqual("new.png", row.IconPath);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, row.TitleHighlightData);
        }

        [Test]
        public void ReplaceResultsForPlugin_WhenAnotherPluginHasSameDisplayText_OnlyReplacesRequestedPluginRows()
        {
            using var viewModel = new ResultsViewModel(new Settings());
            viewModel.ReplaceResults(
            [
                CreatePluginResult("plugin-a", "a-old", "Shared", "same subtitle", 30),
                CreatePluginResult("plugin-b", "b-original", "Shared", "same subtitle", 20)
            ]);

            viewModel.ReplaceResultsForPlugin(
                "plugin-a",
                [CreatePluginResult("plugin-a", "a-new", "Shared", "same subtitle", 40)]);

            ClassicAssert.AreEqual(2, viewModel.Results.Count);

            var pluginAResult = viewModel.Results.Single(result => result.PluginResult!.PluginID == "plugin-a");
            var pluginBResult = viewModel.Results.Single(result => result.PluginResult!.PluginID == "plugin-b");
            ClassicAssert.AreEqual("a-new", pluginAResult.PluginResult!.RecordKey);
            ClassicAssert.AreEqual(40, pluginAResult.Score);
            ClassicAssert.AreEqual("b-original", pluginBResult.PluginResult!.RecordKey);
            ClassicAssert.AreEqual(20, pluginBResult.Score);
        }

        private static ResultViewModel CreatePluginResult(
            string pluginId,
            string recordKey,
            string title,
            string subTitle,
            int score,
            int[] titleHighlightData = null,
            string iconPath = "")
        {
            return new ResultViewModel
            {
                Title = title,
                SubTitle = subTitle,
                Score = score,
                IconPath = iconPath,
                TitleHighlightData = titleHighlightData,
                PluginResult = new Result
                {
                    Title = title,
                    SubTitle = subTitle,
                    PluginID = pluginId,
                    RecordKey = recordKey
                }
            };
        }
    }
}
