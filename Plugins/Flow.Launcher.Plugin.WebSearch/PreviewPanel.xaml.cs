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
using AngleSharp;
using AngleSharp.Html.Parser;
using AngleSharp.Common;
using AngleSharp.Html.Dom;
using AngleSharp.Html;
using AngleSharp.Io;
using AngleSharp.Dom;
using HtmlAgilityPack;
using Mono.Cecil;
using YamlDotNet.Core;
using System.Drawing;

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
            // Download the web page
            HttpClient httpClient = new HttpClient();
            string html = await httpClient.GetStringAsync(Url);
            HtmlParser parser = new HtmlParser();
            IHtmlDocument document = parser.ParseDocument(html);

            var titleElement = document.QuerySelectorAll("meta[property=\"og:title\"]")
                        .FirstOrDefault();
            if (titleElement != null)
                Title.Text = titleElement.GetAttribute("content");
            else
            {
                // Try and get from the <TITLE> element
                titleElement = document.QuerySelectorAll("title")
                                        .FirstOrDefault();
                if (titleElement != null)
                    Title.Text = titleElement.TextContent;
            }

            // Try and get the description from OpenGraph data
            var descriptionElement = document.QuerySelectorAll("meta[property=\"og:description\"]").FirstOrDefault();
            if (descriptionElement != null)
                Desc.Text = descriptionElement.GetAttribute("content");
            else
            {
                descriptionElement = document.QuerySelectorAll("meta[name=\"Description\"]").FirstOrDefault();
                if (descriptionElement != null)
                    Desc.Text = descriptionElement.GetAttribute("content");
                else 
                {
                    //descriptionElement = document.QuerySelectorAll("div[id=\"bodyContent\"]").FirstOrDefault();
                    var pars = document.QuerySelectorAll("p");
                    string abc = "hello";
                    foreach (var par in pars)
                    {
                        abc = par.Text();
                        abc = abc + abc;
                        
                    }
                    abc = pars[0].Text().Trim() + pars[1].Text().Trim() + pars[2].Text().Trim() + pars[3].Text().Trim() ;
                    Debug.WriteLine(pars);
                    Desc.Text =  abc.Substring(0,250) + "...";
                    //Debug.WriteLine(descriptionElement);
                    //if (descriptionElement != null)

                }
            }

            // Try and get the images from OpenGraph data
            var imageElements = document.QuerySelectorAll("meta[property=\"og:image\"]");
            foreach (var imageElement in imageElements)
            {
                Uri imageUri = null;
                BitmapImage bitmap = new BitmapImage();
                string imageUrl = imageElement.GetAttribute("content");
                if (imageUrl != null && Uri.TryCreate(imageUrl, UriKind.Absolute, out imageUri))
                    //Images.Add(imageUri);
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
                    bitmap.EndInit();
                    Image.Source = bitmap;
            }


            //HtmlDocument document = DocumentBuilder.Html(html);
            // Load the HTML Document
            //HtmlDocument document = DocumentBuilder.Html(html);
            /*
            String UserAgent = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/27.0.1453.94 Safari/537.36";
            //HttpClient client = new HttpClient();
            //Task<string> getStringTask = client.GetStringAsync("http://msdn.microsoft.com");   
            OpenGraph graph = await OpenGraph.ParseUrlAsync("https://youtube.com");
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
            */
        }
/*
            private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Linux; Android 7.0; SM-G930V Build/NRD90M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.125 Mobile Safari/537.36";
        }
*/
    }
}
