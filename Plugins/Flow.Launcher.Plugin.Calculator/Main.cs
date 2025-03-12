using System;
using System.Collections.Generic;
using System.Globalization;
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
                        @"[ei]|[0-9]|[\+\%\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
                        @")+$", RegexOptions.Compiled);
        private static readonly Regex RegBrackets = new Regex(@"[\(\)\[\]]", RegexOptions.Compiled);
        private static Engine MagesEngine;
        private const string comma = ",";
        private const string dot = ".";

        private PluginInitContext Context { get; set; }

        private static Settings _settings;
        private static SettingsViewModel _viewModel;

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
                string expression;

                switch (_settings.DecimalSeparator)
                {
                    case DecimalSeparator.Comma:
                    case DecimalSeparator.UseSystemLocale when CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ",":
                        expression = query.Search.Replace(",", ".");
                        break;
                    default:
                        expression = query.Search;
                        break;
                }

                var result = MagesEngine.Interpret(expression);

                if (result?.ToString() == "NaN")
                    result = Context.API.GetTranslation("flowlauncher_plugin_calculator_not_a_number");

                if (result is Function)
                    result = Context.API.GetTranslation("flowlauncher_plugin_calculator_expression_not_complete");

                if (!string.IsNullOrEmpty(result?.ToString()))
                {
                    decimal roundedResult = Math.Round(Convert.ToDecimal(result), _settings.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                    string newResult = ChangeDecimalSeparator(roundedResult, GetDecimalSeparator());

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
                                    Context.API.ShowMsgBox("Copy failed, please try later");
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

            if ((query.Search.Contains(dot) && GetDecimalSeparator() != dot) ||
                (query.Search.Contains(comma) && GetDecimalSeparator() != comma))
                return false;

            return true;
        }

        private string ChangeDecimalSeparator(decimal value, string newDecimalSeparator)
        {
            if (String.IsNullOrEmpty(newDecimalSeparator))
            {
                return value.ToString();
            }

            var numberFormatInfo = new NumberFormatInfo
            {
                NumberDecimalSeparator = newDecimalSeparator
            };
            return value.ToString(numberFormatInfo);
        }

        private string GetDecimalSeparator()
        {
            string systemDecimalSeperator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            switch (_settings.DecimalSeparator)
            {
                case DecimalSeparator.UseSystemLocale: return systemDecimalSeperator;
                case DecimalSeparator.Dot: return dot;
                case DecimalSeparator.Comma: return comma;
                default: return systemDecimalSeperator;
            }
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
