using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Media;

namespace Flow.Launcher.Infrastructure.Image
{
    [Serializable]
    public class ImageUsage
    {

        public int usage;
        public ImageSource imageSource;

        public ImageUsage(int usage, ImageSource image)
        {
            this.usage = usage;
            imageSource = image;
        }
    }

    public class ImageCache
    {
        private const int MaxCached = 50;
        public ConcurrentDictionary<(string, bool), ImageUsage> Data { get; } = new();
        private const int permissibleFactor = 2;
        private SemaphoreSlim semaphore = new(1, 1);

        public void Initialization(Dictionary<(string, bool), int> usage)
        {
            foreach (var key in usage.Keys)
            {
                Data[key] = new ImageUsage(usage[key], null);
            }
        }

        public ImageSource this[string path, bool isFullImage = false]
        {
            get
            {
                if (!Data.TryGetValue((path, isFullImage), out var value))
                {
                    return null;
                }
                value.usage++;
                return value.imageSource;

            }
            set
            {
                Data.AddOrUpdate(
                    (path, isFullImage),
                    new ImageUsage(0, value),
                    (k, v) =>
                    {
                        v.imageSource = value;
                        v.usage++;
                        return v;
                    }
                );

                SliceExtra();

                async void SliceExtra()
                {
                    // To prevent the dictionary from drastically increasing in size by caching images, the dictionary size is not allowed to grow more than the permissibleFactor * maxCached size
                    // This is done so that we don't constantly perform this resizing operation and also maintain the image cache size at the same time
                    if (Data.Count > permissibleFactor * MaxCached)
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        // To delete the images from the data dictionary based on the resizing of the Usage Dictionary
                        // Double Check to avoid concurrent remove
                        if (Data.Count > permissibleFactor * MaxCached)
                            foreach (var key in Data.OrderBy(x => x.Value.usage).Take(Data.Count - MaxCached).Select(x => x.Key))
                                Data.TryRemove(key, out _);
                        semaphore.Release();
                    }
                }
            }
        }

        public bool ContainsKey(string key, bool isFullImage)
        {
            return key is not null && Data.ContainsKey((key, isFullImage)) && Data[(key, isFullImage)].imageSource != null;
        }

        public bool TryGetValue(string key, bool isFullImage, out ImageSource image)
        {
            if (key is not null)
            {
                bool hasKey = Data.TryGetValue((key, isFullImage), out var imageUsage);
                image = hasKey ? imageUsage.imageSource : null;
                return hasKey;
            }
            else
            {
                image = null;
                return false;
            }
        }

        public int CacheSize()
        {
            return Data.Count;
        }

        /// <summary>
        /// return the number of unique images in the cache (by reference not by checking images content)
        /// </summary>
        public int UniqueImagesInCache()
        {
            return Data.Values.Select(x => x.imageSource).Distinct().Count();
        }
    }
}
