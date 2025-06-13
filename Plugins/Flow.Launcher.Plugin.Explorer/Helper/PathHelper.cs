using System;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.Explorer.Search;

namespace Flow.Launcher.Plugin.Explorer.Helper;

public static class PathHelper
{
    public static string GetPathName(this string selectedPath)
    {
        if (string.IsNullOrEmpty(selectedPath)) return string.Empty;
        var path = selectedPath.EndsWith(Constants.DirectorySeparator) ? selectedPath[0..^1] : selectedPath;

        if (path.EndsWith(':'))
            return path[0..^1] + " Drive";

        return path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None)
            .Last();
    }
}
