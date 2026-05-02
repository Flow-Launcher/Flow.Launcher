using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Flow.Launcher.Plugin.BrowserWorkspace.LevelDb;

/// <summary>
/// High-level reader that assembles the current state from a LevelDB directory
/// by merging .ldb (SSTable) files and the .log (WAL) file, giving precedence
/// to entries with higher sequence numbers (i.e., newer writes).
/// </summary>
internal static class LevelDbReader
{
    /// <summary>
    /// Returns all live (non-deleted) key-value pairs in the database as
    /// UTF-8 decoded strings.  If a key/value is not valid UTF-8 it is skipped.
    /// </summary>
    public static Dictionary<string, string> ReadAllAsStrings(string dbDirectory)
    {
        // key → (value, seqNum)
        var raw = new Dictionary<string, (byte[] Value, ulong SeqNum)>(StringComparer.Ordinal);

        // Read .log file first – it contains the most recent (unflushed) writes.
        foreach (var logFile in Directory.GetFiles(dbDirectory, "*.log"))
        {
            foreach (var (userKey, value, seqNum) in LogFileReader.ReadEntries(logFile))
            {
                string k = TryDecodeUtf8(userKey);
                if (k is null) continue;
                if (!raw.TryGetValue(k, out var existing) || seqNum > existing.SeqNum)
                    raw[k] = (value, seqNum);
            }
        }

        // Read .ldb (SSTable) files, newest first (higher file number = newer).
        var ldbFiles = Directory.GetFiles(dbDirectory, "*.ldb");
        Array.Sort(ldbFiles, static (a, b) =>
        {
            int na = ParseFileNumber(a);
            int nb = ParseFileNumber(b);
            return nb.CompareTo(na); // descending
        });

        foreach (var ldbFile in ldbFiles)
        {
            foreach (var (userKey, value, seqNum) in SsTableReader.ReadEntries(ldbFile))
            {
                string k = TryDecodeUtf8(userKey);
                if (k is null) continue;
                if (!raw.TryGetValue(k, out var existing) || seqNum > existing.SeqNum)
                    raw[k] = (value, seqNum);
            }
        }

        // Convert values to strings, skipping anything that is not valid UTF-8.
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, (valueBytes, _)) in raw)
        {
            string v = TryDecodeUtf8(valueBytes);
            if (v is not null)
                result[key] = v;
        }
        return result;
    }

    private static string TryDecodeUtf8(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try { return Encoding.UTF8.GetString(bytes); }
        catch { return null; }
    }

    private static int ParseFileNumber(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(stem, out int n) ? n : 0;
    }
}
