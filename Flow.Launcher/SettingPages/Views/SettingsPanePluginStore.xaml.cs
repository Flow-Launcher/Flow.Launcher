using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPanePluginStore
{
    private SettingsPanePluginStoreViewModel _viewModel = null!;
    private readonly SettingWindowViewModel _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page type
        _settingViewModel.PageType = typeof(SettingsPanePluginStore);

        // If the navigation is not triggered by button click, view model will be null again
        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPanePluginStoreViewModel>();
            DataContext = _viewModel;
        }
        if (!IsInitialized)
        {
            InitializeComponent();
        }
        UpdateCategoryGrouping();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        base.OnNavigatedTo(e);
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // If SelectedSortMode changed, then we need to update the categories
        if (e.PropertyName == nameof(SettingsPanePluginStoreViewModel.SelectedSortMode))
        {
            UpdateCategoryGrouping();
        }

        // Check if changed property requires PluginStoreCollectionView refresh
        switch (e.PropertyName)
        {
            case nameof(SettingsPanePluginStoreViewModel.FilterText):
            case nameof(SettingsPanePluginStoreViewModel.ShowDotNet):
            case nameof(SettingsPanePluginStoreViewModel.ShowPython):
            case nameof(SettingsPanePluginStoreViewModel.ShowNodeJs):
            case nameof(SettingsPanePluginStoreViewModel.ShowExecutable):
            case nameof(SettingsPanePluginStoreViewModel.SelectedSortMode):
                ((CollectionViewSource)FindResource("PluginStoreCollectionView")).View.Refresh();
                break;
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnNavigatingFrom(e);
    }

    private void SettingsPanePlugins_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.F || Keyboard.Modifiers is not ModifierKeys.Control) return;
        PluginStoreFilterTextbox.Focus();
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        App.API.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void PluginStoreCollectionView_OnFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not PluginStoreItemViewModel plugin)
        {
            e.Accepted = false;
            return;
        }

        e.Accepted = _viewModel.SatisfiesFilter(plugin);
    }

    private void UpdateCategoryGrouping()
    {
        var collectionView = (CollectionViewSource)FindResource("PluginStoreCollectionView");
        var groupDescriptions = collectionView.GroupDescriptions;

        groupDescriptions.Clear();

        // For default sorting mode we use the default categories 
        if (_viewModel.SelectedSortMode == PluginStoreSortMode.Default)
        {
            groupDescriptions.Add(new PropertyGroupDescription(nameof(PluginStoreItemViewModel.DefaultCategory)));
        }
        // Otherwise we only split by installed or not
        else
        {
            groupDescriptions.Add(new PropertyGroupDescription(nameof(PluginStoreItemViewModel.InstallCategory)));
        }
    }
}
