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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Core;
using OpenGraphNet;
using System.Net.Http;
using System.Diagnostics;

namespace Flow.Launcher.Plugin.WebSearch
{
    /// <summary>
    /// PreviewPanel.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PreviewPanel : UserControl
    {
        public string Url { get; set; }

        public PreviewPanel(string url)
        {
            Url = url;
            InitializeComponent();
            //webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            LoadWebsite(url);
        }
        async Task LoadWebsite(string Url)
        {
            String UserAgent = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/27.0.1453.94 Safari/537.36";
            //HttpClient client = new HttpClient();
            //Task<string> getStringTask = client.GetStringAsync("http://msdn.microsoft.com");   
            OpenGraph graph = await OpenGraph.ParseUrlAsync("http://msdn.microsoft.com");
            Debug.WriteLine(graph);
            Title.Text = graph.Metadata["og:title"].First().Value;

            var image = new Image();
            var fullFilePath = graph.Metadata["og:image"].First().Value; ;

            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(fullFilePath, UriKind.Absolute);
            bitmap.EndInit();
            Image.Source = bitmap;

            Desc.Text = graph.Metadata["og:description"].First().Value;
        }
/*
            private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Linux; Android 7.0; SM-G930V Build/NRD90M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.125 Mobile Safari/537.36";
        }
*/
    }
}
