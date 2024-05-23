using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls;

public class CardGroupCardStyleSelector : StyleSelector
{
    public Style FirstStyle { get; set; }
    public Style MiddleStyle { get; set; }
    public Style LastStyle { get; set; }

    public override Style SelectStyle(object item, DependencyObject container)
    {
        var itemsControl = ItemsControl.ItemsControlFromItemContainer(container);
        var index = itemsControl.ItemContainerGenerator.IndexFromContainer(container);

        if (index == 0) return FirstStyle;
        if (index == itemsControl.Items.Count - 1) return LastStyle;
        return MiddleStyle;
    }
}
