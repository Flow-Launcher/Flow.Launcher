using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using FastCache;
using FastCache.Services;

namespace Flow.Launcher.Infrastructure.Image
{
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
        private const int MaxCached = 150;

        public void Initialize(Dictionary<(string, bool), int> usage)
        {
            foreach (var key in usage.Keys)
            {
                Cached<ImageUsage>.Save(key, new ImageUsage(usage[key], null), TimeSpan.MaxValue, MaxCached);
            }
        }

        public ImageSource this[string path, bool isFullImage = false]
        {
            get
            {
                if (!Cached<ImageUsage>.TryGet((path, isFullImage), out var value))
                {
                    return null;
                }

                value.Value.usage++;
                return value.Value.imageSource;
            }
            set
            {
                if (Cached<ImageUsage>.TryGet((path, isFullImage), out var cached))
                {
                    cached.Value.imageSource = value;
                    cached.Value.usage++;
                }

                Cached<ImageUsage>.Save((path, isFullImage), new ImageUsage(0, value), TimeSpan.MaxValue,
                    MaxCached);
            }
        }

        public bool ContainsKey(string key, bool isFullImage)
        {
            return Cached<ImageUsage>.TryGet((key, isFullImage), out _);
        }

        public bool TryGetValue(string key, bool isFullImage, out ImageSource image)
        {
            if (Cached<ImageUsage>.TryGet((key, isFullImage), out var value))
            {
                image = value.Value.imageSource;
                value.Value.usage++;
                return image != null;
            }

            image = null;
            return false;
        }

        public int CacheSize()
        {
            return CacheManager.TotalCount<(string, bool), ImageUsage>();
        }

        /// <summary>
        /// return the number of unique images in the cache (by reference not by checking images content)
        /// </summary>
        public int UniqueImagesInCache()
        {
            return CacheManager.EnumerateEntries<(string, bool), ImageUsage>().Select(x => x.Value.imageSource)
                .Distinct()
                .Count();
        }

        public IEnumerable<Cached<(string, bool), ImageUsage>> EnumerateEntries()
        {
            return CacheManager.EnumerateEntries<(string, bool), ImageUsage>();
        }
    }
}
