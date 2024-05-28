using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Flow.Launcher.SettingPages.ViewModels;
using Page = ModernWpf.Controls.Page;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneTheme : Page
{
    private SettingsPaneThemeViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings })
                throw new ArgumentException($"Settings are required for {nameof(SettingsPaneTheme)}.");
            _viewModel = new SettingsPaneThemeViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }

        base.OnNavigatedTo(e);
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.UpdateColorScheme();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        /*The FamilyTypeface should initialize all of its various properties.*/
        FamilyTypeface targetTypeface = new FamilyTypeface { Stretch = FontStretches.Normal, Weight = FontWeights.Normal, Style = FontStyles.Normal };

        QueryBoxFontSize.Value = 20;
        QueryBoxFontComboBox.SelectedIndex = SearchFontIndex("Segoe UI", QueryBoxFontComboBox);
        QueryBoxFontStyleComboBox.SelectedIndex = SearchFontStyleIndex(targetTypeface, QueryBoxFontStyleComboBox);

        ResultItemFontComboBox.SelectedIndex = SearchFontIndex("Segoe UI", ResultItemFontComboBox);
        ResultItemFontStyleComboBox.SelectedIndex = SearchFontStyleIndex(targetTypeface, ResultItemFontStyleComboBox);
        ResultItemFontSize.Value = 16;

        ResultSubItemFontComboBox.SelectedIndex = SearchFontIndex("Segoe UI", ResultSubItemFontComboBox);
        ResultSubItemFontStyleComboBox.SelectedIndex = SearchFontStyleIndex(targetTypeface, ResultSubItemFontStyleComboBox);
        ResultSubItemFontSize.Value = 13;

        WindowHeightValue.Value = 42;
        ItemHeightValue.Value = 58;
    }

    private int SearchFontIndex(string targetFont, ComboBox combo)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i]?.ToString() == targetFont)
            {
                return i;
            }
        }
        return 0;
    }

    private int SearchFontStyleIndex(FamilyTypeface targetTypeface, ComboBox combo)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is FamilyTypeface typefaceItem &&
                typefaceItem.Stretch == targetTypeface.Stretch &&
                typefaceItem.Weight == targetTypeface.Weight &&
                typefaceItem.Style == targetTypeface.Style)
            {
                return i;
            }
        }
        return 0;
    }
}
