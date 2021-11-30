using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ModernWpf.Media.Animation;
using Page = ModernWpf.Controls.Page;

namespace Flow.Launcher
{
    /// <summary>
    /// WelcomeWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
            ContentFrame.Navigate(typeof(SamplePages.SamplePage1));
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            var pageToNavigateTo = ContentFrame.BackStackDepth % 2 == 1 ? typeof(SamplePages.SamplePage1) : typeof(SamplePages.SamplePage2);

            if (_transitionInfo == null)
            {
                // Default behavior, no transition set or used.
                ContentFrame.Navigate(pageToNavigateTo, null);
            }
            else
            {
                // Explicit transition info used.
                ContentFrame.Navigate(pageToNavigateTo, null, _transitionInfo);
            }
            */
        }

        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {/*
            if (ContentFrame.BackStackDepth > 0)
            {
                ContentFrame.GoBack();
            }
            */
        }
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
