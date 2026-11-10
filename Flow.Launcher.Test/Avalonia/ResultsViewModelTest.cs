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

        [Test]
        public void ReplaceResults_WhenTopResultIsUnchangedAcrossQuery_PreservesSelectionInstanceAndIndexWithoutReset()
        {
            using var viewModel = new ResultsViewModel(new Settings());
            var initialTop = CreatePluginResult("program", "spotify", "Spotify", "Music player", 100);
            var initialSecond = CreatePluginResult("program", "sp-tool", "Special Tool", "Utility", 50);
            viewModel.ReplaceResults([initialTop, initialSecond]);

            ClassicAssert.AreEqual(0, viewModel.SelectedIndex);
            ClassicAssert.AreSame(viewModel.Results[0], viewModel.SelectedItem);
            var selectedInstance = viewModel.SelectedItem;

            var selectionChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ResultsViewModel.SelectedItem) || e.PropertyName == nameof(ResultsViewModel.SelectedIndex))
                {
                    selectionChanges++;
                }
            };

            // Query "sp" -> "spo": top result Spotify stays at top with updated score/highlights
            var updatedTop = CreatePluginResult("program", "spotify", "Spotify", "Music player", 105, titleHighlightData: [0, 1, 2]);
            var updatedSecond = CreatePluginResult("websearch", "spotify-web", "Spotify on Web", "Search", 60);
            viewModel.ReplaceResults([updatedTop, updatedSecond]);

            ClassicAssert.AreEqual(0, viewModel.SelectedIndex);
            ClassicAssert.AreSame(selectedInstance, viewModel.SelectedItem, "Top result instance should be preserved without deselection");
            ClassicAssert.AreEqual(0, selectionChanges, "SelectedIndex / SelectedItem should not flap when the top result stays selected at index 0");
        }

        [Test]
        public void ReplaceResults_WhenTopResultIsKept_DoesNotEmitRemoveOnTopItem()
        {
            using var viewModel = new ResultsViewModel(new Settings());
            var initialTop = CreatePluginResult("program", "spotify", "Spotify", "Music player", 100);
            var initialSecond = CreatePluginResult("program", "sp-tool", "Special Tool", "Utility", 50);
            viewModel.ReplaceResults([initialTop, initialSecond]);

            var removedItems = new System.Collections.Generic.List<object>();
            var events = new System.Collections.Generic.List<string>();
            ((System.Collections.Specialized.INotifyCollectionChanged)viewModel.Results).CollectionChanged += (_, e) =>
            {
                events.Add($"Action={e.Action}, OldIndex={e.OldStartingIndex}, NewIndex={e.NewStartingIndex}");
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        removedItems.Add(item);
                    }
                }
            };

            var updatedTop = CreatePluginResult("program", "spotify", "Spotify", "Music player", 105);
            var updatedSecond = CreatePluginResult("websearch", "spotify-web", "Spotify on Web", "Search", 60);
            viewModel.ReplaceResults([updatedTop, updatedSecond]);

            ClassicAssert.IsFalse(removedItems.Contains(initialTop), $"initialTop was removed! Events: {string.Join("; ", events)}");
            ClassicAssert.AreEqual("Action=Replace, OldIndex=1, NewIndex=1", string.Join("; ", events));
        }

        [Test]
        public void ReplaceResultsForPlugins_IncrementalPluginArrival_DoesNotEvictTopResultOfOtherPlugin()
        {
            using var viewModel = new ResultsViewModel(new Settings());
            var programResult = CreatePluginResult("program", "spotify", "Spotify", "Music player", 100);
            var webResult = CreatePluginResult("websearch", "sp-web", "Search sp on Google", "Web", 80);

            // Initial query results
            viewModel.ReplaceResults([programResult, webResult]);
            ClassicAssert.AreEqual(0, viewModel.SelectedIndex);
            var initialSelected = viewModel.SelectedItem;

            // Web search arrives first in next incremental keystroke
            var newWebResult = CreatePluginResult("websearch", "spo-web", "Search spo on Google", "Web", 85);
            viewModel.ReplaceResultsForPlugin("websearch", [newWebResult]);

            ClassicAssert.AreEqual(2, viewModel.Results.Count);
            ClassicAssert.AreSame(initialSelected, viewModel.Results[0]);
            ClassicAssert.AreSame(initialSelected, viewModel.SelectedItem);
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
