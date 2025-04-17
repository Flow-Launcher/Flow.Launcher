using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public enum WelcomePage
    {
        Intro = 1,           // WelcomePage1
        Features = 2,        // WelcomePage2
        UserType = 3,        // WelcomePageUserType
        Hotkeys = 4,         // WelcomePage3
        Commands = 5,        // WelcomePage4
        Finish = 6           // WelcomePage5
    }

    public partial class WelcomeViewModel : BaseModel
    {
        public const int MaxPageNum = 6; 
        
        public static readonly WelcomePage[] PageSequence = new[]
        {
            WelcomePage.Intro,
            WelcomePage.Features,
            WelcomePage.UserType,
            WelcomePage.Hotkeys,
            WelcomePage.Commands,
            WelcomePage.Finish
        };

        public string PageDisplay => $"{GetPageIndex(CurrentPage) + 1}/{PageSequence.Length}";

        private WelcomePage _currentPage = WelcomePage.Intro;
        public WelcomePage CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    _pageNum = (int)value; 
                    OnPropertyChanged();
                    UpdateView();
                }
            }
        }

        private int _pageNum = 1;
        public int PageNum
        {
            get => _pageNum;
            set
            {
                if (_pageNum != value)
                {
                    _pageNum = value;
                    _currentPage = (WelcomePage)value;
                    OnPropertyChanged();
                    UpdateView();
                }
            }
        }

        private bool _backEnabled = false;
        public bool BackEnabled
        {
            get => _backEnabled;
            set
            {
                _backEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _nextEnabled = true;
        public bool NextEnabled
        {
            get => _nextEnabled;
            set
            {
                _nextEnabled = value;
                OnPropertyChanged();
            }
        }

        private int GetPageIndex(WelcomePage page)
        {
            return Array.IndexOf(PageSequence, page);
        }

        private void UpdateView()
        {
            OnPropertyChanged(nameof(PageDisplay));
            
            int index = GetPageIndex(CurrentPage);
            BackEnabled = index > 0;
            NextEnabled = index < PageSequence.Length - 1;
        }
    }
}
