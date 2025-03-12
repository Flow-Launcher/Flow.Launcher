using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Sys
{
    public partial class SysSettings : UserControl
    {
        public SysSettings(List<Result> Results)
        {
            InitializeComponent();

            foreach (var Result in Results)
            {
                lbxCommands.Items.Add(Result);
            }
        }
        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            GridView gView = listView.View as GridView;

            var workingWidth = listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar
            var col1 = 0.3;
            var col2 = 0.7;

            if (workingWidth <= 0)
            {
                return;
            }

            gView.Columns[0].Width = workingWidth * col1;
            gView.Columns[1].Width = workingWidth * col2;
        }
    }
}
