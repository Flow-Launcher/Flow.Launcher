using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls;

public partial class Card2 : UserControl
{
        public enum CardType
        {
            Default,
            Inside,
            InsideFit
        }

        public new FrameworkElement Content
        {
            get => (FrameworkElement)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }
        public static new readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(FrameworkElement), typeof(Card2));

        public Card2()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty =
          DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card2), new PropertyMetadata(string.Empty));

        public string Sub
        {
            get => (string)GetValue(SubProperty);
            set => SetValue(SubProperty, value);
        }
        public static readonly DependencyProperty SubProperty =
          DependencyProperty.Register(nameof(Sub), typeof(string), typeof(Card2), new PropertyMetadata(string.Empty));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
        public static readonly DependencyProperty IconProperty =
          DependencyProperty.Register(nameof(Icon), typeof(string), typeof(Card2), new PropertyMetadata(defaultValue: null));

        /// <summary>
        /// Gets or sets additional content for the UserControl
        /// </summary>
        public object AdditionalContent
        {
            get => GetValue(AdditionalContentProperty);
            set => SetValue(AdditionalContentProperty, value);
        }
        public static readonly DependencyProperty AdditionalContentProperty =
            DependencyProperty.Register(nameof(AdditionalContent), typeof(object), typeof(Card2),
              new PropertyMetadata(null));
        public CardType Type
        {
            get => (CardType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }
        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(nameof(Type), typeof(CardType), typeof(Card2),
              new PropertyMetadata(CardType.Default));

}
