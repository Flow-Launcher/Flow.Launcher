using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Url;

public partial class URLSettings : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public URLSettings(Settings settings)
    {
        _viewModel = new SettingsViewModel(settings);
        DataContext = _viewModel;
        InitializeComponent();
    }
}
