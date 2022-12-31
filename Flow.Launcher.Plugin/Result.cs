using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Describes the result of a plugin
    /// </summary>
    public class Result
    {

        private string _pluginDirectory;

        private string _icoPath;

        /// <summary>
        /// The title of the result. This is always required.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Provides additional details for the result. This is optional
        /// </summary>
        public string SubTitle { get; set; } = string.Empty;

        /// <summary>
        /// This holds the action keyword that triggered the result. 
        /// If result is triggered by global keyword: *, this should be empty.
        /// </summary>
        public string ActionKeywordAssigned { get; set; }

        /// <summary>
        /// This holds the text which can be provided by plugin to be copied to the 
        /// user's clipboard when Ctrl + C is pressed on a result. If the text is a file/directory path
        /// flow will copy the actual file/folder instead of just the path text.
        /// </summary>
        public string CopyText
        {
            get => string.IsNullOrEmpty(_copyText) ? SubTitle : _copyText;
            set => _copyText = value;
        }

        /// <summary>
        /// This holds the text which can be provided by plugin to help Flow autocomplete text
        /// for user on the plugin result. If autocomplete action for example is tab, pressing tab will have 
        /// the default constructed autocomplete text (result's Title), or the text provided here if not empty.
        /// </summary>
        public string AutoCompleteText { get; set; }

        /// <summary>
        /// Image Displayed on the result
        /// <value>Relative Path to the Image File</value>
        /// <remarks>GlyphInfo is prioritized if not null</remarks>
        /// </summary>
        public string IcoPath
        {
            get { return _icoPath; }
            set
            {
                // As a standard this property will handle prepping and converting to absolute local path for icon image processing
                if (!string.IsNullOrEmpty(value)
                    && !string.IsNullOrEmpty(PluginDirectory)
                    && !Path.IsPathRooted(value)
                    && !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _icoPath = Path.Combine(PluginDirectory, value);
                }
                else
                {
                    _icoPath = value;
                }
            }
        }
        /// <summary>
        /// Determines if Icon has a border radius
        /// </summary>
        public bool RoundedIcon { get; set; } = false;

        /// <summary>
        /// Delegate function, see <see cref="Icon"/>
        /// </summary>
        /// <returns></returns>
        public delegate ImageSource IconDelegate();

        /// <summary>
        /// Delegate to Get Image Source
        /// </summary>
        public IconDelegate Icon;
        private string _copyText = string.Empty;

        /// <summary>
        /// Information for Glyph Icon (Prioritized than IcoPath/Icon if user enable Glyph Icons)
        /// </summary>
        public GlyphInfo Glyph { get; init; }


        /// <summary>
        /// Delegate. An action to take in the form of a function call when the result has been selected
        /// <returns>
        /// true to hide flowlauncher after select result
        /// </returns>
        /// </summary>
        public Func<ActionContext, bool> Action { get; set; }

        /// <summary>
        /// Delegate. An Async action to take in the form of a function call when the result has been selected
        /// <returns>
        /// true to hide flowlauncher after select result
        /// </returns>
        /// </summary>
        public Func<ActionContext, ValueTask<bool>> AsyncAction { get; set; }

        /// <summary>
        /// Priority of the current result
        /// <value>default: 0</value>
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// A list of indexes for the characters to be highlighted in Title
        /// </summary>
        public IList<int> TitleHighlightData { get; set; }

        /// <summary>
        /// Deprecated as of Flow Launcher v1.9.1. Subtitle highlighting is no longer offered
        /// </summary>
        [Obsolete("Deprecated as of Flow Launcher v1.9.1. Subtitle highlighting is no longer offered")]
        public IList<int> SubTitleHighlightData { get; set; }

        /// <summary>
        /// Query information associated with the result
        /// </summary>
        internal Query OriginQuery { get; set; }

        /// <summary>
        /// Plugin directory
        /// </summary>
        public string PluginDirectory
        {
            get { return _pluginDirectory; }
            set
            {
                _pluginDirectory = value;

                // When the Result object is returned from the query call, PluginDirectory is not provided until
                // UpdatePluginMetadata call is made at PluginManager.cs L196. Once the PluginDirectory becomes available
                // we need to update (only if not Uri path) the IcoPath with the full absolute path so the image can be loaded.
                IcoPath = _icoPath;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var r = obj as Result;

            var equality = string.Equals(r?.Title, Title) &&
                           string.Equals(r?.SubTitle, SubTitle) &&
                           string.Equals(r?.IcoPath, IcoPath) &&
                           TitleHighlightData == r.TitleHighlightData;

            return equality;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashcode = (Title?.GetHashCode() ?? 0) ^
                           (SubTitle?.GetHashCode() ?? 0);
            return hashcode;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Title + SubTitle;
        }

        /// <summary>
        /// Additional data associated with this result
        /// <example>
        /// As external information for ContextMenu
        /// </example>
        /// </summary>
        public object ContextData { get; set; }

        /// <summary>
        /// Plugin ID that generated this result
        /// </summary>
        public string PluginID { get; internal set; }

        /// <summary>
        /// Show message as ToolTip on result Title hover over
        /// </summary>
        public string TitleToolTip { get; set; }

        /// <summary>
        /// Show message as ToolTip on result SubTitle hover over
        /// </summary>
        public string SubTitleToolTip { get; set; }

        /// <summary>
        /// Customized Preview Panel
        /// </summary>
        public Lazy<UserControl> PreviewPanel { get; set; }

        /// <summary>
        /// Run this result, asynchronously
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public ValueTask<bool> ExecuteAsync(ActionContext context)
        {
            return AsyncAction?.Invoke(context) ?? ValueTask.FromResult(Action?.Invoke(context) ?? false);
        }

        /// <summary>
        /// Progress bar display. Providing an int value between 0-100 will trigger the progress bar to be displayed on the result
        /// </summary>
        public int? ProgressBar { get; set; }

        /// <summary>
        /// Optionally set the color of the progress bar
        /// </summary>
        /// <default>#26a0da (blue)</default>
        public string ProgressBarColor { get; set; } = "#26a0da";

        public PreviewInfo Preview { get; set; } = PreviewInfo.Default;

        /// <summary>
        /// Info of the preview image.
        /// </summary>
        public record PreviewInfo
        {
            /// <summary>
            /// Full image used for preview panel
            /// </summary>
            public string PreviewImagePath { get; set; }
            /// <summary>
            /// Determines if the preview image should occupy the full width of the preview panel.
            /// </summary>
            public bool IsMedia { get; set; }
            public string Description { get; set; }
            public IconDelegate PreviewDelegate { get; set; }

            public static PreviewInfo Default { get; } = new()
            {
                PreviewImagePath = null,
                Description = null,
                IsMedia = false,
                PreviewDelegate = null,
            };
        }
    }
}
