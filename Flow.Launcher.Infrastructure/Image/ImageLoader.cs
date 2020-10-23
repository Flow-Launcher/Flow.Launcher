using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly ImageCache ImageCache = new ImageCache();
        private static BinaryStorage<Dictionary<string, int>> _storage;
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new ConcurrentDictionary<string, string>();
        private static IImageHashGenerator _hashGenerator;
        private static bool EnableImageHash = true;

        private static readonly string[] ImageExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".tiff",
            ".ico"
        };

        public static void Initialize()
        {
            _storage = new BinaryStorage<Dictionary<string, int>>("Image");
            _hashGenerator = new ImageHashGenerator();

            ImageCache.Usage = LoadStorageToConcurrentDictionary();

            foreach (var icon in new[] { Constant.DefaultIcon, Constant.ErrorIcon })
            {
                ImageSource img = new BitmapImage(new Uri(icon));
                img.Freeze();
                ImageCache[icon] = img;
            }

            Task.Run(() =>
            {
                Stopwatch.Normal("|ImageLoader.Initialize|Preload images cost", () =>
                {
                    ImageCache.Usage.AsParallel().ForAll(x =>
                    {
                        Load(x.Key);
                    });
                });
                Log.Info($"|ImageLoader.Initialize|Number of preload images is <{ImageCache.Usage.Count}>, Images Number: {ImageCache.CacheSize()}, Unique Items {ImageCache.UniqueImagesInCache()}");
            });
        }

        public static void Save()
        {
            lock (_storage)
            {
                ImageCache.Cleanup();
                _storage.Save(new Dictionary<string, int>(ImageCache.Usage));
            }
        }

        private static ConcurrentDictionary<string, int> LoadStorageToConcurrentDictionary()
        {
            lock(_storage)
            {
                var loaded = _storage.TryLoad(new Dictionary<string, int>());

                return new ConcurrentDictionary<string, int>(loaded);
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
            Error,
            Cache
        }

        private static ImageResult LoadInternal(string path, bool loadFullImage = false)
        {
            ImageResult imageResult;

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return new ImageResult(ImageCache[Constant.ErrorIcon], ImageType.Error);
                }
                if (ImageCache.ContainsKey(path))
                {
                    return new ImageResult(ImageCache[path], ImageType.Cache);
                }

                if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var imageSource = new BitmapImage(new Uri(path));
                    imageSource.Freeze();
                    return new ImageResult(imageSource, ImageType.Data);
                }

                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(Constant.ProgramDirectory, "Images", Path.GetFileName(path));
                }

                imageResult = GetThumbnailResult(ref path, loadFullImage);
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
                    Log.Exception($"|ImageLoader.Load|Failed to get thumbnail for {path} on first try", e);
                    Log.Exception($"|ImageLoader.Load|Failed to get thumbnail for {path} on second try", e2);

                    ImageSource image = ImageCache[Constant.ErrorIcon];
                    ImageCache[path] = image;
                    imageResult = new ImageResult(image, ImageType.Error);
                }
            }

            return imageResult;
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
                        image = LoadFullImage(path);
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
                else
                {
                    type = ImageType.File;
                    image = GetThumbnail(path, ThumbnailOptions.None);
                }
            }
            else
            {
                image = ImageCache[Constant.ErrorIcon];
                path = Constant.ErrorIcon;
            }

            if (type != ImageType.Error)
            {
                image.Freeze();
            }

            return new ImageResult(image, type);
        }

        private static BitmapSource GetThumbnail(string path, ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly)
        {
            return WindowsThumbnailProvider.GetThumbnail(
                path,
                Constant.ThumbnailSize,
                Constant.ThumbnailSize,
                option);
        }

        public static ImageSource Load(string path, bool loadFullImage = false)
        {
            var imageResult = LoadInternal(path, loadFullImage);

            var img = imageResult.ImageSource;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            { // we need to get image hash
                string hash = EnableImageHash ? _hashGenerator.GetHashFromImage(img) : null;
                if (hash != null)
                {
                    if (GuidToKey.TryGetValue(hash, out string key))
                    { // image already exists
                        if (ImageCache.Usage.TryGetValue(path, out _))
                        {
                            img = ImageCache[key];
                        }
                    }
                    else
                    { // new guid
                        GuidToKey[hash] = path;
                    }
                }

                // update cache
                ImageCache[path] = img;
            }


            return img;
        }

        private static BitmapImage LoadFullImage(string path)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            return image;
        }
    }
}
