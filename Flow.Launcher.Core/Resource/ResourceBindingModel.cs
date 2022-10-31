using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class ResourceBindingModel<T> : BaseModel
    {
        public ResourceBindingModel(string key, T value)
        {
            Value = value;
            Display = InternationalizationManager.Instance.GetTranslation(key);
            InternationalizationManager.Instance.OnLanguageChanged += _ =>
            {
                Display = InternationalizationManager.Instance.GetTranslation(key);
            };
        }
        public string Display
        {
            get => _display;
            set
            {
                if (value == _display)
                    return;
                _display = value;
                OnPropertyChanged();
            }
        }
        private string _display;
        public T Value { get; set; }
    }
}
