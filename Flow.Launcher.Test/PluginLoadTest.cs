using NUnit.Framework;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Test
{
    [TestFixture]
    class PluginLoadTest
    {
        [Test]
        public void GivenDuplicatePluginMetadatasWhenLoadedThenShouldReturnOnlyUniqueList()
        {
            // Given
            var duplicateList = new List<PluginMetadata>
            {
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B4085823D60DC76F28855",
                    Version = "1.0.0"
                },
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B4085823D60DC76F28855",
                    Version = "1.0.1"
                },
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B4085823D60DC76F28855",
                    Version = "1.0.2"
                },
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B4085823D60DC76F28855",
                    Version = "1.0.0"
                },
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B4085823D60DC76F28855",
                    Version = "1.0.0"
                },
                new PluginMetadata
                {
                    ID = "ABC0TYUC6D3B7855823D60DC76F28855",
                    Version = "1.0.0"
                },
                new PluginMetadata
                {
                    ID = "ABC0TYUC6D3B7855823D60DC76F28855",
                    Version = "1.0.0"
                }
            };

            // When
            (var unique, var duplicates) = PluginConfig.GetUniqueLatestPluginMetadata(duplicateList);
            
            // Then
            Assert.True(unique.FirstOrDefault().ID == "CEA0TYUC6D3B4085823D60DC76F28855" && unique.FirstOrDefault().Version == "1.0.2");
            Assert.True(unique.Count() == 1);

            Assert.False(duplicates.Any(x => x.Version == "1.0.2" && x.ID == "CEA0TYUC6D3B4085823D60DC76F28855"));
            Assert.True(duplicates.Count() == 6);
        }

        [Test]
        public void GivenDuplicatePluginMetadatasWithNoUniquePluginWhenLoadedThenShouldReturnEmptyList()
        {
            // Given
            var duplicateList = new List<PluginMetadata>
            {
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B7855823D60DC76F28855",
                    Version = "1.0.0"
                },
                new PluginMetadata
                {
                    ID = "CEA0TYUC6D3B7855823D60DC76F28855",
                    Version = "1.0.0"
                }
            };

            // When
            (var unique, var duplicates) = PluginConfig.GetUniqueLatestPluginMetadata(duplicateList);

            // Then
            Assert.True(unique.Count() == 0);
            Assert.True(duplicates.Count() == 2);
        }
    }
}
