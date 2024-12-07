using Flow.Launcher.Infrastructure.Image;
using System;
using System.IO;
using System.Threading.Tasks;
#pragma warning disable IDE0005
using System.Windows;
#pragma warning restore IDE0005
using System.Windows.Media;

namespace Flow.Launcher.Plugin.WebSearch
{
    public class SearchSourceViewModel : BaseModel
    {
        public SearchSource SearchSource { get; set; }

        public void UpdateIconAttributes(SearchSource selectedSearchSource, string fullpathToSelectedImage)
        {
            var parentDirectorySelectedImg = Directory.GetParent(fullpathToSelectedImage).ToString();

            selectedSearchSource.CustomIcon = parentDirectorySelectedImg != Main.DefaultImagesDirectory;

            var iconFileName = Path.GetFileName(fullpathToSelectedImage);
            selectedSearchSource.Icon = iconFileName;
        }

        public void CopyNewImageToUserDataDirectoryIfRequired(
            SearchSource selectedSearchSource, string fullpathToSelectedImage, string fullPathToOriginalImage)
        {
            var destinationFileNameFullPath = Path.Combine(Main.CustomImagesDirectory, Path.GetFileName(fullpathToSelectedImage));

            var parentDirectorySelectedImg = Directory.GetParent(fullpathToSelectedImage).ToString();

            if (parentDirectorySelectedImg != Main.CustomImagesDirectory && parentDirectorySelectedImg != Main.DefaultImagesDirectory)
            {
                try
                {
                    File.Copy(fullpathToSelectedImage, destinationFileNameFullPath);
                }
                catch (Exception)
                {
#if DEBUG
                    throw;
#else
                Main._context.API.ShowMsgBox(string.Format("Copying the selected image file to {0} has failed, changes will now be reverted", destinationFileNameFullPath));
                UpdateIconAttributes(selectedSearchSource, fullPathToOriginalImage);
#endif
                }
            }
        }

        internal void SetupCustomImagesDirectory()
        {
            if (!Directory.Exists(Main.CustomImagesDirectory))
                Directory.CreateDirectory(Main.CustomImagesDirectory);
        }

        internal bool ShouldProvideHint(string fullPathToSelectedImage)
        {
            return Directory.GetParent(fullPathToSelectedImage).ToString() == Main.DefaultImagesDirectory;
        }

        internal async ValueTask<ImageSource> LoadPreviewIconAsync(string pathToPreviewIconImage)
        {
            return await ImageLoader.LoadAsync(pathToPreviewIconImage);
        }
    }
}
