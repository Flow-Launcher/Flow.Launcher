using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Program
{
    public class Settings
    {
        public DateTime LastIndexTime { get; set; }
        public List<ProgramSource> ProgramSources { get; set; } = new List<ProgramSource>();
        public List<DisabledProgramSource> DisabledProgramSources { get; set; } = new List<DisabledProgramSource>();
        public string[] CustomSuffixes { get; set; } = Array.Empty<string>();
        public string[] CustomProtocols { get; set; } = Array.Empty<string>();

        [JsonIgnore]
        public Dictionary<string, bool> BuiltinSuffixesStatus { get; set; } = new Dictionary<string, bool>{
            { "exe", true }, { "appref-ms", true }, { "lnk", true }
        };

        [JsonIgnore]
        public Dictionary<string, bool> BuiltinProtocolsStatus { get; set; } = new Dictionary<string, bool>{
            { $"steam://run/{SuffixSeperator}steam://rungameid/", true }, { "com.epicgames.launcher://apps/", true }, { $"http://{SuffixSeperator}https://", false}
        };

        public bool UseCustomSuffixes = false;
        public bool UseCustomProtocols = false;

        public string[] GetSuffixes()
        {
            List<string> extensions = new List<string>();
            foreach(var item in BuiltinSuffixesStatus)
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
                    var tmp = item.Key.Split(SuffixSeperator, StringSplitOptions.RemoveEmptyEntries);
                    foreach(var p in tmp)
                    {
                        protocols.Add(p);
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

        public bool EnableStartMenuSource { get; set; } = true;
        public bool EnableDescription { get; set; } = false;
        public bool HideAppsPath { get; set; } = true;
        public bool EnableRegistrySource { get; set; } = true;
        public string CustomizedExplorer { get; set; } = Explorer;
        public string CustomizedArgs { get; set; } = ExplorerArgs;

        internal const char SuffixSeperator = ';';

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
