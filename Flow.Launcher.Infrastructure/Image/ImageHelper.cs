using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageHelper
    {
        public static Bitmap LoadFromResource(Uri resourceUri)
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }

        public static async Task<Bitmap?> LoadFromFile(string path, int? width)
        {
            if (width is null)
            {
                return new Bitmap(path);
            }

            await using var stream = File.OpenRead(path);
            MemoryStream memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return Bitmap.DecodeToWidth(memoryStream, width.Value);
        }

        public static async Task<Bitmap?> LoadFromWeb(Uri url)
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                return new Bitmap(new MemoryStream(data));
            }
            catch (HttpRequestException ex)
            {
                Log.Error($"An error occurred while downloading image '{url}' : {ex.Message}");
                return null;
            }
        }
    }
}
