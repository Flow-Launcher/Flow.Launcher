using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls;

public partial class CardGroup : UserControl
{
    public enum CardGroupPosition
    {
        NotInGroup,
        First,
        Middle,
        Last
    }

    public new ObservableCollection<Card> Content
    {
        get { return (ObservableCollection<Card>)GetValue(ContentProperty); }
        set { SetValue(ContentProperty, value); }
    }

    public static new readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(ObservableCollection<Card>), typeof(CardGroup));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.RegisterAttached(
        "Position", typeof(CardGroupPosition), typeof(CardGroup),
        new FrameworkPropertyMetadata(CardGroupPosition.NotInGroup, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static void SetPosition(UIElement element, CardGroupPosition value)
    {
        element.SetValue(PositionProperty, value);
    }

    public static CardGroupPosition GetPosition(UIElement element)
    {
        return (CardGroupPosition)element.GetValue(PositionProperty);
    }

    public CardGroup()
    {
        InitializeComponent();
        Content = new ObservableCollection<Card>();
    }
}
