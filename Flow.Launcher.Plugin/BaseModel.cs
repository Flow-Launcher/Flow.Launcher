using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Flow.Launcher.Plugin
{
    public class BaseModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));  
        }
    }
}