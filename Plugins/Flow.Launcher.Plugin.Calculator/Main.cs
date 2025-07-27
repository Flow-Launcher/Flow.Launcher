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
        private static readonly Regex RegValidExpressChar = MainRegexHelper.GetRegValidExpressChar();
        private static readonly Regex RegBrackets = MainRegexHelper.GetRegBrackets();
        private static readonly Regex ThousandGroupRegex = new Regex(@"\B(?=(\d{3})+(?!\d))", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(@"[\d\.,]+", RegexOptions.Compiled);


        private static Engine MagesEngine;
        private const string Comma = ",";
        private const string Dot = ".";

        internal static PluginInitContext Context { get; set; } = null!;

        private Settings _settings;
        private SettingsViewModel _viewModel;

        /// <summary>
        /// Holds the formatting information for a single query.
        /// This is used to ensure thread safety by keeping query state local.
        /// </summary>
        private class ParsingContext
        {
            public string InputDecimalSeparator { get; set; }
            public bool InputUsesGroupSeparators { get; set; }
        }

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

            var context = new ParsingContext();

            try
            {
                var expression = NumberRegex.Replace(query.Search, m => NormalizeNumber(m.Value, context));

                var result = MagesEngine.Interpret(expression);

                if (result?.ToString() == "NaN")
                    result = Localize.flowlauncher_plugin_calculator_not_a_number();

                if (result is Function)
                    result = Localize.flowlauncher_plugin_calculator_expression_not_complete();

                if (!string.IsNullOrEmpty(result?.ToString()))
                {
                    decimal roundedResult = Math.Round(Convert.ToDecimal(result), _settings.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                    string newResult = FormatResult(roundedResult, context);

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
                // ignored
            }

            return new List<Result>();
        }

        /// <summary>
        /// Parses a string representation of a number, detecting its format. It uses structural analysis
        /// and falls back to system culture for truly ambiguous cases (e.g., "1,234").
        /// It populates the provided ParsingContext with the detected format for later use.
        /// </summary>
        /// <returns>A normalized number string with '.' as the decimal separator for the Mages engine.</returns>
        private string NormalizeNumber(string numberStr, ParsingContext context)
        {
            var systemGroupSep = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
            int dotCount = numberStr.Count(f => f == '.');
            int commaCount = numberStr.Count(f => f == ',');

            // Case 1: Unambiguous mixed separators (e.g., "1.234,56")
            if (dotCount > 0 && commaCount > 0)
            {
                context.InputUsesGroupSeparators = true;
                if (numberStr.LastIndexOf('.') > numberStr.LastIndexOf(','))
                {
                    context.InputDecimalSeparator = Dot;
                    return numberStr.Replace(Comma, string.Empty);
                }
                else
                {
                    context.InputDecimalSeparator = Comma;
                    return numberStr.Replace(Dot, string.Empty).Replace(Comma, Dot);
                }
            }

            // Case 2: Only dots
            if (dotCount > 0)
            {
                if (dotCount > 1)
                {
                    context.InputUsesGroupSeparators = true;
                    return numberStr.Replace(Dot, string.Empty);
                }
                // A number is ambiguous if it has a single Dot in the thousands position,
                // and does not start with a "0." or "."
                bool isAmbiguous = numberStr.Length - numberStr.LastIndexOf('.') == 4
                                   && !numberStr.StartsWith("0.")
                                   && !numberStr.StartsWith(".");
                if (isAmbiguous)
                {
                    if (systemGroupSep == Dot)
                    {
                        context.InputUsesGroupSeparators = true;
                        return numberStr.Replace(Dot, string.Empty);
                    }
                    else
                    {
                        context.InputDecimalSeparator = Dot;
                        return numberStr;
                    }
                }
                else // Unambiguous decimal (e.g., "12.34" or "0.123" or ".123")
                {
                    context.InputDecimalSeparator = Dot;
                    return numberStr;
                }
            }

            // Case 3: Only commas
            if (commaCount > 0)
            {
                if (commaCount > 1)
                {
                    context.InputUsesGroupSeparators = true;
                    return numberStr.Replace(Comma, string.Empty);
                }
                // A number is ambiguous if it has a single Comma in the thousands position,
                // and does not start with a "0," or ","
                bool isAmbiguous = numberStr.Length - numberStr.LastIndexOf(',') == 4
                                   && !numberStr.StartsWith("0,")
                                   && !numberStr.StartsWith(",");
                if (isAmbiguous)
                {
                    if (systemGroupSep == Comma)
                    {
                        context.InputUsesGroupSeparators = true;
                        return numberStr.Replace(Comma, string.Empty);
                    }
                    else
                    {
                        context.InputDecimalSeparator = Comma;
                        return numberStr.Replace(Comma, Dot);
                    }
                }
                else // Unambiguous decimal (e.g., "12,34" or "0,123" or ",123")
                {
                    context.InputDecimalSeparator = Comma;
                    return numberStr.Replace(Comma, Dot);
                }
            }

            // Case 4: No separators
            return numberStr;
        }

        private string FormatResult(decimal roundedResult, ParsingContext context)
        {
            string decimalSeparator = context.InputDecimalSeparator ?? GetDecimalSeparator();
            string groupSeparator = GetGroupSeparator(decimalSeparator);

            string resultStr = roundedResult.ToString(CultureInfo.InvariantCulture);

            string[] parts = resultStr.Split('.');
            string integerPart = parts[0];
            string fractionalPart = parts.Length > 1 ? parts[1] : string.Empty;

            if (context.InputUsesGroupSeparators && integerPart.Length > 3)
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
            // This logic is now independent of the system's group separator
            // to ensure consistent output for unit testing.
            return decimalSeparator == Dot ? Comma : Dot;
        }

        private bool CanCalculate(Query query)
        {
            if (query.Search.Length < 2)
            {
                return false;
            }

            if (!RegValidExpressChar.IsMatch(query.Search))
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
            return Localize.flowlauncher_plugin_caculator_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_caculator_plugin_description();
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
