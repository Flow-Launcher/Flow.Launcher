using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.BrowserWorkspace.LevelDb;
using Flow.Launcher.Plugin.BrowserWorkspace.Models;

namespace Flow.Launcher.Plugin.BrowserWorkspace;

/// <summary>
/// Discovers Microsoft Edge installations and loads their workspaces.
///
/// Supports two storage formats:
///   v1 – workspace list stored as JSON in <c>Workspaces\WorkspacesCache</c>.
///   v2 – each workspace stored as a JSON value in a LevelDB database at
///         <c>Workspaces\LevelDB\</c> (introduced approximately in Edge 146).
/// </summary>
internal static class EdgeWorkspaceLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // -----------------------------------------------------------------------
    // Entry point

    public static List<EdgeWorkspace> LoadAll()
    {
        var result = new List<EdgeWorkspace>();
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        foreach (var (executablePath, userDataPath) in DiscoverEdgeInstances(localApp))
        {
            foreach (var profile in DiscoverProfiles(userDataPath))
            {
                var workspaces = LoadWorkspacesForProfile(userDataPath, profile, executablePath);
                result.AddRange(workspaces);
            }
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Edge instance discovery

    /// <summary>Returns (executablePath, userDataPath) pairs for each found Edge installation.</summary>
    private static IEnumerable<(string Executable, string UserData)> DiscoverEdgeInstances(string localApp)
    {
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        // Standard channel subdirectories under %ProgramFiles(x86)%\Microsoft\
        var channelDirs = new[]
        {
            // Stable
            (Exe: Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
             Data: Path.Combine(localApp, "Microsoft", "Edge", "User Data")),
            // Beta
            (Exe: Path.Combine(programFilesX86, "Microsoft", "Edge Beta", "Application", "msedge.exe"),
             Data: Path.Combine(localApp, "Microsoft", "Edge Beta", "User Data")),
            // Dev
            (Exe: Path.Combine(programFilesX86, "Microsoft", "Edge Dev", "Application", "msedge.exe"),
             Data: Path.Combine(localApp, "Microsoft", "Edge Dev", "User Data")),
            // Canary (installed per-user)
            (Exe: Path.Combine(localApp, "Microsoft", "Edge SxS", "Application", "msedge.exe"),
             Data: Path.Combine(localApp, "Microsoft", "Edge SxS", "User Data")),
        };

        foreach (var (exe, data) in channelDirs)
        {
            if (File.Exists(exe) && Directory.Exists(data))
                yield return (exe, data);
        }
    }

    // -----------------------------------------------------------------------
    // Profile discovery

    private static IEnumerable<string> DiscoverProfiles(string userDataPath)
    {
        if (!Directory.Exists(userDataPath)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(userDataPath))
        {
            string name = Path.GetFileName(dir);
            // Edge/Chrome profiles are named "Default" or "Profile N"
            if (name == "Default" || (name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) &&
                                       int.TryParse(name.AsSpan("Profile ".Length), out _)))
            {
                yield return name;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Workspace loading (v1 + v2)

    private static List<EdgeWorkspace> LoadWorkspacesForProfile(
        string userDataPath, string profile, string executablePath)
    {
        string workspacesDir = Path.Combine(userDataPath, profile, "Workspaces");

        // ---- v1: WorkspacesCache JSON file --------------------------------
        string cacheFile = Path.Combine(workspacesDir, "WorkspacesCache");
        if (File.Exists(cacheFile))
        {
            var v1 = TryLoadV1(cacheFile, profile, executablePath);
            if (v1.Count > 0) return v1;
        }

        // ---- v2: LevelDB directory ----------------------------------------
        string levelDbDir = Path.Combine(workspacesDir, "LevelDB");
        if (Directory.Exists(levelDbDir))
        {
            var v2 = TryLoadV2(levelDbDir, profile, executablePath);
            if (v2.Count > 0) return v2;
        }

        return new List<EdgeWorkspace>();
    }

    // ---- v1 loader ---------------------------------------------------------

    private static List<EdgeWorkspace> TryLoadV1(string cacheFilePath, string profile, string exe)
    {
        try
        {
            string json = File.ReadAllText(cacheFilePath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("workspaces", out JsonElement arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return new List<EdgeWorkspace>();

            var list = new List<EdgeWorkspace>();
            foreach (JsonElement elem in arr.EnumerateArray())
            {
                var ws = ParseWorkspaceElement(elem, profile, exe);
                if (ws is not null) list.Add(ws);
            }
            return list;
        }
        catch
        {
            return new List<EdgeWorkspace>();
        }
    }

    // ---- v2 loader ---------------------------------------------------------

    private static List<EdgeWorkspace> TryLoadV2(string levelDbDir, string profile, string exe)
    {
        var list = new List<EdgeWorkspace>();
        try
        {
            var entries = LevelDbReader.ReadAllAsStrings(levelDbDir);
            foreach (var (_, value) in entries)
            {
                // Values may be individual workspace JSON objects
                // or an array / cache blob – try both.
                if (TryParseWorkspaceJson(value, out EdgeWorkspace single))
                {
                    single.Profile = profile;
                    single.EdgeExecutablePath = exe;
                    list.Add(single);
                    continue;
                }

                // Try as array of workspaces
                if (TryParseWorkspaceArray(value, profile, exe, out var many))
                {
                    list.AddRange(many);
                    continue;
                }

                // Try as WorkspacesCache-like object { "workspaces": [...] }
                if (TryParseWorkspacesCache(value, profile, exe, out var cached))
                {
                    list.AddRange(cached);
                }
            }
        }
        catch
        {
            // Best-effort – return whatever was collected
        }
        return list;
    }

    // -----------------------------------------------------------------------
    // JSON helpers

    private static bool TryParseWorkspaceJson(string json, out EdgeWorkspace workspace)
    {
        workspace = null;
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{')) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            workspace = ParseWorkspaceElement(doc.RootElement, null, null);
            return workspace is not null;
        }
        catch { return false; }
    }

    private static bool TryParseWorkspaceArray(string json, string profile, string exe,
                                                out List<EdgeWorkspace> workspaces)
    {
        workspaces = null;
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('[')) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            workspaces = new List<EdgeWorkspace>();
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var ws = ParseWorkspaceElement(elem, profile, exe);
                if (ws is not null) workspaces.Add(ws);
            }
            return workspaces.Count > 0;
        }
        catch { return false; }
    }

    private static bool TryParseWorkspacesCache(string json, string profile, string exe,
                                                 out List<EdgeWorkspace> workspaces)
    {
        workspaces = null;
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{')) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("workspaces", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return false;
            workspaces = new List<EdgeWorkspace>();
            foreach (var elem in arr.EnumerateArray())
            {
                var ws = ParseWorkspaceElement(elem, profile, exe);
                if (ws is not null) workspaces.Add(ws);
            }
            return workspaces.Count > 0;
        }
        catch { return false; }
    }

    private static EdgeWorkspace ParseWorkspaceElement(JsonElement elem, string profile, string exe)
    {
        if (elem.ValueKind != JsonValueKind.Object) return null;
        if (!elem.TryGetProperty("id", out var idProp)) return null;
        if (!elem.TryGetProperty("name", out var nameProp)) return null;

        string id = idProp.GetString();
        string name = nameProp.GetString();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) return null;

        int count = 0;
        if (elem.TryGetProperty("count", out var countProp))
            countProp.TryGetInt32(out count);

        return new EdgeWorkspace
        {
            Id = id,
            Name = name,
            Count = count,
            Profile = profile,
            EdgeExecutablePath = exe
        };
    }
}
