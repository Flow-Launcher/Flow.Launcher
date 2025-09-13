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
        private static readonly Regex RegBrackets = MainRegexHelper.GetRegBrackets();
        private static readonly Regex ThousandGroupRegex = MainRegexHelper.GetThousandGroupRegex();
        private static readonly Regex NumberRegex = MainRegexHelper.GetNumberRegex();
        private static readonly Regex PowRegex = new(@"\bpow(\((?:[^()\[\]]|\((?<Depth>)|\)(?<-Depth>)|\[(?<Depth>)|\](?<-Depth>))*(?(Depth)(?!))\))", RegexOptions.Compiled | RegexOptions.RightToLeft);


        private static Engine MagesEngine;
        private const string Comma = ",";
        private const string Dot = ".";

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
                return new List<Result>();
            }

            if (!IsBracketComplete(query.Search))
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = Localize.flowlauncher_plugin_calculator_expression_not_complete(),
                        IcoPath = "Images/calculator.png"
                    }
                };
            }

            try
            {
                var expression = NumberRegex.Replace(query.Search, m => NormalizeNumber(m.Value));

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
                            IcoPath = "Images/calculator.png"
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
                            IcoPath = "Images/calculator.png",
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
                        IcoPath = "Images/calculator.png"
                    }
                };
            }

            return new List<Result>();
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

        /// <summary>
        /// Parses a string representation of a number using the system's current culture.
        /// </summary>
        /// <returns>A normalized number string with '.' as the decimal separator for the Mages engine.</returns>
        private string NormalizeNumber(string numberStr)
        {
            var culture = CultureInfo.CurrentCulture;
            var groupSep = culture.NumberFormat.NumberGroupSeparator;
            var decimalSep = culture.NumberFormat.NumberDecimalSeparator;

            // If the string contains the group separator, check if it's used correctly.
            if (!string.IsNullOrEmpty(groupSep) && numberStr.Contains(groupSep))
            {
                var parts = numberStr.Split(groupSep);
                // If any part after the first (excluding a possible last part with a decimal)
                // does not have 3 digits, then it's not a valid use of a thousand separator.
                for (int i = 1; i < parts.Length; i++)
                {
                    var part = parts[i];
                    // The last part might contain a decimal separator.
                    if (i == parts.Length - 1 && part.Contains(culture.NumberFormat.NumberDecimalSeparator))
                    {
                        part = part.Split(culture.NumberFormat.NumberDecimalSeparator)[0];
                    }

                    if (part.Length != 3)
                    {
                        // This is not a number with valid thousand separators,
                        // so it must be arguments to a function. Return it unmodified.
                        return numberStr;
                    }
                }
            }

            // If validation passes, we can assume the separators are used correctly for numbers.
            string processedStr = numberStr.Replace(groupSep, "");
            processedStr = processedStr.Replace(decimalSep, ".");

            return processedStr;
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
            if (_settings.DecimalSeparator == DecimalSeparator.UseSystemLocale)
            {
                return CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
            }

            // This logic is now independent of the system's group separator
            // to ensure consistent output when a specific separator is chosen.
            return decimalSeparator == Dot ? Comma : Dot;
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

        private static bool IsBracketComplete(string query)
        {
            var matchs = RegBrackets.Matches(query);
            var leftBracketCount = 0;
            foreach (Match match in matchs)
            {
                if (match.Value == "(" || match.Value == "[")
                {
                    leftBracketCount++;
                }
                else
                {
                    leftBracketCount--;
                }

                if (leftBracketCount < 0)
                {
                    return false;
                }
            }

            return leftBracketCount == 0;
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
