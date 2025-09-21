using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly string ClassName = nameof(ImageLoader);

        private static readonly ImageCache ImageCache = new();
        private static Lock storageLock { get; } = new();
        private static BinaryStorage<List<(string, bool)>> _storage;
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new();
        private static ImageHashGenerator _hashGenerator;
        private static readonly bool EnableImageHash = true;
        public static ImageSource Image => ImageCache[Constant.ImageIcon, false];
        public static ImageSource MissingImage => ImageCache[Constant.MissingImgIcon, false];
        public static ImageSource LoadingImage => ImageCache[Constant.LoadingImgIcon, false];
        public const int SmallIconSize = 64;
        public const int FullIconSize = 256;
        public const int FullImageSize = 320;

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico"];
        private static readonly string SvgExtension = ".svg";

        public static async Task InitializeAsync()
        {
            var usage = await Task.Run(() =>
            {
                _storage = new BinaryStorage<List<(string, bool)>>("Image");
                _hashGenerator = new ImageHashGenerator();

                var usage = LoadStorageToConcurrentDictionary();
                _storage.ClearData();

                ImageCache.Initialize(usage);

                foreach (var icon in new[] { Constant.DefaultIcon, Constant.ImageIcon, Constant.MissingImgIcon, Constant.LoadingImgIcon })
                {
                    ImageSource img = new BitmapImage(new Uri(icon));
                    img.Freeze();
                    ImageCache[icon, false] = img;
                }

                return usage;
            });

            _ = Task.Run(async () =>
            {
                await Stopwatch.InfoAsync(ClassName, "Preload images cost", async () =>
                {
                    foreach (var (path, isFullImage) in usage)
                    {
                        await LoadAsync(path, isFullImage);
                    }
                });
                Log.Info(ClassName, $"Number of preload images is <{ImageCache.CacheSize()}>, Images Number: {ImageCache.CacheSize()}, Unique Items {ImageCache.UniqueImagesInCache()}");
            });
        }

        public static void Save()
        {
            lock (storageLock)
            {
                try
                {
                    _storage.Save([.. ImageCache.EnumerateEntries().Select(x => x.Key)]);
                }
                catch (System.Exception e)
                {
                    Log.Exception(ClassName, "Failed to save image cache to file", e);
                }
            }
        }

        private static List<(string, bool)> LoadStorageToConcurrentDictionary()
        {
            lock (storageLock)
            {
                return _storage.TryLoad([]);
            }
        }

        private class ImageResult
        {
            public ImageResult(ImageSource imageSource, ImageType imageType)
            {
                ImageSource = imageSource;
                ImageType = imageType;
            }

            public ImageType ImageType { get; }
            public ImageSource ImageSource { get; }
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

                // extra scope for use of same variable name
                {
                    if (ImageCache.TryGetValue(path, loadFullImage, out var imageSource))
                    {
                        return new ImageResult(imageSource, ImageType.Cache);
                    }
                }

                if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    var image = await LoadRemoteImageAsync(loadFullImage, uriResult);
                    ImageCache[path, loadFullImage] = image;
                    return new ImageResult(image, ImageType.ImageFile);
                }

                if (path.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    var imageSource = new BitmapImage(new Uri(path));
                    imageSource.Freeze();
                    return new ImageResult(imageSource, ImageType.Data);
                }

                imageResult = await Task.Run(() => GetThumbnailResult(ref path, loadFullImage));
            }
            catch (System.Exception e)
            {
                try
                {
                    // Get thumbnail may fail for certain images on the first try, retry again has proven to work
                    imageResult = GetThumbnailResult(ref path, loadFullImage);
                }
                catch (System.Exception e2)
                {
                    Log.Exception(ClassName, $"Failed to get thumbnail for {path} on first try", e);
                    Log.Exception(ClassName, $"Failed to get thumbnail for {path} on second try", e2);

                    ImageSource image = MissingImage;
                    ImageCache[path, false] = image;
                    imageResult = new ImageResult(image, ImageType.Error);
                }
            }

            return imageResult;
        }

        private static async Task<BitmapImage> LoadRemoteImageAsync(bool loadFullImage, Uri uriResult)
        {
            // Download image from url
            await using var resp = await Http.Http.GetStreamAsync(uriResult);
            await using var buffer = new MemoryStream();
            await resp.CopyToAsync(buffer);
            buffer.Seek(0, SeekOrigin.Begin);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (!loadFullImage)
            {
                image.DecodePixelHeight = SmallIconSize;
                image.DecodePixelWidth = SmallIconSize;
            }

            image.StreamSource = buffer;
            image.EndInit();
            image.StreamSource = null;
            image.Freeze();
            return image;
        }

        private static ImageResult GetThumbnailResult(ref string path, bool loadFullImage = false)
        {
            ImageSource image;
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
                        try
                        {
                            image = LoadFullImage(path);
                            type = ImageType.FullImageFile;
                        }
                        catch (NotSupportedException ex)
                        {
                            image = Image;
                            type = ImageType.Error;
                            Log.Exception(ClassName, $"Failed to load image file from path {path}: {ex.Message}", ex);
                        }
                    }
                    else
                    {
                        /* Although the documentation for GetImage on MSDN indicates that
                         * if a thumbnail is available it will return one, this has proved to not
                         * be the case in many situations while testing.
                         * - Solution: explicitly pass the ThumbnailOnly flag
                         */
                        image = GetThumbnail(path, ThumbnailOptions.ThumbnailOnly);
                    }
                }
                else if (extension == SvgExtension)
                {
                    try
                    {
                        image = LoadSvgImage(path, loadFullImage);
                        type = ImageType.FullImageFile;
                    }
                    catch (System.Exception ex)
                    {
                        image = Image;
                        type = ImageType.Error;
                        Log.Exception(ClassName, $"Failed to load SVG image from path {path}: {ex.Message}", ex);
                    }
                }
                else
                {
                    type = ImageType.File;
                    image = GetThumbnail(path, ThumbnailOptions.None, loadFullImage ? FullIconSize : SmallIconSize);
                }
            }
            else
            {
                image = MissingImage;
                path = Constant.MissingImgIcon;
            }

            if (type != ImageType.Error)
            {
                image.Freeze();
            }

            return new ImageResult(image, type);
        }

        private static BitmapSource GetThumbnail(string path, ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly,
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

        public static bool TryGetValue(string path, bool loadFullImage, out ImageSource image)
        {
            return ImageCache.TryGetValue(path, loadFullImage, out image);
        }

        public static async ValueTask<ImageSource> LoadAsync(string path, bool loadFullImage = false, bool cacheImage = true)
        {
            var imageResult = await LoadInternalAsync(path, loadFullImage);

            var img = imageResult.ImageSource;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            {
                // we need to get image hash
                string hash = EnableImageHash ? _hashGenerator.GetHashFromImage(img) : null;
                if (hash != null)
                {
                    if (GuidToKey.TryGetValue(hash, out string key))
                    {
                        // image already exists
                        img = ImageCache[key, loadFullImage] ?? img;
                    }
                    else if (cacheImage)
                    {
                        // save guid key
                        GuidToKey[hash] = path;
                    }
                }

                if (cacheImage)
                {
                    // update cache
                    ImageCache[path, loadFullImage] = img;
                }
            }

            return img;
        }

        private static BitmapImage LoadFullImage(string path)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.EndInit();

            if (image.PixelWidth > FullImageSize)
            {
                BitmapImage resizedWidth = new BitmapImage();
                resizedWidth.BeginInit();
                resizedWidth.CacheOption = BitmapCacheOption.OnLoad;
                resizedWidth.UriSource = new Uri(path);
                resizedWidth.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                resizedWidth.DecodePixelWidth = FullImageSize;
                resizedWidth.EndInit();

                if (resizedWidth.PixelHeight > FullImageSize)
                {
                    BitmapImage resizedHeight = new BitmapImage();
                    resizedHeight.BeginInit();
                    resizedHeight.CacheOption = BitmapCacheOption.OnLoad;
                    resizedHeight.UriSource = new Uri(path);
                    resizedHeight.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    resizedHeight.DecodePixelHeight = FullImageSize;
                    resizedHeight.EndInit();
                    return resizedHeight;
                }

                return resizedWidth;
            }

            return image;
        }

        private static RenderTargetBitmap LoadSvgImage(string path, bool loadFullImage = false)
        {
            // Set up drawing settings
            var desiredHeight = loadFullImage ? FullImageSize : SmallIconSize;
            var drawingSettings = new WpfDrawingSettings
            {
                IncludeRuntime = true,
                // Set IgnoreRootViewbox to false to respect the SVG's viewBox
                IgnoreRootViewbox = false
            };

            // Load and render the SVG
            var converter = new FileSvgReader(drawingSettings);
            var drawing = converter.Read(new Uri(path));

            // Calculate scale to achieve desired height
            var drawingBounds = drawing.Bounds;
            if (drawingBounds.Height <= 0)
            {
                throw new InvalidOperationException($"Invalid SVG dimensions: Height must be greater than zero in {path}");
            }
            var scale = desiredHeight / drawingBounds.Height;
            var scaledWidth = drawingBounds.Width * scale;
            var scaledHeight = drawingBounds.Height * scale;

            // Convert the Drawing to a Bitmap
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.PushTransform(new ScaleTransform(scale, scale));
                drawingContext.DrawDrawing(drawing);
            }

            // Create a RenderTargetBitmap to hold the rendered image
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(scaledWidth),
                (int)Math.Ceiling(scaledHeight),
                96, // DpiX
                96, // DpiY
                PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);

            return bitmap;
        }
    }
}
