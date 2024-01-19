using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using static Flow.Launcher.Infrastructure.Http.Http;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly ImageCache ImageCache = new();
        private static SemaphoreSlim storageLock { get; } = new SemaphoreSlim(1, 1);
        private static BinaryStorage<Dictionary<(string, bool), int>> _storage;
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new();
        private static IImageHashGenerator _hashGenerator;
        private static readonly bool EnableImageHash = true;
        public static Bitmap MissingImage { get; } = new Bitmap(Constant.MissingImgIcon);
        public static Bitmap LoadingImage { get; } = new Bitmap(Constant.LoadingImgIcon);
        public const int SmallIconSize = 64;
        public const int FullIconSize = 256;


        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico" };

        public static async Task InitializeAsync()
        {
            _storage = new BinaryStorage<Dictionary<(string, bool), int>>("Image");
            _hashGenerator = new ImageHashGenerator();

            var usage = await LoadStorageToConcurrentDictionaryAsync();

            ImageCache.Initialize(usage.ToDictionary(x => x.Key, x => x.Value));

            foreach (var icon in new[] { Constant.DefaultIcon, Constant.MissingImgIcon })
            {
                var img = new Bitmap(icon);
                ImageCache[icon, false] = img;
            }

            _ = Task.Run(async () =>
            {
                await Stopwatch.NormalAsync("|ImageLoader.Initialize|Preload images cost", async () =>
                {
                    foreach (var ((path, isFullImage), _) in ImageCache.Data)
                    {
                        await LoadAsync(path, isFullImage);
                    }
                });
                Log.Info(
                    $"|ImageLoader.Initialize|Number of preload images is <{ImageCache.CacheSize()}>, Images Number: {ImageCache.CacheSize()}, Unique Items {ImageCache.UniqueImagesInCache()}");
            });
        }

        public static async Task SaveAsync()
        {
            await storageLock.WaitAsync();

            try
            {
                await _storage.SaveAsync(ImageCache.Data
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value.usage));
            }
            finally
            {
                storageLock.Release();
            }
        }

        private static async Task<ConcurrentDictionary<(string, bool), int>> LoadStorageToConcurrentDictionaryAsync()
        {
            await storageLock.WaitAsync();
            try
            {
                var loaded = await _storage.TryLoadAsync(new Dictionary<(string, bool), int>());

                return new ConcurrentDictionary<(string, bool), int>(loaded);
            }
            finally
            {
                storageLock.Release();
            }
        }

        private readonly record struct ImageResult(IImage image, ImageType imageType)
        {
            public ImageType ImageType { get; } = imageType;
            public IImage Image { get; } = image;
        }

        private enum ImageType
        {
            File,
            Folder,
            Data,
            ImageFile,
            FullImageFile,
            Error,
            Cache
        }

        private static async ValueTask<ImageResult> LoadInternalAsync(string path, bool loadFullImage = false)
        {
            ImageResult imageResult;

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return new ImageResult(MissingImage, ImageType.Error);
                }

                if (ImageCache.ContainsKey(path, loadFullImage))
                {
                    return new ImageResult(ImageCache[path, loadFullImage], ImageType.Cache);
                }

                if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    var image = await LoadRemoteImageAsync(loadFullImage, uriResult);
                    ImageCache[path, loadFullImage] = image;
                    return new ImageResult(image, ImageType.ImageFile);
                }

                // if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                // {
                //     var imageSource = new BitmapImage(new Uri(path));
                //     imageSource.Freeze();
                //     return new ImageResult(imageSource, ImageType.Data);
                // }

                imageResult = await Task.Run(async () => await GetThumbnailResult(path, loadFullImage));
            }
            catch (System.Exception e)
            {
                try
                {
                    // Get thumbnail may fail for certain images on the first try, retry again has proven to work
                    imageResult = await GetThumbnailResult(path, loadFullImage);
                }
                catch (System.Exception e2)
                {
                    Log.Exception($"|ImageLoader.Load|Failed to get thumbnail for {path} on first try", e);
                    Log.Exception($"|ImageLoader.Load|Failed to get thumbnail for {path} on second try", e2);

                    var image = ImageCache[Constant.MissingImgIcon, false];
                    ImageCache[path, false] = image;
                    imageResult = new ImageResult(image, ImageType.Error);
                }
            }

            return imageResult;
        }

        private static async Task<Bitmap> LoadRemoteImageAsync(bool loadFullImage, Uri uriResult)
        {
            // Download image from url
            await using var resp = await GetStreamAsync(uriResult);
            await using var buffer = new MemoryStream();
            await resp.CopyToAsync(buffer);
            buffer.Seek(0, SeekOrigin.Begin);
            Bitmap image;
            if (!loadFullImage)
            {
                image = Bitmap.DecodeToWidth(buffer, SmallIconSize);
            }
            else
            {
                image = new Bitmap(buffer);
            }

            return image;
        }

        private static async ValueTask<ImageResult> GetThumbnailResult(string path, bool loadFullImage = false)
        {
            IImage image;
            ImageType type = ImageType.Error;

            if (Directory.Exists(path))
            {
                /* Directories can also have thumbnails instead of shell icons.
                 * Generating thumbnails for a bunch of folder results while scrolling
                 * could have a big impact on performance and Flow.Launcher responsibility.
                 * - Solution: just load the icon
                 */
                type = ImageType.Folder;
                image = GetThumbnail(path, ThumbnailOptions.IconOnly);
            }
            else if (File.Exists(path))
            {
                var extension = Path.GetExtension(path).ToLower();
                if (ImageExtensions.Contains(extension))
                {
                    type = ImageType.ImageFile;
                    if (loadFullImage)
                    {
                        image = new Bitmap(path);
                        type = ImageType.FullImageFile;
                    }
                    else
                    {
                        /* Although the documentation for GetImage on MSDN indicates that
                         * if a thumbnail is available it will return one, this has proved to not
                         * be the case in many situations while testing.
                         * - Solution: explicitly pass the ThumbnailOnly flag
                         */
                        image = await ImageHelper.LoadFromFile(path, SmallIconSize);
                    }
                }
                else if (extension == ".svg")
                {
                    var source = SvgSource.Load(path, null);
                    // very annoying, but we need to create the instance on the UI Thread. It is not an expensive operation though
                    image = Dispatcher.UIThread.Invoke(() => new SvgImage() { Source = source });
                    type = ImageType.FullImageFile;
                }
                else
                {
                    type = ImageType.File;
                    image = GetThumbnail(path, ThumbnailOptions.None, loadFullImage ? FullIconSize : SmallIconSize);
                }
            }
            else
            {
                image = ImageCache[Constant.MissingImgIcon, false];
                path = Constant.MissingImgIcon;
            }


            return new ImageResult(image, type);
        }

        private static Bitmap GetThumbnail(string path, ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly,
            int size = SmallIconSize)
        {
            return WindowsThumbnailProvider.GetThumbnail(
                path,
                size,
                size,
                option);
        }

        public static bool CacheContainImage(string path, bool loadFullImage = false)
        {
            return ImageCache.ContainsKey(path, loadFullImage);
        }

        public static bool TryGetValue(string path, bool loadFullImage, out IImage image)
        {
            return ImageCache.TryGetValue(path, loadFullImage, out image);
        }

        public static async ValueTask<IImage> LoadAsync(string path, bool loadFullImage = false)
        {
            var imageResult = await LoadInternalAsync(path, loadFullImage);

            var img = imageResult.Image;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            {
                // we need to get image hash
                // string hash = EnableImageHash ? _hashGenerator.GetHashFromImage(img) : null;
                // if (hash != null)
                // {
                //     if (GuidToKey.TryGetValue(hash, out string key))
                //     {
                //         // image already exists
                //         img = ImageCache[key, loadFullImage] ?? img;
                //     }
                //     else
                //     {
                //         // new guid
                //
                //         GuidToKey[hash] = path;
                //     }
                // }

                // update cache
                ImageCache[path, loadFullImage] = img;
            }

            return img;
        }
    }
}
