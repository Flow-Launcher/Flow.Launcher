using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public ConcurrentDictionary<string, ImageUsage> Data { get; private set; } = new ConcurrentDictionary<string, ImageUsage>();
        private const int permissibleFactor = 2;
        private SemaphoreSlim semaphore = new(1, 1);

        public void Initialization(Dictionary<string, int> usage)
        {
            foreach (var key in usage.Keys)
            {
                Data[key] = new ImageUsage(usage[key], null);
            }
        }

        public ImageSource this[string path]
        {
            get
            {
                if (Data.TryGetValue(path, out var value))
                {
                    value.usage++;
                    return value.imageSource;
                }

                return null;
            }
            set
            {
                Data.AddOrUpdate(
                        path,
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
                        await semaphore.WaitAsync();
                        // To delete the images from the data dictionary based on the resizing of the Usage Dictionary
                        // Double Check to avoid concurrent remove
                        if (Data.Count > permissibleFactor * MaxCached)
                            foreach (var key in Data.OrderBy(x => x.Value.usage).Take(Data.Count - MaxCached).Select(x => x.Key).ToArray())
                                Data.TryRemove(key, out _);
                        semaphore.Release();
                    }
                }
            }
        }

        public bool ContainsKey(string key)
        {
            return Data.ContainsKey(key) && Data[key].imageSource != null;
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