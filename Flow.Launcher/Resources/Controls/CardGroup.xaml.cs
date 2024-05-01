using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls;

public partial class CardGroup : UserControl
{
    public new ObservableCollection<Card> Content
    {
        get { return (ObservableCollection<Card>)GetValue(ContentProperty); }
        set { SetValue(ContentProperty, value); }
    }

    public static new readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(ObservableCollection<Card>), typeof(CardGroup));

    public CardGroup()
    {
        InitializeComponent();
        Content = new ObservableCollection<Card>();
    }
}
