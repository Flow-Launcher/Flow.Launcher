﻿using System.Collections.Generic;
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
            var nonGlobalPlugins = new Dictionary<string, List<PluginPair>>
            {
                {">", new List<PluginPair>{ new PluginPair {Metadata = new PluginMetadata { ActionKeywords = new List<string> { ">" } } }}}
            };

            Query q = QueryBuilder.Build(">   file.txt    file2 file3", nonGlobalPlugins);

            Assert.AreEqual("file.txt file2 file3", q.Search);
            Assert.AreEqual(">", q.ActionKeyword);
        }

        [Test]
        public void ExclusivePluginQueryIgnoreDisabledTest()
        {
            var nonGlobalPlugins = new Dictionary<string, List<PluginPair>>
            {
                {">", new List<PluginPair>{new PluginPair {Metadata = new PluginMetadata {ActionKeywords = new List<string> {">"}, Disabled = true}} } }
            };

            Query q = QueryBuilder.Build(">   file.txt    file2 file3", nonGlobalPlugins);

            Assert.AreEqual("> file.txt file2 file3", q.Search);
        }

        [Test]
        public void GenericPluginQueryTest()
        {
            Query q = QueryBuilder.Build("file.txt file2 file3", new Dictionary<string, List<PluginPair>>());

            Assert.AreEqual("file.txt file2 file3", q.Search);
            Assert.AreEqual("", q.ActionKeyword);

            Assert.AreEqual("file.txt", q.FirstSearch);
            Assert.AreEqual("file2", q.SecondSearch);
            Assert.AreEqual("file3", q.ThirdSearch);
            Assert.AreEqual("file2 file3", q.SecondToEndSearch);
        }
    }
}
