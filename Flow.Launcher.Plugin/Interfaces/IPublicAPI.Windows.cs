using System.Threading.Tasks;
using System.Windows.Media;

namespace Flow.Launcher.Plugin
{
    public partial interface IPublicAPI
    {
        /// <summary>
        /// Load image from path.
        /// Support local, remote and data:image url.
        /// Support png, jpg, jpeg, gif, bmp, tiff, ico, svg image files.
        /// If image path is missing, it will return a missing icon.
        /// </summary>
        /// <param name="path">The path of the image.</param>
        /// <param name="loadFullImage">
        /// Load full image or not.
        /// </param>
        /// <param name="cacheImage">
        /// Cache the image or not. Cached image will be stored in FL cache.
        /// If the image is just used one time, it's better to set this to false.
        /// </param>
        /// <returns></returns>
        ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false, bool cacheImage = true);
    }
}
