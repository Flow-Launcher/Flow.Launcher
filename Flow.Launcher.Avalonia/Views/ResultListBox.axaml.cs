using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Flow.Launcher.Avalonia.Views;

public partial class ResultListBox : UserControl
{
    private ListBox? _listBox;

    public ResultListBox()
    {
        InitializeComponent();
        _listBox = this.FindControl<ListBox>("ResultsList");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        // Handle left click on result item
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            // The ListBox handles selection automatically
            // Additional click handling can be added here
        }
    }
}
