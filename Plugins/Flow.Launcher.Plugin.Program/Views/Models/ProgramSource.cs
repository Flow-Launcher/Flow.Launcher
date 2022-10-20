using System;
using System.IO;
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

        public string Location;
        public string Name { get => name ?? new DirectoryInfo(Location).Name; set => name = value; }
        public bool Enabled { get; set; } = true;
        private string uid { get; set; }

        /// <summary>
        /// Guranteed lowercase.
        /// </summary>
        public string UniqueIdentifier { get => uid; set => uid = value.ToLowerInvariant(); }

        public ProgramSource()
        {
        }  // TODO Remove

        /// <summary>
        /// Custom user added source.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="enabled"></param>
        public ProgramSource(string location, bool enabled)
        {
            Location = location;
            Enabled = enabled;
            UniqueIdentifier = location;
        }

        public ProgramSource(ProgramSource source)
        {
            Location = source.Location;
            Name = source.Name;
            Enabled = source.Enabled;
            UniqueIdentifier = source.UniqueIdentifier;
        }

        public ProgramSource(IProgram source)
        {
            Location = source.Location;
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

        public static bool operator ==(ProgramSource a, ProgramSource b)
        {
            return a is not null && a.Equals(b);
        }

        public static bool operator !=(ProgramSource a, ProgramSource b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(uid);
        }
    }

    public class DisabledProgramSource : ProgramSource
    {
        public DisabledProgramSource() { }

        public DisabledProgramSource(string location) : base(location, false) { }

        public DisabledProgramSource(ProgramSource source) : base(source)
        {
            Enabled = false;
        }

        public DisabledProgramSource(IProgram program) : base(program)
        {
            Enabled = false;
        }
    }
}
