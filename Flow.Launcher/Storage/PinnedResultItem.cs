using System;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class PinnedResultItem : Result
    {
        public DateTime LastPinnedAt { get; set; }
        public bool IsQuery { get; set; }
        public string Query { get; set;  }
        public PinnedResultItem() { }

        public PinnedResultItem(Result result, string query)
        {
            Title = result.Title;
            SubTitle = result.SubTitle;
            PluginID = result.PluginID;
            OriginQuery = result.OriginQuery;
            RecordKey = result.RecordKey;
            IcoPath = result.IcoPath;
            PluginDirectory = result.PluginDirectory;
            Glyph = result.Glyph;
            ShowBadge = result.ShowBadge;
            BadgeIcoPath = result.BadgeIcoPath;
            RoundedIcon = result.RoundedIcon;
            Score = result.Score;
            TitleHighlightData = result.TitleHighlightData;
            CopyText = result.CopyText;
            AutoCompleteText = result.AutoCompleteText;
            LastPinnedAt = DateTime.Now;
            Query = query ?? string.Empty;
            IsQuery = !string.IsNullOrEmpty(query);
            AsyncAction = null;
        }

        public PinnedResultItem DeepCopy()
        {

            var queryValue = Query;
            var glyphValue = Glyph;

            var title = string.Empty;
            var subtitle = string.Empty;
            var icoPath = string.Empty;
            var glyph = null as GlyphInfo;

            if (IsQuery)
            {
                title = Localize.executeQuery(Query);
                subtitle = Localize.lastPinnedAt(LastPinnedAt);
                icoPath = Constant.HistoryIcon;
                glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE81C");
            } else
            {
                title = Title;
                subtitle = SubTitle;
                icoPath = IcoPath;
                glyph = glyphValue != null
                            ? new GlyphInfo(glyphValue.FontFamily, glyphValue.Glyph)
                            : null;
            }
            return new PinnedResultItem()
            {
                Title = title,
                SubTitle = subtitle,
                PluginID = PluginID,
                Query = Query,
                OriginQuery = new Query { TrimmedQuery = Query },
                RecordKey = RecordKey,
                IcoPath = icoPath,
                ShowBadge = ShowBadge,
                BadgeIcoPath = BadgeIcoPath,
                PluginDirectory = PluginDirectory,
                RoundedIcon = RoundedIcon,
                Score = Score,
                TitleHighlightData = TitleHighlightData,
                CopyText = CopyText,
                AutoCompleteText = AutoCompleteText,
                Action = _ =>
                {
                    App.API.BackToQueryResults();
                    App.API.ChangeQuery(queryValue);
                    return false;
                },
                IsQuery = IsQuery,
                AsyncAction = null,
                Glyph = glyph,
                LastPinnedAt = LastPinnedAt
            };
        }

        public bool Equals(Result r)
        {
            if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
            {
                return Title == r.Title
                    && SubTitle == r.SubTitle
                    && PluginID == r.PluginID;
                    //&& Query == r.OriginQuery.TrimmedQuery;
            }
            else
            {
                return RecordKey == r.RecordKey
                    && PluginID == r.PluginID;
                    //&& Query == r.OriginQuery.TrimmedQuery;
            }
        }

        public bool Equals(Result r, string query)
        {
            if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
            {
                return Title == r.Title
                    && SubTitle == r.SubTitle
                    && PluginID == r.PluginID
                    && Query == query;
            }
            else
            {
                return RecordKey == r.RecordKey
                    && PluginID == r.PluginID
                    && Query == query;
            }
        }
    }
}
