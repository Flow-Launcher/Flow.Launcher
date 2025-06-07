using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher.Resources.Controls;

public partial class InstalledPluginDisplay
{
    public InstalledPluginDisplay()
    {
        InitializeComponent();
    }

    // This is used for PriorityControl to force its value to be 0 when the user clears the value
    private void NumberBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue))
        {
            sender.Value = 0;
        }
    }
}
