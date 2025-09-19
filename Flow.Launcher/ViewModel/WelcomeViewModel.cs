using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public partial class WelcomeViewModel : BaseModel
    {
        public const int MaxPageNum = 5;

        public string PageDisplay => $"{PageNum}/5";

        private int _pageNum = 1;
        public int PageNum
        {
            get => _pageNum;
            set
            {
                if (_pageNum != value)
                {
                    _pageNum = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PageDisplay));
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

        private void UpdateView()
        {
            if (PageNum == 1)
            {
                BackEnabled = false;
                NextEnabled = true;
            }
            else if (PageNum == MaxPageNum)
            {
                BackEnabled = true;
                NextEnabled = false;
            }
            else
            {
                BackEnabled = true;
                NextEnabled = true;
            }
        }
    }
}
