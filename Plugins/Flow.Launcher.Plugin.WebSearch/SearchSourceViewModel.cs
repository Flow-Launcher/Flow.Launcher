using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.IO;
using System.Windows;

namespace Flow.Launcher.Plugin.WebSearch
{
    public class SearchSourceViewModel : BaseModel
    {
        internal readonly string DestinationDirectory = 
            Path.Combine(DataLocation.DataDirectory(), @"Settings\Plugins\Flow.Launcher.Plugin.WebSearch\CustomIcons");

        public SearchSource SearchSource { get; set; }

        public void UpdateIconPath(SearchSource selectedSearchSource, string fullpathToSelectedImage)
        {
            var parentDirectorySelectedImg = Directory.GetParent(fullpathToSelectedImage).ToString();

            var iconPathDirectory = parentDirectorySelectedImg == DestinationDirectory
                                    || parentDirectorySelectedImg == Main.ImagesDirectory 
                                        ? parentDirectorySelectedImg : DestinationDirectory;

            var iconFileName = Path.GetFileName(fullpathToSelectedImage);
            selectedSearchSource.Icon = iconFileName;
            selectedSearchSource.IconPath = Path.Combine(iconPathDirectory, Path.GetFileName(fullpathToSelectedImage));
        }

        public void CopyNewImageToUserDataDirectoryIfRequired(
            SearchSource selectedSearchSource, string fullpathToSelectedImage, string fullPathToOriginalImage)
        {
            var destinationFileNameFullPath = Path.Combine(DestinationDirectory, Path.GetFileName(fullpathToSelectedImage));

            var parentDirectorySelectedImg = Directory.GetParent(fullpathToSelectedImage).ToString();

            if (parentDirectorySelectedImg != DestinationDirectory && parentDirectorySelectedImg != Main.ImagesDirectory)
            {
                try
                {
                    File.Copy(fullpathToSelectedImage, destinationFileNameFullPath);
                }
                catch (Exception e)
                {
#if DEBUG
                    throw e;
#else
                MessageBox.Show(string.Format("Copying the selected image file to {0} has failed, changes will now be reverted", destinationFileNameFullPath));
                UpdateIconPath(selectedSearchSource, fullPathToOriginalImage);
#endif
                }
            }

            selectedSearchSource.NotifyImageChange();
        }

        internal void SetupCustomImagesDirectory()
        {
            if (!Directory.Exists(DestinationDirectory))
                Directory.CreateDirectory(DestinationDirectory);
        }

        internal bool ShouldProvideHint(string fullPathToSelectedImage)
        {
            return Directory.GetParent(fullPathToSelectedImage).ToString() == Main.ImagesDirectory;
        }
    }
}