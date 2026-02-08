using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Plugin.Program.ViewModels;

namespace Flow.Launcher.Plugin.Program.Views.Avalonia;

public partial class ProgramSetting : UserControl
{
    public ProgramSetting()
    {
        InitializeComponent();
    }

    public ProgramSetting(PluginInitContext context, Settings settings)
    {
        DataContext = new ProgramSettingViewModel(context, settings);
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
