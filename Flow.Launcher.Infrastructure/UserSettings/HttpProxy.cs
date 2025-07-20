namespace Flow.Launcher.Infrastructure.UserSettings
{
    public enum ProxyProperty
    {
        Enabled,
        Server,
        Port,
        UserName,
        Password
    }

    public class HttpProxy
    {
        private bool _enabled = false;
        private string _server;
        private int _port;
        private string _userName;
        private string _password;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                OnPropertyChanged(ProxyProperty.Enabled);
            }
        }

        public string Server
        {
            get => _server;
            set
            {
                _server = value;
                OnPropertyChanged(ProxyProperty.Server);
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(ProxyProperty.Port);
            }
        }

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(ProxyProperty.UserName);
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(ProxyProperty.Password);
            }
        }

        public delegate void ProxyPropertyChangedHandler(ProxyProperty property);
        public event ProxyPropertyChangedHandler PropertyChanged;

        private void OnPropertyChanged(ProxyProperty property)
        {
            PropertyChanged?.Invoke(property);
        }
    }
}