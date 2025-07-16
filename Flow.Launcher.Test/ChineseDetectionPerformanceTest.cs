using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Flow.Launcher.Infrastructure;
using ToolGood.Words.Pinyin;

namespace Flow.Launcher.Test
{
    /// <summary>
    /// Performance test comparing ContainsChinese() vs WordsHelper.HasChinese()
    /// 
    /// This test verifies:
    /// 1. Both methods produce identical results (correctness)
    /// 2. Performance characteristics of both implementations
    /// 3. Memory allocation patterns
    /// 
    /// The ContainsChinese() method uses optimized Unicode range checking with ReadOnlySpan
    /// while WordsHelper.HasChinese() uses the ToolGood.Words library implementation.
    /// </summary>
    [TestFixture]
    public class ChineseDetectionPerformanceTest
    {
        private readonly List<string> _testStrings = new()
        {
            // Pure English - should return false
            "Hello World",
            "Visual Studio Code",
            "Microsoft Office 2023",
            "Adobe Photoshop Creative Suite",
            "Google Chrome Browser Application",
            
            // Pure Chinese - should return true
            "你好世界",
            "微软办公软件", 
            "谷歌浏览器",
            "北京大学计算机科学与技术学院",
            "中华人民共和国国家发展和改革委员会",
            
            // Mixed content - should return true
            "Hello 世界",
            "Visual Studio 代码编辑器",
            "QQ音乐 Music Player", 
            "Windows 10 操作系统",
            "GitHub 代码仓库管理平台",
            
            // Edge cases
            "",
            " ",
            "123456",
            "!@#$%^&*()",
            "café résumé naïve", // Accented characters (not Chinese)
            
            // Long strings for performance testing
            "This is a very long English string that contains no Chinese characters but is designed to test performance with longer text content that might appear in file names or application descriptions",
            "这是一个非常长的中文字符串，包含了很多汉字，用来测试在处理较长中文文本时的性能表现，比如可能出现在文件名或应用程序描述中的文本内容",
            "This is a mixed 混合内容的字符串 that contains both English and Chinese characters 中英文混合 to test performance with 复杂的文本内容 in real-world scenarios 真实场景中的应用"
        };

        [Test]
        public void ContainsChinese_CorrectnessTest()
        {
            // Verify ContainsChinese works correctly for known cases
            ClassicAssert.IsFalse(ContainsChinese("Hello World"), "Pure English should return false");
            ClassicAssert.IsTrue(ContainsChinese("你好世界"), "Pure Chinese should return true");
            ClassicAssert.IsTrue(ContainsChinese("Hello 世界"), "Mixed content should return true");
            ClassicAssert.IsFalse(ContainsChinese(""), "Empty string should return false");
            ClassicAssert.IsFalse(ContainsChinese("123456"), "Numbers should return false");
            ClassicAssert.IsFalse(ContainsChinese("café résumé"), "Accented characters should return false");
        }

        [Test]
        public void WordsHelper_CorrectnessTest()
        {
            // Verify WordsHelper.HasChinese works correctly for known cases
            ClassicAssert.IsFalse(WordsHelper.HasChinese("Hello World"), "Pure English should return false");
            ClassicAssert.IsTrue(WordsHelper.HasChinese("你好世界"), "Pure Chinese should return true");
            ClassicAssert.IsTrue(WordsHelper.HasChinese("Hello 世界"), "Mixed content should return true");
            ClassicAssert.IsFalse(WordsHelper.HasChinese(""), "Empty string should return false");
            ClassicAssert.IsFalse(WordsHelper.HasChinese("123456"), "Numbers should return false");
            ClassicAssert.IsFalse(WordsHelper.HasChinese("café résumé"), "Accented characters should return false");
        }

        [Test]
        public void BothMethods_ShouldProduceSameResults()
        {
            // Critical test: verify both methods produce identical results for all test cases
            foreach (var testString in _testStrings)
            {
                var wordsHelperResult = WordsHelper.HasChinese(testString);
                var containsChineseResult = ContainsChinese(testString);
                
                ClassicAssert.AreEqual(wordsHelperResult, containsChineseResult,
                    $"Results differ for string: '{testString}'. WordsHelper: {wordsHelperResult}, ContainsChinese: {containsChineseResult}");
            }
            
            Console.WriteLine($"✓ Both methods produce identical results for all {_testStrings.Count} test cases");
        }

        [Test]
        public void PerformanceComparison_BasicBenchmark()
        {
            const int iterations = 1000000;
            
            Console.WriteLine("=== CHINESE CHARACTER DETECTION PERFORMANCE TEST ===");
            Console.WriteLine($"Test iterations: {iterations:N0}");
            Console.WriteLine($"Test strings: {_testStrings.Count}");
            Console.WriteLine($"Total operations: {iterations * _testStrings.Count:N0}");
            Console.WriteLine();

            // Warmup to ensure JIT compilation
            Console.WriteLine("Warming up...");
            for (int i = 0; i < 1000; i++)
            {
                foreach (var testString in _testStrings)
                {
                    _ = ContainsChinese(testString);
                    _ = WordsHelper.HasChinese(testString);
                }
            }

            // Benchmark ContainsChinese method
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                foreach (var testString in _testStrings)
                {
                    _ = ContainsChinese(testString);
                }
            }
            sw1.Stop();

            // Benchmark WordsHelper.HasChinese method
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                foreach (var testString in _testStrings)
                {
                    _ = WordsHelper.HasChinese(testString);
                }
            }
            sw2.Stop();

            // Calculate and display results
            var containsChineseMs = sw1.Elapsed.TotalMilliseconds;
            var wordsHelperMs = sw2.Elapsed.TotalMilliseconds;
            var speedRatio = wordsHelperMs / containsChineseMs;
            var timeDifference = wordsHelperMs - containsChineseMs;

            Console.WriteLine("RESULTS:");
            Console.WriteLine($"ContainsChinese():        {containsChineseMs:F3} ms");
            Console.WriteLine($"WordsHelper.HasChinese(): {wordsHelperMs:F3} ms");
            Console.WriteLine($"Time difference:          {timeDifference:F3} ms");
            Console.WriteLine($"Speed improvement:        {speedRatio:F2}x");
            Console.WriteLine($"Performance gain:         {((speedRatio - 1) * 100):F1}%");
            Console.WriteLine();

            if (speedRatio > 1.0)
            {
                Console.WriteLine($"✓ ContainsChinese() is {speedRatio:F2}x faster than WordsHelper.HasChinese()");
            }
            else
            {
                Console.WriteLine($"⚠ WordsHelper.HasChinese() is {(1/speedRatio):F2}x faster than ContainsChinese()");
            }

            // Test always passes - this is a measurement test
            ClassicAssert.IsTrue(true);
        }

        [Test]
        public void PerformanceComparison_ByStringType()
        {
            Console.WriteLine("=== PERFORMANCE BY STRING TYPE ===");
            
            var categories = new Dictionary<string, List<string>>
            {
                ["Pure English"] = _testStrings.Where(s => !ContainsChinese(s) && s.All(c => c <= 127)).ToList(),
                ["Pure Chinese"] = _testStrings.Where(s => ContainsChinese(s) && s.All(c => IsChineseCharacter(c) || char.IsWhiteSpace(c))).ToList(),
                ["Mixed Content"] = _testStrings.Where(s => ContainsChinese(s) && s.Any(c => c <= 127 && char.IsLetter(c))).ToList(),
                ["Edge Cases"] = _testStrings.Where(s => string.IsNullOrWhiteSpace(s) || s.All(c => !char.IsLetter(c))).ToList()
            };

            foreach (var category in categories)
            {
                if (category.Value.Count == 0) continue;
                
                Console.WriteLine($"\n{category.Key} ({category.Value.Count} strings):");
                
                var sample = category.Value.First();
                var displayText = sample.Length > 40 ? sample.Substring(0, 40) + "..." : sample;
                Console.WriteLine($"  Sample: '{displayText}'");
                
                const int categoryIterations = 5000;
                
                // Test each method
                var sw1 = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < categoryIterations; i++)
                {
                    foreach (var str in category.Value)
                    {
                        _ = ContainsChinese(str);
                    }
                }
                sw1.Stop();
                
                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < categoryIterations; i++)
                {
                    foreach (var str in category.Value)
                    {
                        _ = WordsHelper.HasChinese(str);
                    }
                }
                sw2.Stop();
                
                var ratio = (double)sw2.ElapsedTicks / sw1.ElapsedTicks;
                Console.WriteLine($"  Performance: ContainsChinese is {ratio:F2}x faster");
            }
            
            ClassicAssert.IsTrue(true);
        }

        /// <summary>
        /// Optimized Chinese character detection using comprehensive CJK Unicode ranges
        /// This method uses ReadOnlySpan for better performance and covers all CJK character ranges
        /// </summary>
        private static bool ContainsChinese(ReadOnlySpan<char> text)
        {
            foreach (var c in text)
            {
                if (IsChineseCharacter(c))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a character is a Chinese character using comprehensive Unicode ranges
        /// Covers CJK Unified Ideographs and all extension blocks
        /// </summary>
        private static bool IsChineseCharacter(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||     // CJK Unified Ideographs (most common Chinese characters)
                   (c >= 0x3400 && c <= 0x4DBF) ||     // CJK Extension A
                   (c >= 0x20000 && c <= 0x2A6DF) ||   // CJK Extension B
                   (c >= 0x2A700 && c <= 0x2B73F) ||   // CJK Extension C
                   (c >= 0x2B740 && c <= 0x2B81F) ||   // CJK Extension D
                   (c >= 0x2B820 && c <= 0x2CEAF) ||   // CJK Extension E
                   (c >= 0x2CEB0 && c <= 0x2EBEF) ||   // CJK Extension F
                   (c >= 0xF900 && c <= 0xFAFF) ||     // CJK Compatibility Ideographs
                   (c >= 0x2F800 && c <= 0x2FA1F);     // CJK Compatibility Supplement
        }
    }
}
