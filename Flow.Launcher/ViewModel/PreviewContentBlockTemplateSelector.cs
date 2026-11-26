using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public sealed class PreviewContentBlockTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Resource key of the template for muted text.
    /// </summary>
    public const string MutedTextTemplateKey = "MutedTextPreviewBlock";

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PreviewContentBlockViewModel block || container is not FrameworkElement element)
        {
            return null;
        }

        // Text is the only block that renders two ways, so it is the only one not keyed by its type
        if (block.InputBlock is TextPreviewBlock { Emphasis: PreviewTextEmphasis.Muted })
        {
            return element.FindResource(MutedTextTemplateKey) as DataTemplate;
        }

        return element.FindResource(block.InputBlock.GetType()) as DataTemplate;
    }
}