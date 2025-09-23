using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.Sys;

public partial class SysSettings : UserControl
{
    private readonly Settings _settings;

    public SysSettings(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _settings = viewModel.Settings;
        DataContext = viewModel;
    }

    private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ListView listView = sender as ListView;
        GridView gView = listView.View as GridView;

        var workingWidth =
            listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar

        if (workingWidth <= 0) return;

        var col1 = 0.2;
        var col2 = 0.6;
        var col3 = 0.2;

        gView.Columns[0].Width = workingWidth * col1;
        gView.Columns[1].Width = workingWidth * col2;
        gView.Columns[2].Width = workingWidth * col3;
    }

    public void OnEditCommandKeywordClick(object sender, RoutedEventArgs e)
    {
        var commandKeyword = new CommandKeywordSettingWindow(_settings.SelectedCommand);
        commandKeyword.ShowDialog();
    }

    private void MouseDoubleClickItem(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)e.OriginalSource).DataContext is Command && _settings.SelectedCommand != null)
        {
            var commandKeyword = new CommandKeywordSettingWindow(_settings.SelectedCommand);
            commandKeyword.ShowDialog();
        }
    }
}
