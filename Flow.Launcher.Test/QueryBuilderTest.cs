using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

            ClassicAssert.AreEqual(">   ping    google.com   -n 20  -6", q.RawQuery);
            ClassicAssert.AreEqual("ping    google.com   -n 20  -6", q.Search, "Search should not start with the ActionKeyword.");
            ClassicAssert.AreEqual(">", q.ActionKeyword);

            ClassicAssert.AreEqual(5, q.SearchTerms.Length, "The length of SearchTerms should match.");

            ClassicAssert.AreEqual("ping", q.FirstSearch);
            ClassicAssert.AreEqual("google.com", q.SecondSearch);
            ClassicAssert.AreEqual("-n", q.ThirdSearch);

            ClassicAssert.AreEqual("google.com -n 20 -6", q.SecondToEndSearch, "SecondToEndSearch should be trimmed of multiple whitespace characters");
        }

        [Test]
        public void ExclusivePluginQueryIgnoreDisabledTest()
        {
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                {">", new PluginPair {Metadata = new PluginMetadata {ActionKeywords = new List<string> {">"}, Disabled = true}}}
            };

            Query q = QueryBuilder.Build(">   ping    google.com   -n 20  -6", nonGlobalPlugins);

            ClassicAssert.AreEqual(">   ping    google.com   -n 20  -6", q.Search);
            ClassicAssert.AreEqual(q.Search, q.RawQuery, "RawQuery should be equal to Search.");
            ClassicAssert.AreEqual(6, q.SearchTerms.Length, "The length of SearchTerms should match.");
            ClassicAssert.AreNotEqual(">", q.ActionKeyword, "ActionKeyword should not match that of a disabled plugin.");
            ClassicAssert.AreEqual("ping google.com -n 20 -6", q.SecondToEndSearch, "SecondToEndSearch should be trimmed of multiple whitespace characters");
        }

        [Test]
        public void GenericPluginQueryTest()
        {
            Query q = QueryBuilder.Build("file.txt file2 file3", new Dictionary<string, PluginPair>());

            ClassicAssert.AreEqual("file.txt file2 file3", q.Search);
            ClassicAssert.AreEqual("", q.ActionKeyword);

            ClassicAssert.AreEqual("file.txt", q.FirstSearch);
            ClassicAssert.AreEqual("file2", q.SecondSearch);
            ClassicAssert.AreEqual("file3", q.ThirdSearch);
            ClassicAssert.AreEqual("file2 file3", q.SecondToEndSearch);
        }
    }
}
