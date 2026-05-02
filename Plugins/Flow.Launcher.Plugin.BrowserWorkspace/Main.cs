using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Flow.Launcher.Plugin.BrowserWorkspace.Models;

namespace Flow.Launcher.Plugin.BrowserWorkspace;

public class Main : IPlugin, IReloadable
{
    private const string ClassName = nameof(Main);

    private static PluginInitContext _context;
    private List<EdgeWorkspace> _cachedWorkspaces = new();

    public void Init(PluginInitContext context)
    {
        _context = context;
        LoadWorkspaces();
    }

    public List<Result> Query(Query query)
    {
        string search = query.Search.Trim();

        IEnumerable<EdgeWorkspace> candidates = string.IsNullOrEmpty(search)
            ? _cachedWorkspaces
            : _cachedWorkspaces.Where(w =>
                w.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                w.Profile.Contains(search, StringComparison.OrdinalIgnoreCase));

        return candidates.Select(ws =>
        {
            string subtitle = ws.Count > 0
                ? $"Edge Workspace · Profile: {ws.Profile} · {ws.Count} tab{(ws.Count == 1 ? "" : "s")}"
                : $"Edge Workspace · Profile: {ws.Profile}";

            return new Result
            {
                Title = ws.Name,
                SubTitle = subtitle,
                IcoPath = @"Images\workspace.png",
                Score = CalculateScore(ws, search),
                Action = _ =>
                {
                    LaunchWorkspace(ws);
                    return true;
                }
            };
        })
        .Where(r => r.Score > 0 || string.IsNullOrEmpty(search))
        .OrderByDescending(r => r.Score)
        .ToList();
    }

    public void ReloadData() => LoadWorkspaces();

    // -----------------------------------------------------------------------

    private void LoadWorkspaces()
    {
        try
        {
            _cachedWorkspaces = EdgeWorkspaceLoader.LoadAll();
        }
        catch (Exception ex)
        {
            _context?.API.LogException(ClassName, "Failed to load Edge workspaces", ex);
            _cachedWorkspaces = new List<EdgeWorkspace>();
        }
    }

    private static void LaunchWorkspace(EdgeWorkspace ws)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ws.EdgeExecutablePath,
                Arguments = $"--profile-directory={ws.Profile} --launch-workspace={ws.Id}",
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _context?.API.LogException(ClassName, $"Failed to launch workspace '{ws.Name}'", ex);
        }
    }

    private static int CalculateScore(EdgeWorkspace ws, string search)
    {
        if (string.IsNullOrEmpty(search)) return 50;

        // Exact match
        if (ws.Name.Equals(search, StringComparison.OrdinalIgnoreCase)) return 100;
        // Starts-with
        if (ws.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase)) return 80;
        // Contains
        if (ws.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) return 60;
        // Profile match
        if (ws.Profile.Contains(search, StringComparison.OrdinalIgnoreCase)) return 30;

        return 0;
    }
}
