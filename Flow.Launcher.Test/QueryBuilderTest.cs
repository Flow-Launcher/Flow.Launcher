using System.Collections.Generic;
using NUnit.Framework;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Test
{
    public class QueryBuilderTest
    {
        [Test]
        public void ExclusivePluginQueryTest()
        {
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                {">", new PluginPair {Metadata = new PluginMetadata {ActionKeywords = new List<string> {">"}}}}
            };

            Query q = QueryBuilder.Build(">   ping    google.com   -n 20  -6", nonGlobalPlugins);

            Assert.AreEqual(">   ping    google.com   -n 20  -6", q.RawQuery);
            Assert.AreEqual("ping    google.com   -n 20  -6", q.Search, "Search should not start with the ActionKeyword.");
            Assert.AreEqual(">", q.ActionKeyword);

            Assert.AreEqual(5, q.SearchTerms.Length, "The length of SearchTerms should match.");

            Assert.AreEqual("ping", q.FirstSearch);
            Assert.AreEqual("google.com", q.SecondSearch);
            Assert.AreEqual("-n", q.ThirdSearch);

            Assert.AreEqual("google.com -n 20 -6", q.SecondToEndSearch, "SecondToEndSearch should be trimmed of multiple whitespace characters");
        }

        [Test]
        public void ExclusivePluginQueryIgnoreDisabledTest()
        {
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                {">", new PluginPair {Metadata = new PluginMetadata {ActionKeywords = new List<string> {">"}, Disabled = true}}}
            };

            Query q = QueryBuilder.Build(">   ping    google.com   -n 20  -6", nonGlobalPlugins);

            Assert.AreEqual(">   ping    google.com   -n 20  -6", q.Search);
            Assert.AreEqual(q.Search, q.RawQuery, "RawQuery should be equal to Search.");
            Assert.AreEqual(6, q.SearchTerms.Length, "The length of SearchTerms should match.");
            Assert.AreNotEqual(">", q.ActionKeyword, "ActionKeyword should not match that of a disabled plugin.");
            Assert.AreEqual("ping google.com -n 20 -6", q.SecondToEndSearch, "SecondToEndSearch should be trimmed of multiple whitespace characters");
        }

        [Test]
        public void GenericPluginQueryTest()
        {
            Query q = QueryBuilder.Build("file.txt file2 file3", new Dictionary<string, PluginPair>());

            Assert.AreEqual("file.txt file2 file3", q.Search);
            Assert.AreEqual("", q.ActionKeyword);

            Assert.AreEqual("file.txt", q.FirstSearch);
            Assert.AreEqual("file2", q.SecondSearch);
            Assert.AreEqual("file3", q.ThirdSearch);
            Assert.AreEqual("file2 file3", q.SecondToEndSearch);
        }
    }
}
