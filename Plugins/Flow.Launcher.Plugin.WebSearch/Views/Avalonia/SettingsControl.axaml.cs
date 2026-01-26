using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using System;

namespace Flow.Launcher.Plugin.WebSearch.Views.Avalonia
{
    public partial class SettingsControl : UserControl
    {
        private readonly PluginInitContext _context;
        private readonly SettingsViewModel _viewModel;

        public SettingsControl(PluginInitContext context, SettingsViewModel viewModel)
        {
            InitializeComponent();
            _context = context;
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnAddSearchSourceClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Add Search Source",
                Content = "Adding search sources is not yet implemented in the Avalonia version.",
                CloseButtonText = "OK"
            };
            
            if (VisualRoot is TopLevel topLevel)
            {
                 await dialog.ShowAsync(topLevel);
            }
        }

        private async void OnEditSearchSourceClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Edit Search Source",
                Content = "Editing search sources is not yet implemented in the Avalonia version.",
                CloseButtonText = "OK"
            };
            
            if (VisualRoot is TopLevel topLevel)
            {
                 await dialog.ShowAsync(topLevel);
            }
        }

        private async void OnDeleteSearchSourceClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Settings.SelectedSearchSource != null)
            {
                var selected = _viewModel.Settings.SelectedSearchSource;
                var warning = _context.API.GetTranslation("flowlauncher_plugin_websearch_delete_warning");
                var formatted = string.Format(warning, selected.Title);

                var dialog = new ContentDialog
                {
                    Title = "Delete Search Source",
                    Content = formatted,
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No"
                };

                if (VisualRoot is TopLevel topLevel)
                {
                    var result = await dialog.ShowAsync(topLevel);

                    if (result == ContentDialogResult.Primary)
                    {
                        var id = _context.CurrentPluginMetadata.ID;
                        _context.API.RemoveActionKeyword(id, selected.ActionKeyword);
                        _viewModel.Settings.SearchSources.Remove(selected);
                    }
                }
            }
        }
    }
}
