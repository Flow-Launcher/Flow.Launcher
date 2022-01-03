namespace Flow.Launcher.Plugin.BrowserBookmark.Models
{
    public class CustomBrowser : BaseModel
    {
        private string _name;
        private string _dataDirectoryPath;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            } 
        }
        public string DataDirectoryPath
        {
            get => _dataDirectoryPath;
            set
            {
                _dataDirectoryPath = value;
                OnPropertyChanged(nameof(DataDirectoryPath));
            }
        }
    }
}