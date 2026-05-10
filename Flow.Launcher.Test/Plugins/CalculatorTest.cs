using System;
using System.Collections.Generic;
using System.Reflection;
using Flow.Launcher.Plugin.Calculator;
using Flow.Launcher.Plugin.Calculator.Storage;
using Mages.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class CalculatorPluginTest
    {
        private readonly Main _plugin;
        private readonly Settings _settings = new()
        {
            DecimalSeparator = DecimalSeparator.UseSystemLocale,
            MaxDecimalPlaces = 10,
            ShowErrorMessage = false, // Make sure we return the empty results when error occurs
            UseThousandsSeparator = true, // Default value
            EnableHistory = true,
        };
        private readonly Engine _engine = new(new Configuration
        {
            Scope = new Dictionary<string, object>
            {
                { "e", Math.E }, // e is not contained in the default mages engine
            }
        });

        public CalculatorPluginTest()
        {
            _plugin = new Main();
            
            var settingField = typeof(Main).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance);
            if (settingField == null)
                Assert.Fail("Could not find field '_settings' on Flow.Launcher.Plugin.Calculator.Main");
            settingField.SetValue(_plugin, _settings);

            var engineField = typeof(Main).GetField("MagesEngine", BindingFlags.NonPublic | BindingFlags.Static);
            if (engineField == null)
                Assert.Fail("Could not find static field 'MagesEngine' on Flow.Launcher.Plugin.Calculator.Main");
            engineField.SetValue(null, _engine);

            SetHistory(new History());
        }

        [SetUp]
        public void SetUp()
        {
            SetHistory(new History());
        }

        [Test]
        public void ThousandsSeparatorTest_Enabled()
        {
            _settings.UseThousandsSeparator = true;

            _settings.DecimalSeparator = DecimalSeparator.Dot;
            var result = GetCalculationResult("1000+234");
            // When thousands separator is enabled, the result should contain a separator
            // Since decimal separator is dot, thousands separator should be comma
            ClassicAssert.AreEqual("1,234", result);

            _settings.DecimalSeparator = DecimalSeparator.Comma;
            var result2 = GetCalculationResult("1000+234");
            // When thousands separator is enabled, the result should contain a separator
            // Since decimal separator is comma, thousands separator should be dot
            ClassicAssert.AreEqual("1.234", result2);
        }

        [Test]
        public void ThousandsSeparatorTest_Disabled()
        {
            _settings.UseThousandsSeparator = false;
            _settings.DecimalSeparator = DecimalSeparator.UseSystemLocale;

            var result = GetCalculationResult("1000+234");
            ClassicAssert.AreEqual("1234", result);
        }

        [Test]
        public void ThousandsSeparatorTest_LargeNumber()
        {
            _settings.UseThousandsSeparator = false;
            _settings.DecimalSeparator = DecimalSeparator.UseSystemLocale;

            var result = GetCalculationResult("1000000+234567");
            ClassicAssert.AreEqual("1234567", result);
        }

        [Test]
        public void CalculationHistory_IsStoredWhenEnabled()
        {
            _settings.EnableHistory = true;

            _plugin.Query(new Plugin.Query { Search = "1+1" });
            WaitForHistoryDebounce();

            ClassicAssert.AreEqual(1, GetHistory().Items.Count);
            ClassicAssert.AreEqual("1+1", GetHistory().Items[0].Query);
            ClassicAssert.AreEqual("2", GetHistory().Items[0].CopyText);
            ClassicAssert.IsNotEmpty(GetHistory().Items[0].SubTitle);
        }

        [Test]
        public void CalculationHistory_IsNotReturnedWhenDisabled()
        {
            _settings.EnableHistory = true;
            _plugin.Query(new Plugin.Query { Search = "1+1" });
            WaitForHistoryDebounce();

            _settings.EnableHistory = false;
            var results = _plugin.Query(new Plugin.Query { Search = "2+2" });

            ClassicAssert.AreEqual(1, results.Count);
            ClassicAssert.AreEqual("4", results[0].Title);
        }

        // Basic operations
        [TestCase(@"1+1", "2")]
        [TestCase(@"2-1", "1")]
        [TestCase(@"2*2", "4")]
        [TestCase(@"4/2", "2")]
        [TestCase(@"2^3", "8")]
        // Decimal places
        [TestCase(@"10/3", "3.3333333333")]
        // Parentheses
        [TestCase(@"(1+2)*3", "9")]
        [TestCase(@"2^(1+2)", "8")]
        // Functions
        [TestCase(@"pow(2,3)", "8")]
        [TestCase(@"min(1,-1,-2)", "-2")]
        [TestCase(@"max(1,-1,-2)", "1")]
        [TestCase(@"sqrt(16)", "4")]
        [TestCase(@"sin(pi)", "0.0000000000")]
        [TestCase(@"cos(0)", "1")]
        [TestCase(@"tan(0)", "0")]
        [TestCase(@"log10(100)", "2")]
        [TestCase(@"log(100)", "2")]
        [TestCase(@"log2(8)", "3")]
        [TestCase(@"ln(e)", "1")]
        [TestCase(@"abs(-5)", "5")]
        // Constants
        [TestCase(@"pi", "3.1415926536")]
        // Complex expressions
        [TestCase(@"(2+3)*sqrt(16)-log(100)/ln(e)", "18")]
        [TestCase(@"sin(pi/2)+cos(0)+tan(0)", "2")]
        // Error handling (should return empty result)
        [TestCase(@"10/0", "")]
        [TestCase(@"sqrt(-1)", "")]
        [TestCase(@"log(0)", "")]
        [TestCase(@"invalid_expression", "")]
        public void CalculatorTest(string expression, string result)
        {
            _settings.UseThousandsSeparator = false;
            _settings.DecimalSeparator = DecimalSeparator.Dot;

            ClassicAssert.AreEqual(GetCalculationResult(expression), result);
        }

        private string GetCalculationResult(string expression)
        {
            var results = _plugin.Query(new Plugin.Query()
            {
                Search = expression
            });
            return results.Count > 0 ? results[0].Title : string.Empty;
        }

        private void WaitForHistoryDebounce()
        {
            var flushPendingItemMethod = typeof(History).GetMethod("FlushPendingItem", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(flushPendingItemMethod, Is.Not.Null,
                "Could not find method 'FlushPendingItem' on Flow.Launcher.Plugin.Calculator.Storage.History");

            flushPendingItemMethod!.Invoke(GetHistory(), null);
        }

        private History GetHistory()
        {
            var historyProperty = typeof(Main).GetProperty("History", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(historyProperty, Is.Not.Null,
                "Could not find property 'History' on Flow.Launcher.Plugin.Calculator.Main");

            var history = historyProperty!.GetValue(_plugin) as History;
            Assert.That(history, Is.Not.Null,
                "Property 'History' on Flow.Launcher.Plugin.Calculator.Main' was not a calculator History instance");

            return history!;
        }

        private void SetHistory(History history)
        {
            var historyProperty = typeof(Main).GetProperty("History", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(historyProperty, Is.Not.Null,
                "Could not find property 'History' on Flow.Launcher.Plugin.Calculator.Main");

            historyProperty!.SetValue(_plugin, history);
        }
    }
}
