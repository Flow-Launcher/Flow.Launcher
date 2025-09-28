#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public partial class HtmlFaviconParser
{
    [GeneratedRegex("<link[^>]+?>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex LinkTagRegex();
    [GeneratedRegex("rel\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RelAttributeRegex();
    [GeneratedRegex("href\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex HrefAttributeRegex();
    [GeneratedRegex("sizes\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex SizesAttributeRegex();
    [GeneratedRegex("<base[^>]+href\\s*=\\s*(?:['\"](?<v>[^'\"]*)['\"]|(?<v>[^>\\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex BaseHrefRegex();

    public List<FaviconCandidate> Parse(string htmlContent, Uri originalBaseUri)
    {
        var candidates = new List<FaviconCandidate>();
        var effectiveBaseUri = originalBaseUri;

        var baseMatch = BaseHrefRegex().Match(htmlContent);
        if (baseMatch.Success)
        {
            var baseHref = baseMatch.Groups["v"].Value;
            if (Uri.TryCreate(originalBaseUri, baseHref, out var newBaseUri))
            {
                effectiveBaseUri = newBaseUri;
            }
        }

        foreach (Match linkMatch in LinkTagRegex().Matches(htmlContent))
        {
            var linkTag = linkMatch.Value;
            var relMatch = RelAttributeRegex().Match(linkTag);
            if (!relMatch.Success || !relMatch.Groups["v"].Value.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;

            var hrefMatch = HrefAttributeRegex().Match(linkTag);
            if (!hrefMatch.Success) continue;

            var href = hrefMatch.Groups["v"].Value;
            if (string.IsNullOrWhiteSpace(href)) continue;

            Main.Context.API.LogDebug(nameof(Parse), $"Found potential favicon link. Raw tag: '{linkTag}', Extracted href: '{href}', Base URI: '{effectiveBaseUri}'");

            if (href.StartsWith("//"))
            {
                href = effectiveBaseUri.Scheme + ":" + href;
            }

            if (!Uri.TryCreate(effectiveBaseUri, href, out var fullUrl))
            {
                Main.Context.API.LogWarn(nameof(Parse), $"Failed to create a valid URI from href: '{href}' and base URI: '{effectiveBaseUri}'");
                continue;
            }

            var score = CalculateFaviconScore(linkTag, fullUrl.ToString());
            candidates.Add(new FaviconCandidate(fullUrl.ToString(), score));

            if (score >= ImageConverter.TargetIconSize)
            {
                Main.Context.API.LogDebug(nameof(Parse), $"Found suitable favicon candidate (score: {score}). Halting further HTML parsing.");
                break;
            }
        }

        return candidates;
    }

    private static int CalculateFaviconScore(string linkTag, string fullUrl)
    {
        var extension = Path.GetExtension(fullUrl).ToUpperInvariant();
        if (extension == ".SVG") return 10000;

        var sizesMatch = SizesAttributeRegex().Match(linkTag);
        if (sizesMatch.Success)
        {
            var sizesValue = sizesMatch.Groups["v"].Value.ToUpperInvariant();
            if (sizesValue == "ANY") return 1000;

            var firstSizePart = sizesValue.Split(' ')[0];
            if (int.TryParse(firstSizePart.Split('X')[0], out var size))
            {
                return size;
            }
        }

        if (extension == ".ICO") return 32;

        return 16;
    }
}
