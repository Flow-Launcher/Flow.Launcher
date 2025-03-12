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
        private string name = string.Empty;
        private string loc = string.Empty;
        private string uniqueIdentifier = string.Empty;

        public string Location
        {
            get => loc;
            set
            {
                loc = value ?? string.Empty;
                UniqueIdentifier = value;
            }
        }

        public string Name { get => name; set => name = value ?? string.Empty; }
        public bool Enabled { get; set; } = true;

        public string UniqueIdentifier
        {
            get => uniqueIdentifier;
            private set
            {
                uniqueIdentifier = value == null ? string.Empty : value.ToLowerInvariant();
            }
        }

        [JsonConstructor]
        public ProgramSource(string name, string location, bool enabled, string uniqueIdentifier)
        {
            loc = location ?? string.Empty;
            Name = name;
            Enabled = enabled;
            UniqueIdentifier = uniqueIdentifier;
        }

        /// <summary>
        /// Add source by location
        /// </summary>
        /// <param name="location">location of program source</param>
        /// <param name="enabled">enabled</param>
        public ProgramSource(string location, bool enabled = true)
        {
            loc = location ?? string.Empty;
            Enabled = enabled;
            UniqueIdentifier = location;  // For path comparison
            Name = new DirectoryInfo(Location).Name;
        }

        public ProgramSource(IProgram source)
        {
            loc = source.Location ?? string.Empty;
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
            return uniqueIdentifier.GetHashCode();
        }
    }
}
