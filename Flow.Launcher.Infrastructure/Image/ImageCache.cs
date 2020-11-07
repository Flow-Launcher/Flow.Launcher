using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Flow.Launcher.Infrastructure.Image
{
    [Serializable]
    public class ImageCache
    {
        private const int MaxCached = 50;
        public ConcurrentDictionary<string, (int usage, ImageSource imageSource)> Data { get; private set; } = new ConcurrentDictionary<string, (int, ImageSource)>();
        private const int permissibleFactor = 2;

        public void Initialization(Dictionary<string, int> usage)
        {
            foreach (var key in usage.Keys)
            {
                Data[key] = (usage[key], null);
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
                else return null;
            }
            set
            {
                Data.AddOrUpdate(path, (1, value), (k, v) =>
                {
                    v.imageSource = value;
                    v.usage++;
                    return v;
                });

                // To prevent the dictionary from drastically increasing in size by caching images, the dictionary size is not allowed to grow more than the permissibleFactor * maxCached size
                // This is done so that we don't constantly perform this resizing operation and also maintain the image cache size at the same time
                if (Data.Count > permissibleFactor * MaxCached)
                {
                    // To delete the images from the data dictionary based on the resizing of the Usage Dictionary.

                    
                    foreach (var key in Data.OrderBy(x => x.Value.usage).Take(Data.Count - MaxCached).Select(x => x.Key))
                    {
                        if (!(key.Equals(Constant.ErrorIcon) || key.Equals(Constant.DefaultIcon)))
                        {
                            Data.TryRemove(key, out _);
                        }
                    }
                }
            }
        }


        public bool ContainsKey(string key)
        {
            var contains = Data.ContainsKey(key);
            return contains;
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