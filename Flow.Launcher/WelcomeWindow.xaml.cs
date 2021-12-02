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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Resources.Pages;
using ModernWpf.Media.Animation;

namespace Flow.Launcher
{
    public partial class WelcomeWindow : Window
    {
        private readonly List<Page> pages;
        private readonly Settings settings;

        public WelcomeWindow(Settings settings)
        {
            InitializeComponent();
            BackButton.IsEnabled = false;
            this.settings = settings;

            pages = new()
            {
                new WelcomePage1(settings),
                new WelcomePage2(settings),
                new WelcomePage3(),
                new WelcomePage4(),
                new WelcomePage5(settings),
            };
            ContentFrame.Navigate(pages[0]);
        }

        private NavigationTransitionInfo _transitionInfo = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
        Storyboard sb = new Storyboard();
        private int page;
        private int pageNum = 1;
        private int MaxPage = 5;
        public string PageDisplay => $"{pageNum}/5";

        private void UpdateView()
        {
            pageNum = page + 1;
            PageNavigation.Text = PageDisplay;
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

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            page = page + 1;
            UpdateView();
            var pageToNavigateTo = pages[page];

            ContentFrame.Navigate(pageToNavigateTo, _transitionInfo);
        }

        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (page > 0)
            {
                page--;
                UpdateView();
                var pageToNavigateTo = pages[page];
                ContentFrame.Navigate(pageToNavigateTo, _transitionInfo);
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

