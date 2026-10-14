using Flow.Launcher.Storage;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class QueryHistoryTest
    {
        [Test]
        public void Remove_RemovesOnlySelectedEntryByDefault()
        {
            var history = new History();
            var selected = CreateHistoryItem("query one");
            var otherQuery = CreateHistoryItem("query two");
            history.LastOpenedHistoryItems.AddRange([selected, otherQuery]);

            var removedCount = history.Remove(selected);

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(history.LastOpenedHistoryItems, Is.EqualTo(new[] { otherQuery }));
        }

        [Test]
        public void Remove_WhenRemovingMatchingResults_RemovesEntriesFromAllQueries()
        {
            var history = new History();
            var selected = CreateHistoryItem("query one");
            var sameResultFromAnotherQuery = CreateHistoryItem("query two");
            var differentResult = CreateHistoryItem("query three", recordKey: "different");
            history.LastOpenedHistoryItems.AddRange([selected, sameResultFromAnotherQuery, differentResult]);

            var removedCount = history.Remove(selected, removeAllMatchingResults: true);

            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(history.LastOpenedHistoryItems, Is.EqualTo(new[] { differentResult }));
        }

        private static LastOpenedHistoryResult CreateHistoryItem(string query, string recordKey = "same")
        {
            return new LastOpenedHistoryResult
            {
                Title = "Result",
                SubTitle = "Subtitle",
                PluginID = "Plugin",
                RecordKey = recordKey,
                Query = query
            };
        }
    }
}
