using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flow.Launcher
{
    public partial class ResultListBox
    {
        public ICommand SelectionChangedCommand { get; set; }

        protected object _lock = new object();
        private Point _lastpos;
        private ListBoxItem curItem = null;

        public ResultListBox()
        {
            InitializeComponent();
        }

        public void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                ScrollIntoView(e.AddedItems[0]);
            }
            //string text = ((sender as ListBox)?.SelectedItem as ListBoxItem).ToString();
            if (((ListBox)sender).SelectedItem != null)
            { 
                //MessageBox.Show("You have selected " + ((ListBox)sender).SelectedItem);
                System.Diagnostics.Debug.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                //var text = ((ListBox)sender).SelectedItem;
                //var text = (ListBox)ResultList.SelectedItem;
                //System.Diagnostics.Debug.WriteLine(text);
                //ListBox lstBox = (ListBox)sender;
                //ListBoxItem item = lstBox.SelectedItem;

                //var listBox = (ListBox)sender;
                //ListBoxItem selectedItem = listBox.SelectedItem;
                //someTextBox.Text = selectedItem.id;

            }


        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            lock(_lock)
            {
                curItem = (ListBoxItem)sender;
                var p = e.GetPosition((IInputElement)sender);
                _lastpos = p;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            lock(_lock)
            {
                var p = e.GetPosition((IInputElement)sender);
                if (_lastpos != p)
                {
                    ((ListBoxItem)sender).IsSelected = true;
                }
            }
        }

        private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            lock(_lock)
            {
                if (curItem != null)
                {
                    curItem.IsSelected = true;
                }
            }
        }
    }
}
