using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Image;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class ImageLoaderTest
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task GivenWidePngIcon_WhenLoadedAsSmallIcon_ThenBitmapIsConstrainedToSmallIconBoxAsync()
        {
            await ImageLoader.InitializeAsync();
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.png");
            CreatePng(path, width: 400, height: 100);

            try
            {
                var image = await ImageLoader.LoadAsync(path, loadFullImage: false, cacheImage: false);
                var bitmap = (BitmapSource)image;

                ClassicAssert.LessOrEqual(bitmap.PixelWidth, ImageLoader.SmallIconSize);
                ClassicAssert.LessOrEqual(bitmap.PixelHeight, ImageLoader.SmallIconSize);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task GivenInvalidPngIcon_WhenLoadedAsSmallIcon_ThenMissingImageIsReturnedWithoutThrowingAsync()
        {
            await ImageLoader.InitializeAsync();
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.png");
            await File.WriteAllTextAsync(path, "not a png");

            try
            {
                var image = await ImageLoader.LoadAsync(path, loadFullImage: false, cacheImage: false);

                ClassicAssert.AreSame(ImageLoader.MissingImage, image);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task GivenInvalidPngPreview_WhenLoadedAsFullImage_ThenImagePlaceholderIsReturnedWithoutThrowingAsync()
        {
            await ImageLoader.InitializeAsync();
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.png");
            await File.WriteAllTextAsync(path, "not a png");

            try
            {
                var image = await ImageLoader.LoadAsync(path, loadFullImage: true, cacheImage: false);

                ClassicAssert.AreSame(ImageLoader.Image, image);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task GivenInvalidSvgIcon_WhenLoaded_ThenImagePlaceholderIsReturnedWithoutThrowingAsync()
        {
            await ImageLoader.InitializeAsync();
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid():N}.svg");
            await File.WriteAllTextAsync(path, "<svg><not-closed>");

            try
            {
                var image = await ImageLoader.LoadAsync(path, loadFullImage: false, cacheImage: false);

                ClassicAssert.AreSame(ImageLoader.Image, image);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void CreatePng(string path, int width, int height)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xD0;
                pixels[i + 1] = 0x90;
                pixels[i + 2] = 0x30;
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
