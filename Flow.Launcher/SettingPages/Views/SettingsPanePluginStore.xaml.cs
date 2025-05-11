﻿using System.ComponentModel;
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

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPanePluginStoreViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        base.OnNavigatedTo(e);
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsPanePluginStoreViewModel.FilterText):
            case nameof(SettingsPanePluginStoreViewModel.ShowDotNet):
            case nameof(SettingsPanePluginStoreViewModel.ShowPython):
            case nameof(SettingsPanePluginStoreViewModel.ShowNodeJs):
            case nameof(SettingsPanePluginStoreViewModel.ShowExecutable):
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
}
