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
        public ConcurrentDictionary<string, int> Usage = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, ImageSource> _data = new ConcurrentDictionary<string, ImageSource>();
        private const int permissibleFactor = 2;

        public ImageSource this[string path]
        {
            get
            {
                Usage.AddOrUpdate(path, 1, (k, v) => v + 1);
                var i = _data[path];
                return i;
            }
            set
            {
                _data[path] = value;

                // To prevent the dictionary from drastically increasing in size by caching images, the dictionary size is not allowed to grow more than the permissibleFactor * maxCached size
                // This is done so that we don't constantly perform this resizing operation and also maintain the image cache size at the same time
                if (_data.Count > permissibleFactor * MaxCached)
                {
                    // This function resizes the Usage dictionary, taking the top 'maxCached' number of items and filtering the image icons that are not accessed frequently.
                    Cleanup();

                    // To delete the images from the data dictionary based on the resizing of the Usage Dictionary.
                    foreach (var key in _data.Keys)
                    {
                        int dictValue;
                        if (!Usage.TryGetValue(key, out dictValue) && !(key.Equals(Constant.ErrorIcon) || key.Equals(Constant.DefaultIcon)))
                        {
                            ImageSource imgSource;
                            _data.TryRemove(key, out imgSource);
                        }
                    }
                }
            }
        }

        public void Cleanup()
        {
            var images = Usage
                .OrderByDescending(o => o.Value)
                .Take(MaxCached)
                .ToDictionary(i => i.Key, i => i.Value);
            Usage = new ConcurrentDictionary<string, int>(images);
        }

        public bool ContainsKey(string key)
        {
            var contains = _data.ContainsKey(key);
            return contains;
        }

        public int CacheSize()
        {
            return _data.Count;
        }

        /// <summary>
        /// return the number of unique images in the cache (by reference not by checking images content)
        /// </summary>
        public int UniqueImagesInCache()
        {
            return _data.Values.Distinct().Count();
        }
    }

}