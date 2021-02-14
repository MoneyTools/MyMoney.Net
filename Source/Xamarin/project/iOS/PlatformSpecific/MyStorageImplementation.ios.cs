using Foundation;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIKit;
using VTeamWidgets.Abstractions;
using Xamarin.Essentials;

namespace VTeamWidgets
{
    /// <summary>
    /// Implementation for file picking on iOS
    /// </summary>
    public class MyStorageImplementation_Apple : NSObject, IMyStorage
    {
        /// <summary>
        /// Lets the user pick a file with the systems default file picker.
        /// For iOS iCloud drive needs to be configured.
        /// </summary>
        /// <param name="allowedTypes">
        /// Specifies one or multiple allowed types. When null, all file types
        /// can be selected while picking.
        /// On iOS you can specify UTType constants, e.g. UTType.Image.
        /// </param>
        /// <returns>
        /// File data object, or null when user cancelled picking file
        /// </returns>
        public async Task<FileResult> PickFile(string[] allowedTypes)
        {
            try
            {
                return await FilePicker.PickAsync();
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong                
                System.Diagnostics.Debug.Write(ex.Message);
            }

            return null;
        }

        public Task<FileResult> PickFolder()
        {
            return null;
        }


        public bool FileExist(string pathAndFilenameToVerify)
        {
            return File.Exists(pathAndFilenameToVerify);
        }

        public long GetFileTime(string pathTofile)
        {
            return FileExist(pathTofile) ? File.GetLastWriteTimeUtc(pathTofile).ToFileTimeUtc() : 0;
        }

        public bool CopyFile(string source, string destination)
        {
            try
            {
                File.Copy(source, destination);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return false;
        }

        /// <summary>
        /// iOS implementation of opening a file by using a UIDocumentInteractionController.
        /// </summary>
        /// <param name="fileUrl">file Url to open in viewer</param>
        public void OpenFile(NSUrl fileUrl)
        {
            UIDocumentInteractionController docController = UIDocumentInteractionController.FromUrl(fileUrl);

            UIWindow window = UIApplication.SharedApplication.KeyWindow;
            UIView[] subViews = window.Subviews;
            UIView lastView = subViews.Last();
            CoreGraphics.CGRect frame = lastView.Frame;
            _ = docController.PresentOpenInMenu(frame, lastView, true);
        }

        /// <summary>
        /// iOS implementation of OpenFile(), opening a file already stored on iOS "my documents"
        /// directory.
        /// </summary>
        /// <param name="fileToOpen">relative filename of file to open</param>
        public void OpenFile(string fileToOpen)
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string fileName = Path.Combine(documents, fileToOpen);

            if (NSFileManager.DefaultManager.FileExists(fileName))
            {
                var url = new NSUrl(fileName, true);
                this.OpenFile(url);
            }
        }

    }
}
