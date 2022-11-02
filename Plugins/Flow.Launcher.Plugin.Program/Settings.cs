using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using PropertyChanged;
using Windows.Foundation.Metadata;

namespace Flow.Launcher.Plugin.Program
{
    public class Settings
    {
        public DateTime LastIndexTime { get; set; }
        public List<ProgramSource> ProgramSources { get; set; } = new List<ProgramSource>();
        public List<DisabledProgramSource> DisabledProgramSources { get; set; } = new List<DisabledProgramSource>();

        [Obsolete("Should use GetSuffixes() instead."), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] ProgramSuffixes { get; set; } = null;
        public string[] CustomSuffixes { get; set; } = Array.Empty<string>();  // Custom suffixes only
        public string[] CustomProtocols { get; set; } = Array.Empty<string>();

        public Dictionary<string, bool> BuiltinSuffixesStatus { get; set; } = new Dictionary<string, bool>{
            { "exe", true }, { "appref-ms", true }, { "lnk", true }
        };

        public Dictionary<string, bool> BuiltinProtocolsStatus { get; set; } = new Dictionary<string, bool>{
            { "steam", true }, { "epic", true }, { "http", false }
        };

        [JsonIgnore]
        public Dictionary<string, string> BuiltinProtocols { get; set; } = new Dictionary<string, string>{
            { "steam", $"steam://run/{SuffixSeparator}steam://rungameid/" }, { "epic", "com.epicgames.launcher://apps/" }, { "http",  $"http://{SuffixSeparator}https://"}
        };

        public bool UseCustomSuffixes { get; set; } = false;
        public bool UseCustomProtocols { get; set; } = false;

        public string[] GetSuffixes()
        {
            RemoveRedundantSuffixes();
            List<string> extensions = new List<string>();
            foreach (var item in BuiltinSuffixesStatus)
            {
                if (item.Value)
                {
                    extensions.Add(item.Key);
                }
            }

            if (BuiltinProtocolsStatus.Values.Any(x => x == true) || UseCustomProtocols)
            {
                extensions.Add("url");
            }

            if (UseCustomSuffixes)
            {
                return extensions.Concat(CustomSuffixes).DistinctBy(x => x.ToLower()).ToArray();
            }
            else
            {
                return extensions.DistinctBy(x => x.ToLower()).ToArray();
            }
        }

        public string[] GetProtocols()
        {
            List<string> protocols = new List<string>();
            foreach (var item in BuiltinProtocolsStatus)
            {
                if (item.Value)
                {
                    if (BuiltinProtocols.TryGetValue(item.Key, out string ps))
                    {
                        var tmp = ps.Split(SuffixSeparator, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var protocol in tmp)
                        {
                            protocols.Add(protocol);
                        }
                    }
                }
            }

            if (UseCustomProtocols)
            {
                return protocols.Concat(CustomProtocols).DistinctBy(x => x.ToLower()).ToArray();
            }
            else
            {
                return protocols.DistinctBy(x => x.ToLower()).ToArray();
            }
        }

        private void RemoveRedundantSuffixes()
        {
            // Migrate to new settings
            // CustomSuffixes no longer contains custom suffixes
            // users has tweaked the settings
            // or this function has been executed once
            if (UseCustomSuffixes == true || ProgramSuffixes == null) 
                return;
            var suffixes = ProgramSuffixes.ToList();
            foreach(var item in BuiltinSuffixesStatus)
            {
                suffixes.Remove(item.Key);
            }
            CustomSuffixes = suffixes.ToArray(); // Custom suffixes
            UseCustomSuffixes = CustomSuffixes.Length != 0; // Search custom suffixes or not
            ProgramSuffixes = null;
        }

        public bool EnableStartMenuSource { get; set; } = true;
        public bool EnableDescription { get; set; } = false;
        public bool HideAppsPath { get; set; } = true;
        public bool EnableRegistrySource { get; set; } = true;
        public bool EnablePATHSource { get; set; } = true;

        public string CustomizedExplorer { get; set; } = Explorer;
        public string CustomizedArgs { get; set; } = ExplorerArgs;

        internal const char SuffixSeparator = ';';

        internal const string Explorer = "explorer";

        internal const string ExplorerArgs = "%s";

        /// <summary>
        /// Contains user added folder location contents as well as all user disabled applications
        /// </summary>
        /// <remarks>
        /// <para>Win32 class applications set UniqueIdentifier using their full file path</para>
        /// <para>UWP class applications set UniqueIdentifier using their Application User Model ID</para>
        /// <para>Custom user added program sources set UniqueIdentifier using their location</para>
        /// </remarks>
        public class ProgramSource
        {
            private string name;

            public string Location { get; set; }
            public string Name { get => name ?? new DirectoryInfo(Location).Name; set => name = value; }
            public bool Enabled { get; set; } = true;
            public string UniqueIdentifier { get; set; }
        }

        public class DisabledProgramSource : ProgramSource { }
    }
}
