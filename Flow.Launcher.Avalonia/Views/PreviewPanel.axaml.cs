using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Flow.Launcher.Avalonia.Views;

public partial class PreviewPanel : UserControl
{
    public PreviewPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
