using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace Flow.Launcher.Resources.Controls
{
    public partial class Card : UserControl
    {
        public enum CardType
        {
            Default,
            Inside,
            InsideFit
        }

        public Card()
        {
            InitializeComponent();
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }
        public static readonly DependencyProperty TitleProperty =
          DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));

        public string Sub
        {
            get { return (string)GetValue(SubProperty); }
            set { SetValue(SubProperty, value); }
        }
        public static readonly DependencyProperty SubProperty =
          DependencyProperty.Register(nameof(Sub), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));

        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }
        public static readonly DependencyProperty IconProperty =
          DependencyProperty.Register(nameof(Icon), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets additional content for the UserControl
        /// </summary>
        public object AdditionalContent
        {
            get { return (object)GetValue(AdditionalContentProperty); }
            set { SetValue(AdditionalContentProperty, value); }
        }
        public static readonly DependencyProperty AdditionalContentProperty =
            DependencyProperty.Register(nameof(AdditionalContent), typeof(object), typeof(Card),
              new PropertyMetadata(null));
        public CardType Type
        {
            get { return (CardType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }
        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(nameof(Type), typeof(CardType), typeof(Card),
              new PropertyMetadata(CardType.Default));
    }
}
