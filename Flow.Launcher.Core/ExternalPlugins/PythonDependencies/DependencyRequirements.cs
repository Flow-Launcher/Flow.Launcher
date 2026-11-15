using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class DependencyRequirements
    {
        private static readonly Regex ExactPin = new(
            @"^\s*[A-Za-z0-9][A-Za-z0-9._-]*\s*(\[[^\]]*\])?\s*===?\s*[A-Za-z0-9._+!-]+\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> Normalize(IEnumerable<string> requirements)
        {
            return requirements
                .Where(requirement => !string.IsNullOrWhiteSpace(requirement))
                .Select(requirement => requirement.Trim())
                .OrderBy(requirement => requirement, StringComparer.Ordinal)
                .ToList();
        }

        public static bool IsPinned(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement))
            {
                return false;
            }

            var markerStart = requirement.IndexOf(';');
            var specifier = markerStart < 0 ? requirement : requirement[..markerStart];
            return ExactPin.IsMatch(specifier);
        }

        public static IReadOnlyList<string> FindUnpinned(IEnumerable<string> requirements)
        {
            return Normalize(requirements).Where(requirement => !IsPinned(requirement)).ToList();
        }
    }
}
