using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.ViewModel;

/// <summary>
/// ViewModel for a single result item.
/// </summary>
public partial class ResultViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subTitle = string.Empty;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Settings? _settings;

    // Computed properties for display
    public bool ShowIcon => !string.IsNullOrEmpty(IconPath);
    
    public bool ShowSubTitle => !string.IsNullOrEmpty(SubTitle);
}
