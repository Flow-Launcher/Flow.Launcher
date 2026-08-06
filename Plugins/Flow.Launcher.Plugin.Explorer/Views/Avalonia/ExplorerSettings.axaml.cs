#nullable enable
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using System;

namespace Flow.Launcher.Plugin.Explorer.Views.Avalonia
{
    public partial class ExplorerSettings : UserControl
    {
        public ExplorerSettings()
        {
            InitializeComponent();
        }

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void btnOpenIndexingOptions_Click(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.OpenWindowsIndexingOptions();
        }

        private async void BtnOpenFileEditorPath_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                vm.FileEditorPath = files[0].Path.LocalPath;
            }
        }

        private async void BtnOpenFolderEditorPath_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                vm.FolderEditorPath = folders[0].Path.LocalPath;
            }
        }

        private async void BtnOpenShellPath_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                vm.ShellPath = files[0].Path.LocalPath;
            }
        }
    }
}
