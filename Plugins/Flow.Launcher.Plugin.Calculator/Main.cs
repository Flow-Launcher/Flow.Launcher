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
            if (!CanCalculate(query))
            {
                return new List<Result>();
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
                    expression = Regex.Replace(previous, @"\bpow\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "($1^$2)");
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

        /// <summary>
        /// Parses a string representation of a number using the system's current culture.
        /// </summary>
        /// <returns>A normalized number string with '.' as the decimal separator for the Mages engine.</returns>
        private string NormalizeNumber(string numberStr)
        {
            var culture = CultureInfo.CurrentCulture;
            var groupSep = culture.NumberFormat.NumberGroupSeparator;

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

            // At this point, any group separators are in valid positions (or there are none).
            // We can safely parse with the user's culture.
            if (decimal.TryParse(numberStr, NumberStyles.Any, culture, out var number))
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            return numberStr;
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

        private bool CanCalculate(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return false;
            }

            if (!IsBracketComplete(query.Search))
            {
                return false;
            }

            return true;
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
