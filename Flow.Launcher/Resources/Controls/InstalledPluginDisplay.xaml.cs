using ModernWpf.Controls;

namespace Flow.Launcher.Resources.Controls;

public partial class InstalledPluginDisplay
{
    public InstalledPluginDisplay()
    {
        InitializeComponent();
    }

    private void NumberBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue))
        {
            sender.Value = 0;
        }
    }
}
