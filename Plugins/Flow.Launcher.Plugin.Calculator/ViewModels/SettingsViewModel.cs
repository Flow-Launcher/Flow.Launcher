using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Calculator.ViewModels;

public class SettingsViewModel(Settings settings) : BaseModel
{
    public Settings Settings { get; init; } = settings;

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
