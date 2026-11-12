using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    public partial class Result
    {
        /// <summary>
        /// Delegate function that produces an image for the host UI toolkit (WPF types are unavailable on this platform).
        /// </summary>
        /// <returns></returns>
        public delegate object IconDelegate();

        /// <summary>
        /// Customized Preview Panel; a control of the host UI toolkit (WPF types are unavailable on this platform).
        /// </summary>
        [JsonIgnore]
        public Lazy<object> PreviewPanel { get; set; }
    }
}
