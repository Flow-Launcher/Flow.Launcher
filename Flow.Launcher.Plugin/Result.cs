using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace Flow.Launcher.Plugin
{

    public class Result
    {

        private string _pluginDirectory;
        
        private string _icoPath;

        /// <summary>
        /// Provides the title of the result. This is always required.
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

        public string IcoPath
        {
            get { return _icoPath; }
            set
            {
                if (!string.IsNullOrEmpty(PluginDirectory) && !Path.IsPathRooted(value))
                {
                    _icoPath = Path.Combine(value, IcoPath);
                }
                else
                {
                    _icoPath = value;
                }
            }
        }

        public delegate ImageSource IconDelegate();

        /// <summary>
        /// Delegate to Get Image Source
        /// </summary>
        public IconDelegate Icon { get; set; }

        /// <summary>
        /// Information for Glyph Icon
        /// </summary>
        public GlyphInfo Glyph { get; init; } 


        /// <summary>
        /// return true to hide flowlauncher after select result
        /// </summary>
        public Func<ActionContext, bool> Action { get; set; }

        public int Score { get; set; }

        /// <summary>
        /// A list of indexes for the characters to be highlighted in Title
        /// </summary>
        public IList<int> TitleHighlightData { get; set; }

        /// <summary>
        /// A list of indexes for the characters to be highlighted in SubTitle
        /// </summary>
        public IList<int> SubTitleHighlightData { get; set; }

        /// <summary>
        /// Only results that originQuery match with current query will be displayed in the panel
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
                if (!string.IsNullOrEmpty(IcoPath) && !Path.IsPathRooted(IcoPath))
                {
                    IcoPath = Path.Combine(value, IcoPath);
                }
            }
        }

        public override bool Equals(object obj)
        {
            var r = obj as Result;

            var equality = string.Equals(r?.Title, Title) &&
                           string.Equals(r?.SubTitle, SubTitle) &&
                           string.Equals(r?.IcoPath, IcoPath) &&
                           TitleHighlightData == r.TitleHighlightData &&
                           SubTitleHighlightData == r.SubTitleHighlightData;

            return equality;
        }

        public override int GetHashCode()
        {
            var hashcode = (Title?.GetHashCode() ?? 0) ^
                           (SubTitle?.GetHashCode() ?? 0);
            return hashcode;
        }

        public override string ToString()
        {
            return Title + SubTitle;
        }

        public Result() { }

        /// <summary>
        /// Additional data associate with this result
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
    }
}