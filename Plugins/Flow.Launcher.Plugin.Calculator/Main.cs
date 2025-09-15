using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Mages.Core;
using Flow.Launcher.Plugin.Calculator.Views;
using Flow.Launcher.Plugin.Calculator.ViewModels;

namespace Flow.Launcher.Plugin.Calculator
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        private static readonly Regex ThousandGroupRegex = MainRegexHelper.GetThousandGroupRegex();
        private static readonly Regex NumberRegex = MainRegexHelper.GetNumberRegex();
        private static readonly Regex PowRegex = MainRegexHelper.GetPowRegex();
        private static readonly Regex FunctionRegex = MainRegexHelper.GetFunctionRegex();

        private static Engine MagesEngine;
        private const string Comma = ",";
        private const string Dot = ".";
        private const string IcoPath = "Images/calculator.png";
        private static readonly List<Result> EmptyResults = [];

        internal static PluginInitContext Context { get; set; } = null!;

        private Settings _settings;
        private SettingsViewModel _viewModel;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings);

            MagesEngine = new Engine(new Configuration
            {
                Scope = new Dictionary<string, object>
                {
                    { "e", Math.E }, // e is not contained in the default mages engine
                }
            });
        }

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return EmptyResults;
            }

            try
            {
                var search = query.Search;
                bool isFunctionPresent = FunctionRegex.IsMatch(search);
                
                // Mages is case sensitive, so we need to convert all function names to lower case.
                search = FunctionRegex.Replace(search, m => m.Value.ToLowerInvariant());
                
                var decimalSep = GetDecimalSeparator();
                var groupSep = GetGroupSeparator(decimalSep);
                var expression = NumberRegex.Replace(search, m => NormalizeNumber(m.Value, isFunctionPresent, decimalSep, groupSep));

                // WORKAROUND START: The 'pow' function in Mages v3.0.0 is broken.
                // https://github.com/FlorianRappl/Mages/issues/132
                // We bypass it by rewriting any pow(x,y) expression to the equivalent (x^y) expression
                // before the engine sees it. This loop handles nested calls.
                string previous;
                do
                {
                    previous = expression;
                    expression = PowRegex.Replace(previous, PowMatchEvaluator);
                } while (previous != expression);
                // WORKAROUND END

                var result = MagesEngine.Interpret(expression);

                if (result == null || string.IsNullOrEmpty(result.ToString()))
                {
                    return new List<Result>
                    {
                        new Result
                        {
                            Title = Localize.flowlauncher_plugin_calculator_expression_not_complete(),
                            IcoPath = IcoPath
                        }
                    };
                }

                if (result.ToString() == "NaN")
                    result = Localize.flowlauncher_plugin_calculator_not_a_number();

                if (result is Function)
                    result = Localize.flowlauncher_plugin_calculator_expression_not_complete();

                if (!string.IsNullOrEmpty(result.ToString()))
                {
                    decimal roundedResult = Math.Round(Convert.ToDecimal(result), _settings.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                    string newResult = FormatResult(roundedResult);

                    return new List<Result>
                    {
                        new Result
                        {
                            Title = newResult,
                            IcoPath = IcoPath,
                            Score = 300,
                            SubTitle = Localize.flowlauncher_plugin_calculator_copy_number_to_clipboard(),
                            CopyText = newResult,
                            Action = c =>
                            {
                                try
                                {
                                    Context.API.CopyToClipboard(newResult);
                                    return true;
                                }
                                catch (ExternalException)
                                {
                                    Context.API.ShowMsgBox(Localize.flowlauncher_plugin_calculator_failed_to_copy());
                                    return false;
                                }
                            }
                        }
                    };
                }
            }
            catch (Exception)
            {
                // Mages engine can throw various exceptions, for simplicity we catch them all and show a generic message.
                return new List<Result>
                {
                    new Result
                    {
                        Title = Localize.flowlauncher_plugin_calculator_expression_not_complete(),
                        IcoPath = IcoPath
                    }
                };
            }

            return EmptyResults;
        }

        private static string PowMatchEvaluator(Match m)
        {
            // m.Groups[1].Value will be `(...)` with parens
            var contentWithParen = m.Groups[1].Value;
            // remove outer parens. `(min(2,3), 4)` becomes `min(2,3), 4`
            var argsContent = contentWithParen.Substring(1, contentWithParen.Length - 2);

            var bracketCount = 0;
            var splitIndex = -1;

            // Find the top-level comma that separates the two arguments of pow.
            for (var i = 0; i < argsContent.Length; i++)
            {
                switch (argsContent[i])
                {
                    case '(':
                    case '[':
                        bracketCount++;
                        break;
                    case ')':
                    case ']':
                        bracketCount--;
                        break;
                    case ',' when bracketCount == 0:
                        splitIndex = i;
                        break;
                }

                if (splitIndex != -1)
                    break;
            }

            if (splitIndex == -1)
            {
                // This indicates malformed arguments for pow, e.g., pow(5) or pow().
                // Return original string to let Mages handle the error.
                return m.Value;
            }

            var arg1 = argsContent.Substring(0, splitIndex).Trim();
            var arg2 = argsContent.Substring(splitIndex + 1).Trim();

            // Check for empty arguments which can happen with stray commas, e.g., pow(,5)
            if (string.IsNullOrEmpty(arg1) || string.IsNullOrEmpty(arg2))
            {
                return m.Value;
            }

            return $"({arg1}^{arg2})";
        }

        private static string NormalizeNumber(string numberStr, bool isFunctionPresent, string decimalSep, string groupSep)
        {
            if (isFunctionPresent)
            {
                // STRICT MODE: When functions are present, ',' is ALWAYS an argument separator.
                if (numberStr.Contains(','))
                {
                    return numberStr;
                }

                string processedStr = numberStr;

                // Handle group separator, with special care for ambiguous dot.
                if (!string.IsNullOrEmpty(groupSep))
                {
                    if (groupSep == ".")
                    {
                        var parts = processedStr.Split('.');
                        if (parts.Length > 1)
                        {
                            var culture = CultureInfo.CurrentCulture;
                            if (IsValidGrouping(parts, culture.NumberFormat.NumberGroupSizes))
                            {
                                processedStr = processedStr.Replace(groupSep, "");
                            }
                            // If not grouped, it's likely a decimal number, so we don't strip dots.
                        }
                    }
                    else
                    {
                        processedStr = processedStr.Replace(groupSep, "");
                    }
                }

                // Handle decimal separator.
                if (decimalSep != ".")
                {
                    processedStr = processedStr.Replace(decimalSep, ".");
                }
                
                return processedStr;
            }
            else
            {
                // LENIENT MODE: No functions are present, so we can be flexible.
                string processedStr = numberStr;
                if (!string.IsNullOrEmpty(groupSep))
                {
                    processedStr = processedStr.Replace(groupSep, "");
                }
                if (decimalSep != ".")
                {
                    processedStr = processedStr.Replace(decimalSep, ".");
                }
                return processedStr;
            }
        }
        
        private static bool IsValidGrouping(string[] parts, int[] groupSizes)
        {
            if (parts.Length <= 1) return true;

            if (groupSizes is null || groupSizes.Length == 0 || groupSizes[0] == 0)
                return false; // has groups, but culture defines none.

            var firstPart = parts[0];
            if (firstPart.StartsWith("-")) firstPart = firstPart.Substring(1);
            if (firstPart.Length == 0) return false; // e.g. ",123"

            if (firstPart.Length > groupSizes[0]) return false;

            var lastGroupSize = groupSizes.Last();
            var canRepeatLastGroup = lastGroupSize != 0;
            
            int groupIndex = 0;
            for (int i = parts.Length - 1; i > 0; i--)
            {
                int expectedSize;
                if (groupIndex < groupSizes.Length)
                {
                    expectedSize = groupSizes[groupIndex];
                }
                else if(canRepeatLastGroup)
                {
                    expectedSize = lastGroupSize;
                }
                else
                {
                    return false;
                }

                if (parts[i].Length != expectedSize) return false;
                
                groupIndex++;
            }

            return true;
        }

        private string FormatResult(decimal roundedResult)
        {
            string decimalSeparator = GetDecimalSeparator();
            string groupSeparator = GetGroupSeparator(decimalSeparator);

            string resultStr = roundedResult.ToString(CultureInfo.InvariantCulture);

            string[] parts = resultStr.Split('.');
            string integerPart = parts[0];
            string fractionalPart = parts.Length > 1 ? parts[1] : string.Empty;

            if (integerPart.Length > 3)
            {
                integerPart = ThousandGroupRegex.Replace(integerPart, groupSeparator);
            }

            if (!string.IsNullOrEmpty(fractionalPart))
            {
                return integerPart + decimalSeparator + fractionalPart;
            }

            return integerPart;
        }

        private string GetGroupSeparator(string decimalSeparator)
        {
            var culture = CultureInfo.CurrentCulture;
            var systemGroupSeparator = culture.NumberFormat.NumberGroupSeparator;

            if (_settings.DecimalSeparator == DecimalSeparator.UseSystemLocale)
            {
                return systemGroupSeparator;
            }

            // When a custom decimal separator is used,
            // use the system's group separator unless it conflicts with the custom decimal separator.
            if (decimalSeparator == systemGroupSeparator)
            {
                // Conflict: use the opposite of the decimal separator as a fallback.
                return decimalSeparator == Dot ? Comma : Dot;
            }

            return systemGroupSeparator;
        }

        private string GetDecimalSeparator()
        {
            string systemDecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return _settings.DecimalSeparator switch
            {
                DecimalSeparator.UseSystemLocale => systemDecimalSeparator,
                DecimalSeparator.Dot => Dot,
                DecimalSeparator.Comma => Comma,
                _ => systemDecimalSeparator,
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_calculator_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_calculator_plugin_description();
        }

        public Control CreateSettingPanel()
        {
            return new CalculatorSettings(_settings);
        }

        public void OnCultureInfoChanged(CultureInfo newCulture)
        {
            DecimalSeparatorLocalized.UpdateLabels(_viewModel.AllDecimalSeparator);
        }
    }
}
