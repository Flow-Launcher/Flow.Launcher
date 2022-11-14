using System;
using System.IO;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.Program.Programs;

namespace Flow.Launcher.Plugin.Program.Views.Models
{
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

        private string loc;
        public string Location
        {
            get => loc;
            set
            {
                loc = value;
                UniqueIdentifier = value.ToLowerInvariant();
            }
        }
        public string Name { get => name ?? new DirectoryInfo(Location).Name; set => name = value; }
        public bool Enabled { get; set; } = true;

        public string UniqueIdentifier { get; private set; }

        [JsonConstructor]
        public ProgramSource(string name, string location, bool enabled, string uniqueIdentifier)
        {
            loc = location;
            this.name = name;
            Enabled = enabled;
            if (location.Equals(uniqueIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                UniqueIdentifier = location.ToLowerInvariant();  // To make sure old config can be reset to case-insensitive
            }
            else
            {
                UniqueIdentifier = uniqueIdentifier;  // For uwp apps
            }
        }

        /// <summary>
        /// Add source by location
        /// </summary>
        /// <param name="location">location of program source</param>
        /// <param name="enabled">enabled</param>
        public ProgramSource(string location, bool enabled = true)
        {
            loc = location;
            Enabled = enabled;
            UniqueIdentifier = location.ToLowerInvariant();  // For path comparison
        }

        public ProgramSource(IProgram source)
        {
            loc = source.Location;
            Name = source.Name;
            Enabled = source.Enabled;
            UniqueIdentifier = source.UniqueIdentifier;
        }

        public override bool Equals(object obj)
        {
            return obj is ProgramSource other && other.UniqueIdentifier == this.UniqueIdentifier;
        }

        public bool Equals(IProgram program)
        {
            return program != null && program.UniqueIdentifier == this.UniqueIdentifier;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UniqueIdentifier);
        }
    }
}
