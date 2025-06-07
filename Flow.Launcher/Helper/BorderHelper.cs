using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Helper;

public static class BorderHelper
{
    #region Child

    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.RegisterAttached(
            "Child",
            typeof(UIElement),
            typeof(BorderHelper),
            new PropertyMetadata(default(UIElement), OnChildChanged));

    public static UIElement GetChild(Border border)
    {
        return (UIElement)border.GetValue(ChildProperty);
    }

    public static void SetChild(Border border, UIElement value)
    {
        border.SetValue(ChildProperty, value);
    }

    private static void OnChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Border)d).Child = (UIElement)e.NewValue;
    }

    #endregion
}
