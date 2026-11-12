using System;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flow.Launcher.Plugin
{
    public partial class Result
    {
        /// <summary>
        /// Delegate function that produces an <see cref="ImageSource"/>
        /// </summary>
        /// <returns></returns>
        public delegate ImageSource IconDelegate();

        /// <summary>
        /// Customized Preview Panel
        /// </summary>
        [JsonIgnore]
        public Lazy<UserControl> PreviewPanel { get; set; }
    }
}
