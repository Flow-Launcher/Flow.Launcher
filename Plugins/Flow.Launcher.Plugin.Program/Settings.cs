using System;
using System.Collections.Generic;
using System.IO;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program
{
    public class Settings
    {
        public DateTime LastIndexTime { get; set; }
        public List<ProgramSource> ProgramSources { get; set; } = new List<ProgramSource>();
        public List<DisabledProgramSource> DisabledProgramSources { get; set; } = new List<DisabledProgramSource>();  // For disabled single programs
        public string[] ProgramSuffixes { get; set; } = {"appref-ms", "exe", "lnk"};

        public bool EnableStartMenuSource { get; set; } = true;

        public bool EnableDescription { get; set; } = false;
        public bool HideAppsPath { get; set; } = true;
        public bool EnableRegistrySource { get; set; } = true;
        public string CustomizedExplorer { get; set; } = Explorer;
        public string CustomizedArgs { get; set; } = ExplorerArgs;

        internal const char SuffixSeperator = ';';

        internal const string Explorer = "explorer";

        internal const string ExplorerArgs = "%s";
    }
}
