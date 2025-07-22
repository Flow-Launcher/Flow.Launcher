using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Mages.Core;
using Flow.Launcher.Plugin.Calculator.ViewModels;
using Flow.Launcher.Plugin.Calculator.Views;

namespace Flow.Launcher.Plugin.Calculator
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        private static readonly Regex RegValidExpressChar = new Regex(
                        @"^(" +
                        @"ceil|floor|exp|pi|e|max|min|det|abs|log|ln|sqrt|" +
                        @"sin|cos|tan|arcsin|arccos|arctan|" +
                        @"eigval|eigvec|eig|sum|polar|plot|round|sort|real|zeta|" +
                        @"bin2dec|hex2dec|oct2dec|" +
                        @"factorial|sign|isprime|isinfty|" +
                        @"==|~=|&&|\|\||(?:\<|\>)=?|" +
                        @"[ei]|[0-9]|0x[\da-fA-F]+|[\+\%\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
                        @")+$", RegexOptions.Compiled);
        private static readonly Regex RegBrackets = new Regex(@"[\(\)\[\]]", RegexOptions.Compiled);
        private static readonly Regex ThousandGroupRegex = new Regex(@"\B(?=(\d{3})+(?!\d))");
        private static Engine MagesEngine;
        private const string comma = ",";
        private const string dot = ".";

        private PluginInitContext Context { get; set; }

        private static Settings _settings;
        private static SettingsViewModel _viewModel;

        private string _inputDecimalSeparator;
        private bool _inputUsesGroupSeparators;

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

            _inputDecimalSeparator = null;
            _inputUsesGroupSeparators = false;

            try
            {
                var numberRegex = new Regex(@"[\d\.,]+");
                var expression = numberRegex.Replace(query.Search, m => NormalizeNumber(m.Value));

                var result = MagesEngine.Interpret(expression);

                if (result?.ToString() == "NaN")
                    result = Context.API.GetTranslation("flowlauncher_plugin_calculator_not_a_number");

                if (result is Function)
                    result = Context.API.GetTranslation("flowlauncher_plugin_calculator_expression_not_complete");

                if (!string.IsNullOrEmpty(result?.ToString()))
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
                            SubTitle = Context.API.GetTranslation("flowlauncher_plugin_calculator_copy_number_to_clipboard"),
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
                                    Context.API.ShowMsgBox(Context.API.GetTranslation("flowlauncher_plugin_calculator_failed_to_copy"));
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
        /// (checking for 3-digit groups) and falls back to system culture for ambiguous cases (e.g., "1,234").
        /// It sets instance fields to remember the user's format for later output formatting.
        /// </summary>
        /// <returns>A normalized number string with '.' as the decimal separator for the Mages engine.</returns>
        private string NormalizeNumber(string numberStr)
        {
            var systemFormat = CultureInfo.CurrentCulture.NumberFormat;
            string systemDecimalSeparator = systemFormat.NumberDecimalSeparator;

            bool hasDot = numberStr.Contains(dot);
            bool hasComma = numberStr.Contains(comma);

            // Unambiguous case: both separators are present. The last one wins as decimal separator.
            if (hasDot && hasComma)
            {
                _inputUsesGroupSeparators = true;
                int lastDotPos = numberStr.LastIndexOf(dot);
                int lastCommaPos = numberStr.LastIndexOf(comma);

                if (lastDotPos > lastCommaPos) // e.g. 1,234.56
                {
                    _inputDecimalSeparator = dot;
                    return numberStr.Replace(comma, string.Empty);
                }
                else // e.g. 1.234,56
                {
                    _inputDecimalSeparator = comma;
                    return numberStr.Replace(dot, string.Empty).Replace(comma, dot);
                }
            }

            if (hasComma)
            {
                string[] parts = numberStr.Split(',');
                // If all parts after the first are 3 digits, it's a potential group separator.
                bool isGroupCandidate = parts.Length > 1 && parts.Skip(1).All(p => p.Length == 3);

                if (isGroupCandidate)
                {
                    // Ambiguous case: "1,234". Resolve using culture.
                    if (systemDecimalSeparator == comma)
                    {
                        _inputDecimalSeparator = comma;
                        return numberStr.Replace(comma, dot);
                    }
                    else
                    {
                        _inputUsesGroupSeparators = true;
                        return numberStr.Replace(comma, string.Empty);
                    }
                }
                else
                {
                    // Unambiguous decimal: "123,45" or "1,2,345"
                    _inputDecimalSeparator = comma;
                    return numberStr.Replace(comma, dot);
                }
            }

            if (hasDot)
            {
                string[] parts = numberStr.Split('.');
                bool isGroupCandidate = parts.Length > 1 && parts.Skip(1).All(p => p.Length == 3);

                if (isGroupCandidate)
                {
                    if (systemDecimalSeparator == dot)
                    {
                        _inputDecimalSeparator = dot;
                        return numberStr;
                    }
                    else
                    {
                        _inputUsesGroupSeparators = true;
                        return numberStr.Replace(dot, string.Empty);
                    }
                }
                else
                {
                    _inputDecimalSeparator = dot;
                    return numberStr; // Already in Mages-compatible format
                }
            }

            // No separators.
            return numberStr;
        }

        private string FormatResult(decimal roundedResult)
        {
            // Use the detected decimal separator from the input; otherwise, fall back to settings.
            string decimalSeparator = _inputDecimalSeparator ?? GetDecimalSeparator();
            string groupSeparator = decimalSeparator == dot ? comma : dot;

            string resultStr = roundedResult.ToString(CultureInfo.InvariantCulture);

            string[] parts = resultStr.Split('.');
            string integerPart = parts[0];
            string fractionalPart = parts.Length > 1 ? parts[1] : string.Empty;

            if (_inputUsesGroupSeparators)
            {
                integerPart = ThousandGroupRegex.Replace(integerPart, groupSeparator);
            }

            if (!string.IsNullOrEmpty(fractionalPart))
            {
                return integerPart + decimalSeparator + fractionalPart;
            }

            return integerPart;
        }

        private bool CanCalculate(Query query)
        {
            // Don't execute when user only input "e" or "i" keyword
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

        private static string GetDecimalSeparator()
        {
            string systemDecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return _settings.DecimalSeparator switch
            {
                DecimalSeparator.UseSystemLocale => systemDecimalSeparator,
                DecimalSeparator.Dot => dot,
                DecimalSeparator.Comma => comma,
                _ => systemDecimalSeparator,
            };
        }

        private bool IsBracketComplete(string query)
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
            return Context.API.GetTranslation("flowlauncher_plugin_caculator_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("flowlauncher_plugin_caculator_plugin_description");
        }

        public Control CreateSettingPanel()
        {
            return new CalculatorSettings(_viewModel);
        }
    }
}
