using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPanePlugins
{
    private SettingsPanePluginsViewModel _viewModel = null!;
    private SettingWindowViewModel _settingViewModel = null;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPanePluginsViewModel>();
            _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page type
        _settingViewModel.PageType = typeof(SettingsPanePlugins);
        base.OnNavigatedTo(e);
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsPanePluginsViewModel.FilterText))
        {
            ((CollectionViewSource)FindResource("PluginCollectionView")).View.Refresh();
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
        PluginFilterTextbox.Focus();
    }

    private void PluginCollectionView_OnFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not PluginViewModel plugin)
        {
            e.Accepted = false;
            return;
        }

        e.Accepted = _viewModel.SatisfiesFilter(plugin);
    }
}
