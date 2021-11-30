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
using WelcomePages = Flow.Launcher.Resources.Pages;

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
            //ContentFrame.Navigate(typeof(SamplePages.SamplePage1));
            ContentFrame.Navigate(typeof(WelcomePages.WelcomePage1));
            BackButton.IsEnabled = false;
        }
        private int page;
        private int MaxPage = 5;
        
        /*public int Page
        {
            get { return this.page; }
            set
            {
                this.page = value;
                if (this.page == 1)
                {
                    this.BackButton.IsEnabled = false; 
                }
                else 
                {
                    this.BackButton.IsEnabled = true;
                };
            }
        }
        */

        private void ButtonDisabler()
        {
            if (page == 0)
            {
                BackButton.IsEnabled = false;
                NextButton.IsEnabled = true;
            }
            else if (page == MaxPage - 1)
            {
                BackButton.IsEnabled = true;
                NextButton.IsEnabled = false;
            }
            else 
            {
                BackButton.IsEnabled = true;
                NextButton.IsEnabled = true;
            }
        }
        private NavigationTransitionInfo _transitionInfo = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };

        private static Type PageSelector(int a)
        {
            switch (a)
            {
                case 0:
                    return typeof(WelcomePages.WelcomePage1);
                case 1:
                    return typeof(WelcomePages.WelcomePage2);
                case 2:
                    return typeof(WelcomePages.WelcomePage3);
                case 3:
                    return typeof(WelcomePages.WelcomePage4);
                case 4:
                    return typeof(WelcomePages.WelcomePage5);
                default:
                    return typeof(WelcomePages.WelcomePage1);
            }
        }
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            page = page + 1;
            ButtonDisabler();
            var pageToNavigateTo = PageSelector(page);
                ContentFrame.Navigate(pageToNavigateTo, null, _transitionInfo);
        }

        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (page > 0)
            {
                page = page - 1;
                ButtonDisabler();
                ContentFrame.GoBack();
            }
            else
            {
                BackButton.IsEnabled = false;
            }
        }
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

