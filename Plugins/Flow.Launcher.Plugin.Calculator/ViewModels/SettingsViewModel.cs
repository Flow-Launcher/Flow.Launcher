using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Calculator.ViewModels;

public class SettingsViewModel(Settings settings) : BaseModel
{
    public Settings Settings { get; } = settings;

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

    public bool EnableHistory
    {
        get => Settings.EnableHistory;
        set
        {
            if (Settings.EnableHistory != value)
            {
                Settings.EnableHistory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowHistoryQueryWarning));
            }
        }
    }

    public List<HistoryCreationModeLocalized> AllHistoryCreationMode { get; } = HistoryCreationModeLocalized.GetValues();

    public HistoryCreationMode SelectedHistoryCreationMode
    {
        get => Settings.HistoryCreationMode;
        set
        {
            if (Settings.HistoryCreationMode != value)
            {
                Settings.HistoryCreationMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowHistoryQueryWarning));
            }
        }
    }

    public bool ShowHistoryQueryWarning => EnableHistory && SelectedHistoryCreationMode == HistoryCreationMode.OnQuery;
}
