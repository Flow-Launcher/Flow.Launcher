using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class ResultViewModelPreviewImageTest
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task GivenEmptyPreviewImagePathAndIconPath_WhenPreviewImageLoads_ThenIconImageIsUsedAsync()
        {
            await ImageLoader.InitializeAsync();
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.png");
            CreatePng(path, width: 64, height: 64);

            try
            {
                var expected = await ImageLoader.LoadAsync(path, loadFullImage: true);
                var result = new Result
                {
                    Title = "Preview fallback",
                    IcoPath = path,
                    Preview = new Result.PreviewInfo
                    {
                        PreviewImagePath = string.Empty
                    }
                };
                var viewModel = new ResultViewModel(result, new Settings());

                await LoadPreviewImageAsync(viewModel);

                ClassicAssert.AreSame(expected, viewModel.PreviewImage);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static Task LoadPreviewImageAsync(ResultViewModel viewModel)
        {
            var method = typeof(ResultViewModel).GetMethod("LoadPreviewImageAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (Task)method.Invoke(viewModel, []);
        }

        private static void CreatePng(string path, int width, int height)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0x40;
                pixels[i + 1] = 0xA0;
                pixels[i + 2] = 0xE0;
                pixels[i + 3] = 0xFF;
            }

            var source = BitmapSource.Create(
                width,
                height,
                dpiX: 96,
                dpiY: 96,
                PixelFormats.Bgra32,
                palette: null,
                pixels,
                stride);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
