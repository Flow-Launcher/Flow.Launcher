using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.IO;
using System.Windows.Forms;

namespace Flow.Launcher.Plugin.WebSearch
{
    public class SearchSourceViewModel : BaseModel
    {
        private readonly string destinationDirectory = 
            Path.Combine(DataLocation.DataDirectory(), @"Settings\Plugins\Flow.Launcher.Plugin.WebSearch\IconImages");

        public SearchSource SearchSource { get; set; }

        public void UpdateIconPath(SearchSource selectedSearchSource, string fullpathToSelectedImage)
        {
            var iconFileName = Path.GetFileName(fullpathToSelectedImage);
            selectedSearchSource.Icon = iconFileName;
            selectedSearchSource.IconPath = Path.Combine(destinationDirectory, Path.GetFileName(fullpathToSelectedImage));
        }

        public void CopyNewImageToUserDataDirectory(SearchSource selectedSearchSource, string fullpathToSelectedImage, string fullPathToOriginalImage)
        {
            var destinationFileNameFullPath = Path.Combine(destinationDirectory, Path.GetFileName(fullpathToSelectedImage));

            try
            {
                if (!Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Copy(fullpathToSelectedImage, destinationFileNameFullPath);

                selectedSearchSource.NotifyImageChange();
            }
            catch(Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Copying the selected image file to {0} has failed, changes will now be reverted", destinationFileNameFullPath));
                UpdateIconPath(selectedSearchSource, fullPathToOriginalImage);
#endif
            }

        }

        public bool ImageFileExistsInLocation(string fullpathToSelectedImage)
        {
            var fileName = Path.GetFileName(fullpathToSelectedImage);

            var newImageFilePathToBe = Path.Combine(destinationDirectory, fileName);

            return File.Exists(newImageFilePathToBe);
        }
    }
}