using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPanePluginStore
{
    private SettingsPanePluginStoreViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            _viewModel = new SettingsPanePluginStoreViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        base.OnNavigatedTo(e);
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsPanePluginStoreViewModel.FilterText))
        {
            ((CollectionViewSource)FindResource("PluginStoreCollectionView")).View.Refresh();
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
}
