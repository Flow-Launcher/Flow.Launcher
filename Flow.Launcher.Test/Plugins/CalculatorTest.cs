using System;
using System.Collections.Generic;
using System.Reflection;
using Flow.Launcher.Plugin.Calculator;
using Mages.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class CalculatorPluginTest
    {
        private readonly Main _plugin;

        public CalculatorPluginTest()
        {
            _plugin = new Main();
            
            var settingField = typeof(Main).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance);
            if (settingField == null)
                Assert.Fail("Could not find field '_settings' on Flow.Launcher.Plugin.Calculator.Main");
            settingField.SetValue(_plugin, new Settings
            {
                ShowErrorMessage = false // Make sure we return the empty results when error occurs
            });

            var engineField = typeof(Main).GetField("MagesEngine", BindingFlags.NonPublic | BindingFlags.Static);
            if (engineField == null)
                Assert.Fail("Could not find static field 'MagesEngine' on Flow.Launcher.Plugin.Calculator.Main");
            engineField.SetValue(null, new Engine(new Configuration
            {
                Scope = new Dictionary<string, object>
                {
                    { "e", Math.E }, // e is not contained in the default mages engine
                }
            }));
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
    }
}
