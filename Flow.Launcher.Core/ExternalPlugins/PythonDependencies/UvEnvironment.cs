#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using Flow.Launcher.Infrastructure.Http;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class UvEnvironment
    {
        /// <summary>
        /// The proxy Flow's own downloads use right now, or null when there is none.
        /// </summary>
        public static string? FlowProxyUrl()
        {
            return ProxyUrl(Http.WebProxy.Address, Http.WebProxy.Credentials as NetworkCredential);
        }

        public static IReadOnlyDictionary<string, string> Build(bool portable, string uvCachePath, string? proxyUrl)
        {
            var environment = new Dictionary<string, string> { ["UV_NO_CONFIG"] = "1" };
            if (portable)
            {
                environment["UV_CACHE_DIR"] = uvCachePath;
            }

            // uv doesn't see Flow's proxy setting, so it gets the same proxy Flow's own downloads use.
            if (proxyUrl is not null)
            {
                environment["HTTP_PROXY"] = proxyUrl;
                environment["HTTPS_PROXY"] = proxyUrl;
            }

            return environment;
        }

        public static string? ProxyUrl(Uri? address, NetworkCredential? credential)
        {
            if (address is null)
            {
                return null;
            }

            var userInfo = string.IsNullOrEmpty(credential?.UserName)
                ? string.Empty
                : $"{Uri.EscapeDataString(credential.UserName)}:{Uri.EscapeDataString(credential.Password ?? string.Empty)}@";
            return $"{address.Scheme}://{userInfo}{address.Authority}";
        }
    }
}
