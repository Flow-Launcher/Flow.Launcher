using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Controls;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Url
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
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

        private static string GetHttpPreference()
        {
            return Settings.AlwaysOpenWithHttps ? "https" : "http";
        }

        public bool IsURL(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var input = raw.Trim();

            // Exclude numbers (e.g. 1.2345)
            if (decimal.TryParse(input, out _))
                return false;

            // Check if it's a bare IP address (without protocol)
            var inputHost = Uri.TryCreate(input, UriKind.Absolute, out var tempUri) ? tempUri.Host : input.Split(['/', ':'])[0].Trim('[', ']');
            if (IPAddress.TryParse(inputHost, out var ip))
            {
                // Exclude invalid address 0.0.0.0
                if (ip.Equals(IPAddress.Any))
                    return false;

                return true;
            }

            // Check if it's a bare IPv6 address (contains multiple colons but no protocol)
            if (input.Count(c => c == ':') > 1 && !input.Contains("://"))
            {
                var ipv6Part = input.Split('/')[0].Trim('[', ']');
                if (IPAddress.TryParse(ipv6Part, out _))
                    return true;
            }

            // Validate using Uri after adding protocol
            var urlToValidate = input;
            if (!UrlSchemes.Any(s => input.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                urlToValidate = GetHttpPreference() + "://" + input;
            }

            if (!Uri.TryCreate(urlToValidate, UriKind.Absolute, out var uri))
                return false;

            // Validate protocol
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFtp)
                return false;

            // Validate host: must contain a dot (domain), be localhost, or be a valid IP
            var host = uri.Host;
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            if (IPAddress.TryParse(host, out var hostIp))
                return !hostIp.Equals(IPAddress.Any);

            // Domain must contain at least one dot, and dot cannot be at the start or end
            if (!host.Contains('.'))
                return false;

            // Ensure valid domain format (not starting or ending with dot, TLD at least 2 characters)
            var parts = host.Split('.');
            if (parts.Length < 2 || parts.Any(string.IsNullOrEmpty))
                return false;

            // TLD must be at least 2 characters
            var tld = parts[^1];
            return tld.Length >= 2 && tld.All(char.IsLetter);
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
