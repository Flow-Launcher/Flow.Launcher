using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Url
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        // Simplified pattern to quickly reject obviously invalid inputs
        // Actual validation is done by Uri.TryCreate which properly handles all URL formats
        private const string UrlPattern = "^" +
            // Optional protocol
            "(?:(?:https?|ftp)://)?" +
            // Optional user authentication
            "(?:[^@\\s]+@)?" +
            // Must contain at least one of:
            "(?:" +
            // - protocol with something after it
            "(?:(?:https?|ftp)://).+" +
            "|" +
            // - IPv6 address (simplified detection - just look for colons in brackets or multiple colons)
            "(?:\\[[0-9a-fA-F:]+\\]|[0-9a-fA-F]*:[0-9a-fA-F:]+)" +
            "|" +
            // - IPv4 address (basic pattern)
            "(?:[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3})" +
            "|" +
            // - localhost
            "localhost" +
            "|" +
            // - domain with TLD (at least one dot with valid characters)
            "(?:[a-z0-9-]+\\.)+[a-z]{2,}" +
            ")" +
            // Optional port and path
            "(?::[0-9]{1,5})?(?:/.*)?$";
            
        private readonly Regex UrlRegex = new(UrlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal static PluginInitContext Context { get; private set; }
        internal static Settings Settings { get; private set; }

        private static readonly string[] UrlSchemes = ["http://", "https://", "ftp://"];

        public List<Result> Query(Query query)
        {
            var raw = query.Search;
            if (!IsURL(raw))
            {
                return [];
            }

            return
                [
                    new()
                    {
                        Title = raw,
                        SubTitle = Localize.flowlauncher_plugin_url_open_url(raw),
                        IcoPath = "Images/url.png",
                        Score = 8,
                        Action = _ =>
                        {
                            // not a recognized scheme, add preferred http scheme
                            if (!UrlSchemes.Any(scheme => raw.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
                            {
                                raw = GetHttpPreference() + "://" + raw;
                            }
                            try
                            {
                                if (Settings.UseCustomBrowser)
                                {
                                    if (Settings.OpenInNewBrowserWindow)
                                    {
                                        SearchWeb.OpenInBrowserWindow(raw, Settings.BrowserPath, Settings.OpenInPrivateMode, Settings.PrivateModeArgument);
                                    }
                                    else
                                    {
                                        SearchWeb.OpenInBrowserTab(raw, Settings.BrowserPath, Settings.OpenInPrivateMode, Settings.PrivateModeArgument);
                                    }
                                }
                                else
                                {
                                    Context.API.OpenWebUrl(raw);
                                }

                                return true;
                            }
                            catch(Exception)
                            {
                                Context.API.ShowMsgError(Localize.flowlauncher_plugin_url_cannot_open_url(raw));
                                return false;
                            }
                        }
                    }
                ];
        }

        private string GetHttpPreference()
        {
            return Settings.AlwaysOpenWithHttps ? "https" : "http";
        }

        public bool IsURL(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var input = raw.Trim();
            
            // Quick pre-filter with regex to reject obviously invalid inputs
            if (!UrlRegex.IsMatch(input))
                return false;

            // Pre-validate IPv4 addresses (Uri accepts invalid octets like 256)
            // Match pattern: optional scheme/auth + IPv4 + optional port/path
            var ipv4Match = Regex.Match(input, @"(?:(?:https?|ftp)://)?(?:[^@/]+@)?(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(?:[:/]|$)");
            if (ipv4Match.Success)
            {
                var octets = ipv4Match.Groups[1].Value.Split('.');
                foreach (var octet in octets)
                {
                    if (!byte.TryParse(octet, out _))
                        return false;
                }
                
                if (ipv4Match.Groups[1].Value == "0.0.0.0")
                    return false;
            }

            // Prepare URL for Uri.TryCreate validation
            var urlToValidate = input;
            
            // Add scheme if missing
            if (!UrlSchemes.Any(s => input.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                // Handle bare IPv6 addresses (multiple colons, no @ for auth, no :// for scheme)
                if (input.Count(c => c == ':') > 1 && !input.Contains("://") && !input.Contains('@'))
                {
                    // Add brackets if not already present
                    if (!input.StartsWith('['))
                    {
                        // Check if there's a port at the end (]:port pattern)
                        var portMatch = Regex.Match(input, @"\]:(\d{1,5})$");
                        if (!portMatch.Success)
                        {
                            urlToValidate = $"{GetHttpPreference()}://[{input}]";
                        }
                        else
                        {
                            urlToValidate = GetHttpPreference() + "://" + input;
                        }
                    }
                    else
                    {
                        urlToValidate = GetHttpPreference() + "://" + input;
                    }
                }
                else
                {
                    urlToValidate = GetHttpPreference() + "://" + input;
                }
            }

            // Use Uri.TryCreate for comprehensive validation
            return Uri.TryCreate(urlToValidate, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;

            Settings = context.API.LoadSettingJsonStorage<Settings>();
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_url_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_url_plugin_description();
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl();
        }
    }
}
