using System;
using System.Windows.Input;

namespace Flow.Launcher.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _action;

        public RelayCommand(Action<object> action)
        {
            _action = action;
        }

        public virtual bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable CS0067 // the event is never used
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        public virtual void Execute(object parameter)
        {
            _action?.Invoke(parameter);
        }
    }
}
