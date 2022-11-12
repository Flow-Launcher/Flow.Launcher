using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Flow.Launcher.Core.Resource;
using YamlDotNet.Core.Tokens;

namespace Flow.Launcher
{
    /// <summary>
    /// MessageBoxEx.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MessageBoxEx : Window
    {
        static MessageBoxEx msgBox;
        static string Button_id;
        public MessageBoxEx()
        {
            InitializeComponent();
        }


        public static string Show(string txtMessage, string txtTitle)
        {
            msgBox = new MessageBoxEx();
            msgBox.TitleTextBlock.Text = txtTitle;
            msgBox.DescTextBlock.Text = txtMessage;
            //msgBox.label1.Text = txtMessage;
            //msgBox.Text = txtTitle;
            msgBox.ShowDialog();
            return Button_id;
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {

            Close();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
