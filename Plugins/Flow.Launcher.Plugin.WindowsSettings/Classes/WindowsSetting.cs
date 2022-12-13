using System.Collections.Generic;

namespace Flow.Launcher.Plugin.WindowsSettings.Classes
{
    /// <summary>
    /// A windows setting
    /// </summary>
    internal record WindowsSetting
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSetting"/> class.
        /// </summary>
        public WindowsSetting()
        {
            Name = string.Empty;
            Area = string.Empty;
            Command = string.Empty;
            Type = string.Empty;
        }

        /// <summary>
        /// Gets or sets the name of this setting.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the area of this setting.
        /// </summary>
        public string Area { get; set; }

        /// <summary>
        /// Gets or sets the command of this setting.
        /// </summary>
        public string Command { get; set; }

        private string type = string.Empty;

        /// <summary>
        /// Gets or sets the type of the windows setting.
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// Gets or sets the type display name of this setting.
        /// </summary>
        public string? DisplayType { get; set; }

        /// <summary>
        /// Gets or sets the alternative names of this setting.
        /// </summary>
        public IEnumerable<string>? AltNames { get; set; }

        /// <summary>
        /// Gets or sets the Keywords names of this task link.
        /// </summary>
        public IEnumerable<IEnumerable<string>>? Keywords { get; set; }

        /// <summary>
        /// Gets or sets the Glyph of this setting
        /// </summary>
        public GlyphInfo? IconGlyph { get; set; }

        /// <summary>
        /// Gets or sets a additional note of this settings.
        /// <para>(e.g. why is not supported on your system)</para>
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Gets or sets the minimum need Windows build for this setting.
        /// </summary>
        public uint? IntroducedInBuild { get; set; }

        /// <summary>
        /// Gets or sets the Windows build since this settings is not longer present.
        /// </summary>
        public uint? DeprecatedInBuild { get; set; }
    }
}