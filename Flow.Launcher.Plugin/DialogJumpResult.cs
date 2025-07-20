namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Describes a result of a <see cref="Query"/> executed by a plugin in dialog jump window
    /// </summary>
    public class DialogJumpResult : Result
    {
        /// <summary>
        /// This holds the path which can be provided by plugin to be navigated to the
        /// file dialog when records in dialog jump window is right clicked on a result.
        /// </summary>
        public required string DialogJumpPath { get; init; }

        /// <summary>
        /// Clones the current dialog jump result
        /// </summary>
        public new DialogJumpResult Clone()
        {
            return new DialogJumpResult
            {
                Title = Title,
                SubTitle = SubTitle,
                ActionKeywordAssigned = ActionKeywordAssigned,
                CopyText = CopyText,
                AutoCompleteText = AutoCompleteText,
                IcoPath = IcoPath,
                BadgeIcoPath = BadgeIcoPath,
                RoundedIcon = RoundedIcon,
                Icon = Icon,
                BadgeIcon = BadgeIcon,
                Glyph = Glyph,
                Action = Action,
                AsyncAction = AsyncAction,
                Score = Score,
                TitleHighlightData = TitleHighlightData,
                OriginQuery = OriginQuery,
                PluginDirectory = PluginDirectory,
                ContextData = ContextData,
                PluginID = PluginID,
                TitleToolTip = TitleToolTip,
                SubTitleToolTip = SubTitleToolTip,
                PreviewPanel = PreviewPanel,
                ProgressBar = ProgressBar,
                ProgressBarColor = ProgressBarColor,
                Preview = Preview,
                AddSelectedCount = AddSelectedCount,
                RecordKey = RecordKey,
                ShowBadge = ShowBadge,
                DialogJumpPath = DialogJumpPath
            };
        }

        /// <summary>
        /// Convert <see cref="Result"/> to <see cref="DialogJumpResult"/>.
        /// </summary>
        public static DialogJumpResult From(Result result, string dialogJumpPath)
        {
            return new DialogJumpResult
            {
                Title = result.Title,
                SubTitle = result.SubTitle,
                ActionKeywordAssigned = result.ActionKeywordAssigned,
                CopyText = result.CopyText,
                AutoCompleteText = result.AutoCompleteText,
                IcoPath = result.IcoPath,
                BadgeIcoPath = result.BadgeIcoPath,
                RoundedIcon = result.RoundedIcon,
                Icon = result.Icon,
                BadgeIcon = result.BadgeIcon,
                Glyph = result.Glyph,
                Action = result.Action,
                AsyncAction = result.AsyncAction,
                Score = result.Score,
                TitleHighlightData = result.TitleHighlightData,
                OriginQuery = result.OriginQuery,
                PluginDirectory = result.PluginDirectory,
                ContextData = result.ContextData,
                PluginID = result.PluginID,
                TitleToolTip = result.TitleToolTip,
                SubTitleToolTip = result.SubTitleToolTip,
                PreviewPanel = result.PreviewPanel,
                ProgressBar = result.ProgressBar,
                ProgressBarColor = result.ProgressBarColor,
                Preview = result.Preview,
                AddSelectedCount = result.AddSelectedCount,
                RecordKey = result.RecordKey,
                ShowBadge = result.ShowBadge,
                DialogJumpPath = dialogJumpPath
            };
        }
    }
}
