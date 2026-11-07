using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public sealed class PreviewContentBlockTemplateSelector : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PreviewContentBlockViewModel block || container is not FrameworkElement element)
        {
            return null;
        }

        return element.FindResource(block.InputBlock.GetType()) as DataTemplate;
    }
}