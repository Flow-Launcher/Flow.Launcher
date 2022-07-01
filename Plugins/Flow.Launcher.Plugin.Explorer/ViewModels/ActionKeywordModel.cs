using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    public class ActionKeywordModel : INotifyPropertyChanged
    {
        private static Settings _settings;

        public event PropertyChangedEventHandler PropertyChanged;

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

        internal Settings.ActionKeyword KeywordProperty { get; }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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