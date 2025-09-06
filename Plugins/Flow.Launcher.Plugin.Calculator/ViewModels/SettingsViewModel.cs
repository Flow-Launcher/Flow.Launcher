using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Calculator.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public Settings Settings { get; init; }

        public static IEnumerable<int> MaxDecimalPlacesRange => Enumerable.Range(1, 20);

        public List<DecimalSeparatorLocalized> AllDecimalSeparator { get; } = DecimalSeparatorLocalized.GetValues();

        public DecimalSeparator SelectedDecimalSeparator
        {
            get => Settings.DecimalSeparator;
            set
            {
                if (Settings.DecimalSeparator != value)
                {
                    Settings.DecimalSeparator = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
