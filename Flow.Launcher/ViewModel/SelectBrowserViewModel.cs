using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public partial class SelectBrowserViewModel : BaseModel
{
    private readonly Settings _settings;

    private int selectedCustomBrowserIndex;

    public int SelectedCustomBrowserIndex
    {
        get => selectedCustomBrowserIndex;
        set
        {
            selectedCustomBrowserIndex = value;
            OnPropertyChanged(nameof(CustomBrowser));
        }
    }

    public ObservableCollection<CustomBrowserViewModel> CustomBrowsers { get; }

    public CustomBrowserViewModel CustomBrowser => CustomBrowsers[SelectedCustomBrowserIndex];

    public SelectBrowserViewModel(Settings settings)
    {
        _settings = settings;
        CustomBrowsers = new ObservableCollection<CustomBrowserViewModel>(_settings.CustomBrowserList.Select(x => x.Copy()));
        SelectedCustomBrowserIndex = _settings.CustomBrowserIndex;
    }

    public bool SaveSettings()
    {
        _settings.CustomBrowserList = CustomBrowsers.ToList();
        _settings.CustomBrowserIndex = SelectedCustomBrowserIndex;
        return true;
    }

    [RelayCommand]
    private void Add()
    {
        CustomBrowsers.Add(new()
        {
            Name = App.API.GetTranslation("defaultBrowser_new_profile")
        });
        SelectedCustomBrowserIndex = CustomBrowsers.Count - 1;
    }

    [RelayCommand]
    private void Delete()
    {
        var currentIndex = SelectedCustomBrowserIndex;
        if (currentIndex >= 0 && currentIndex < CustomBrowsers.Count)
        {
            CustomBrowsers.RemoveAt(currentIndex);
            SelectedCustomBrowserIndex = currentIndex > 0 ? currentIndex - 1 : 0;
        }
    }
}
