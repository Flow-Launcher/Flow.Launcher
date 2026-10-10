using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
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
        public static ImageSource MissingImage => ImageCache[Constant.MissingImgIcon, false];
        public static ImageSource LoadingImage => ImageCache[Constant.LoadingImgIcon, false];
        public static ImageSource FolderImage => ImageCache[Constant.FolderIcon, false];
        public const int SmallIconSize = 64;
        public const int FullIconSize = 256;
        public const int FullImageSize = 320;

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico"];
        private static readonly string SvgExtension = ".svg";
        internal static Func<string, ThumbnailOptions, int, BitmapSource> ShellThumbnailLoader { get; set; } =
            (path, option, size) =>
                WindowsThumbnailProvider.GetThumbnail(
                    path,
                    size,
                    size,
                    option);

        public static async Task InitializeAsync()
        {
            var usage = await Task.Run(() =>
            {
                _storage = new BinaryStorage<List<(string, bool)>>("Image");
                _hashGenerator = new ImageHashGenerator();

                var usage = LoadStorageToConcurrentDictionary();
                _storage.ClearData();

                ImageCache.Initialize(usage);

                foreach (var icon in new[] { Constant.DefaultIcon, Constant.MissingImgIcon, Constant.LoadingImgIcon, Constant.FolderIcon })
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

                imageResult = await Task.Run(() => GetThumbnailResult(path, loadFullImage));
            }
            catch (System.Exception e)
            {
                try
                {
                    // Get thumbnail may fail for certain images on the first try, retry again has proven to work
                    imageResult = GetThumbnailResult(path, loadFullImage);
                }
                catch (System.Exception e2)
                {
                    Log.Warn(ClassName, $"Failed to get thumbnail for {path} on first try: {e.Message}");
                    Log.Warn(ClassName, $"Failed to get thumbnail for {path} on second try: {e2.Message}");

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

        private static ImageResult GetThumbnailResult(string path, bool loadFullImage = false)
        {
            if (Directory.Exists(path))
                return GetDirectoryThumbnailResult(path, loadFullImage);

            if (!File.Exists(path))
                return GetMissingThumbnailResult();

            var extension = Path.GetExtension(path).ToLower();

            if (ImageExtensions.Contains(extension))
                return GetImageFileThumbnailResult(path, loadFullImage);

            if (extension == SvgExtension)
                return GetSvgFileThumbnailResult(path, loadFullImage);

            return GetFileThumbnailResult(path, loadFullImage);
        }

        private static ImageResult CreateImageResult(ImageSource image, ImageType type)
        {
            if (type != ImageType.Error && !image.IsFrozen)
            {
                image.Freeze();
            }

            return new ImageResult(image, type);
        }

        private static ImageResult GetMissingThumbnailResult()
        {
            return CreateImageResult(MissingImage, ImageType.Error);
        }

        private static ImageResult GetDirectoryThumbnailResult(string path, bool loadFullImage)
        {
            var size = loadFullImage ? FullIconSize : SmallIconSize;
            try
            {
                /* Directories can also have thumbnails instead of shell icons.
                 * Generating thumbnails for a bunch of folder results while scrolling
                 * could have a big impact on performance and Flow.Launcher responsibility.
                 * - Solution: just load the icon
                 */
                var image = GetShellThumbnail(path, ThumbnailOptions.IconOnly, size);
                return CreateImageResult(image, ImageType.Folder);
            }
            catch (System.Exception ex)
            {
                Log.Warn(ClassName, $"Failed to get shell thumbnail for folder {path}: {ex.Message}\nUsing default folder image as fallback.");
                return CreateImageResult(FolderImage, ImageType.Folder);
            }
        }

        private static ImageResult GetImageFileThumbnailResult(string path, bool loadFullImage)
        {
            if (loadFullImage)
            {
                try
                {
                    var image = LoadBitmapImageScaleToFitWithin(path, FullImageSize);
                    return CreateImageResult(image, ImageType.FullImageFile);
                }
                catch (NotSupportedException ex)
                {
                    Log.Warn(ClassName, $"Failed to load image file from path {path}: {ex.Message}\nUsing missing icon instead.");
                    return GetMissingThumbnailResult();
                }
            }

            try
            {
                /* Although the documentation for GetImage on MSDN indicates that
                 * if a thumbnail is available it will return one, this has proved to not
                 * be the case in many situations while testing.
                 * - Solution: explicitly pass the ThumbnailOnly flag
                 */
                var image = GetShellThumbnail(path, ThumbnailOptions.ThumbnailOnly);
                return CreateImageResult(image, ImageType.ImageFile);
            }
            catch (System.Exception ex)
            {
                Log.Debug(ClassName, $"Failed to get shell thumbnail for image file {path}: {ex.Message}\nTrying bitmap fallback.");

                try
                {
                    var image = LoadBitmapImageScaleToFitWithin(path, SmallIconSize);
                    return CreateImageResult(image, ImageType.ImageFile);
                }
                catch (System.Exception ex2)
                {
                    Log.Warn(ClassName, $"Failed to load image file from path {path}: {ex2.Message}\nUsing missing icon instead.");
                    return GetMissingThumbnailResult();
                }
            }
        }

        private static ImageResult GetSvgFileThumbnailResult(string path, bool loadFullImage)
        {
            try
            {
                var image = LoadSvgImage(path, loadFullImage);
                return CreateImageResult(image, ImageType.FullImageFile);
            }
            catch (System.Exception ex)
            {
                Log.Warn(ClassName, $"Failed to load SVG image from path {path}: {ex.Message}\nUsing missing icon instead.");
                return GetMissingThumbnailResult();
            }
        }

        private static ImageResult GetFileThumbnailResult(string path, bool loadFullImage)
        {
            var size = loadFullImage ? FullIconSize : SmallIconSize;
            try
            {
                var image = GetShellThumbnail(path, ThumbnailOptions.None, size);
                return CreateImageResult(image, ImageType.File);
            }
            catch (System.Exception ex)
            {
                Log.Debug(ClassName, $"Failed to get shell thumbnail for {path}: {ex.Message}\nTrying ExtractAssociatedIcon fallback.");

                if (TryExtractAssociatedIcon(path, size, out var image))
                {
                    return CreateImageResult(image, ImageType.File);
                }

                Log.Warn(ClassName, $"ExtractAssociatedIcon returned no icon for path: {path}\nUsing missing icon instead.");
                return GetMissingThumbnailResult();
            }
        }

        private static BitmapSource GetShellThumbnail(string path, ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly,
            int size = SmallIconSize)
        {
            return ShellThumbnailLoader(path, option, size);
        }

        private static bool TryExtractAssociatedIcon(string path, int size, out BitmapSource image)
        {
            image = null;

            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null)
                {
                    return false;
                }

                image = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(size, size));
                image.Freeze();
                return true;
            }
            catch
            {
                image = null;
                return false;
            }
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

        private static bool TryGetBitmapImageDimensionsFromMetadata(string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            try
            {
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.None);

                var frame = decoder.Frames.FirstOrDefault();
                if (frame is null)
                    return false;

                width = frame.PixelWidth;
                height = frame.PixelHeight;
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static BitmapImage LoadBitmapImageScaleToFitWithin(string path, int maxSize)
        {
            BitmapImage decodedImage = null;

            // try to get the image's dimensions from metadata before fully decoding the image
            bool metadataReadSucceeded = TryGetBitmapImageDimensionsFromMetadata(path, out var width, out var height);

            // if we couldn't read the metadata then fully load the image and get dimensions from that
            if (!metadataReadSucceeded)
            {
                decodedImage = LoadBitmapImage(path);
                width = decodedImage.PixelWidth;
                height = decodedImage.PixelHeight;
            }

            // If resizing is unnecessary, return the original image
            // (reusing the already decoded image if available).
            if (width <= maxSize && height <= maxSize)
            {
                return decodedImage ?? LoadBitmapImage(path);
            }

            bool widthIsLarger = width >= height;

            // LoadBitmapImage will maintain aspect ratio so we only need to scale by the largest dimension
            if (widthIsLarger)
            {
                return LoadBitmapImage(path, decodePixelWidth: maxSize);
            }
            else
            {
                return LoadBitmapImage(path, decodePixelHeight: maxSize);
            }
        }

        private static BitmapImage LoadBitmapImage(string path, int? decodePixelWidth = null, int? decodePixelHeight = null)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            if (decodePixelWidth.HasValue)
            {
                image.DecodePixelWidth = decodePixelWidth.Value;
            }

            if (decodePixelHeight.HasValue)
            {
                image.DecodePixelHeight = decodePixelHeight.Value;
            }

            image.EndInit();
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
