using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using BitFaster.Caching.Lfu;

namespace Flow.Launcher.Infrastructure.Image
{
    public class ImageCache
    {
        private const int MaxCached = 150;

        private ConcurrentLfu<(string, bool), ImageSource> CacheManager { get; set; } = new(MaxCached);

        public void Initialize(IEnumerable<(string, bool)> usage)
        {
            foreach (var key in usage)
            {
                CacheManager.AddOrUpdate(key, null);
            }
        }

        public ImageSource this[string path, bool isFullImage = false]
        {
            get
            {
                return CacheManager.TryGet((path, isFullImage), out var value) ? value : null;
            }
            set
            {
                CacheManager.AddOrUpdate((path, isFullImage), value);
            }
        }

        public async ValueTask<ImageSource> GetOrAddAsync(string key,
            Func<(string, bool), Task<ImageSource>> valueFactory,
            bool isFullImage = false)
        {
            return await CacheManager.GetOrAddAsync((key, isFullImage), valueFactory);
        }

        public bool ContainsKey(string key, bool isFullImage)
        {
            return CacheManager.TryGet((key, isFullImage), out _);
        }

        public bool TryGetValue(string key, bool isFullImage, out ImageSource image)
        {
            if (CacheManager.TryGet((key, isFullImage), out var value))
            {
                image = value;
                return image != null;
            }


            image = null;
            return false;
        }

        public int CacheSize()
        {
            return CacheManager.Count;
        }

        /// <summary>
        /// return the number of unique images in the cache (by reference not by checking images content)
        /// </summary>
        public int UniqueImagesInCache()
        {
            return CacheManager.Select(x => x.Value)
                .Distinct()
                .Count();
        }

        public IEnumerable<KeyValuePair<(string, bool), ImageSource>> EnumerateEntries()
        {
            return CacheManager;
        }
    }
}
