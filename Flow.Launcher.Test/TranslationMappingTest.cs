using System.Collections.Generic;
using System.Reflection;
using Flow.Launcher.Infrastructure;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class TranslationMappingTest
    {
        [Test]
        public void AddNewIndex_ShouldAddTranslatedIndexPlusLength()
        {
            var mapping = new TranslationMapping();
            mapping.AddNewIndex(5, 3);
            mapping.AddNewIndex(8, 2);

            // 5+3=8, 8+2=10
            ClassicAssert.AreEqual(2, GetOriginalToTranslatedCount(mapping));
            ClassicAssert.AreEqual(8, GetOriginalToTranslatedAt(mapping, 0));
            ClassicAssert.AreEqual(10, GetOriginalToTranslatedAt(mapping, 1));
        }

        [TestCase(0, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(5, 2)]
        [TestCase(6, 2)]
        public void MapToOriginalIndex_ShouldReturnExpectedIndex(int translatedIndex, int expectedOriginalIndex)
        {
            var mapping = new TranslationMapping();
            // a测试
            // a Ce Shi
            mapping.AddNewIndex(0, 1);
            mapping.AddNewIndex(2, 2);
            mapping.AddNewIndex(5, 3);

            var result = mapping.MapToOriginalIndex(translatedIndex);
            ClassicAssert.AreEqual(expectedOriginalIndex, result);
        }

        private static int GetOriginalToTranslatedCount(TranslationMapping mapping)
        {
            var field = typeof(TranslationMapping).GetField("originalToTranslated", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<int>)field.GetValue(mapping);
            return list.Count;
        }

        private static int GetOriginalToTranslatedAt(TranslationMapping mapping, int index)
        {
            var field = typeof(TranslationMapping).GetField("originalToTranslated", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<int>)field.GetValue(mapping);
            return list[index];
        }
    }
}
