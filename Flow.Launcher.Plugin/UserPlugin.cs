using System;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// User Plugin Model for Flow Launcher
    /// </summary>
    public record UserPlugin
    {
        /// <summary>
        /// Unique identifier of the plugin
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Name of the plugin
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the plugin
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Author of the plugin
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Version of the plugin
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Allow language of the plugin <see cref="AllowedLanguage"/>
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Website of the plugin
        /// </summary>
        public string Website { get; set; }

        /// <summary>
        /// URL to download the plugin
        /// </summary>
        public string UrlDownload { get; set; }

        /// <summary>
        /// URL to the source code of the plugin
        /// </summary>
        public string UrlSourceCode { get; set; }

        /// <summary>
        /// Local path where the plugin is installed
        /// </summary>
        public string LocalInstallPath { get; set; }

        /// <summary>
        /// Icon path of the plugin
        /// </summary>
        public string IcoPath { get; set; }

        /// <summary>
        /// The date when the plugin was last updated
        /// </summary>
        public DateTime? LatestReleaseDate { get; set; }

        /// <summary>
        /// The date when the plugin was added to the local system
        /// </summary>
        public DateTime? DateAdded { get; set; }

        /// <summary>
        /// Indicates whether the plugin is installed from a local path
        /// </summary>
        public bool IsFromLocalInstallPath => !string.IsNullOrEmpty(LocalInstallPath);
    }
}
