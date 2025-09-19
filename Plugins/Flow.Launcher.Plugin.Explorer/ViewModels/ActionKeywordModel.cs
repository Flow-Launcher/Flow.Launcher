#nullable enable

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public partial class ActionKeywordModel : BaseModel
    {
        private static Settings _settings = null!;

        public static void Init(Settings settings)
        {
            _settings = settings;
        }

        internal ActionKeywordModel(Settings.ActionKeyword actionKeyword, string description)
        {
            KeywordProperty = actionKeyword;
            Description = description;
        }

        public string Description { get; private init; }

        public string LocalizedDescription => Main.Context.API.GetTranslation(Description);

        internal Settings.ActionKeyword KeywordProperty { get; }

        private string? keyword;
        public string Keyword
        {
            get => keyword ??= _settings.GetActionKeyword(KeywordProperty);
            set
            {
                keyword = value;
                _settings.SetActionKeyword(KeywordProperty, value);
                OnPropertyChanged();
            }
        }

        private bool? enabled;
        public bool Enabled
        {
            get => enabled ??= _settings.GetActionKeywordEnabled(KeywordProperty);
            set
            {
                enabled = value;
                _settings.SetActionKeywordEnabled(KeywordProperty, value);
                OnPropertyChanged();
            }
        }
    }
}
