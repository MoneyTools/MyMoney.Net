using System;
using System.IO;
using System.Threading.Tasks;
using VTeamWidgets.Abstractions;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Xamarin.Essentials;

namespace VTeamWidgets
{
    /// <summary>
    /// Implementation for file picking on UWP
    /// </summary>
    public class MyStorageImplementation_UWP : IMyStorage
    {
        public bool FileExist(string fileToTest)
        {
            try
            {
                var file = StorageApplicationPermissions.FutureAccessList.GetFileAsync("lastSourceFile").GetAwaiter().GetResult();

                return file.IsAvailable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return false;

            //    CopyAsync(ApplicationData.Current.LocalFolder);
            //Windows.Storage.FileIO.
        }


        public long GetFileTime(string pathToFile)
        {
            var file = StorageApplicationPermissions.FutureAccessList.GetFileAsync("lastSourceFile").GetAwaiter().GetResult();

            // Get file's basic properties.
            Windows.Storage.FileProperties.BasicProperties basicProperties = file.GetBasicPropertiesAsync().GetAwaiter().GetResult();
            var tmpDT = basicProperties.DateModified.UtcDateTime;

            System.Diagnostics.Debug.WriteLine(tmpDT.ToString());

            return tmpDT.ToFileTimeUtc();
        }


        public bool CopyFile(string source, string destination)
        {
            try
            {
                var file = StorageApplicationPermissions.FutureAccessList.GetFileAsync("lastSourceFile").GetAwaiter().GetResult();

                string destinationPathOnly = Path.GetDirectoryName(destination);

                var destinationStorage = StorageFolder.GetFolderFromPathAsync(destinationPathOnly).GetAwaiter().GetResult();
                file.CopyAsync(destinationStorage).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return false;
        }



        /// <summary>
        /// Implementation for picking a file on UWP platform.
        /// </summary>
        /// <param name="allowedTypes">
        /// Specifies one or multiple allowed types. When null, all file types
        /// can be selected while picking.
        /// On UWP, specify a list of extensions, like this: ".jpg", ".png".
        /// </param>
        /// <returns>
        /// File data object, or null when user cancelled picking file
        /// </returns>
        public async Task<FileResult> PickFile(string[] allowedTypes)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };

            if (allowedTypes != null)
            {
                var hasAtleastOneType = false;

                foreach (var type in allowedTypes)
                {
                    if (type.StartsWith("."))
                    {
                        picker.FileTypeFilter.Add(type);
                        hasAtleastOneType = true;
                    }
                }

                if (!hasAtleastOneType)
                {
                    picker.FileTypeFilter.Add("*");
                }
            }
            else
            {
                picker.FileTypeFilter.Add("*");
            }

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return null;
            }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("lastSourceFile", file);

            var fr = new FileResult(file.Path)
            {
                FileName = file.Path
            };

            return fr;
        }

        /// <summary>
        /// UWP implementation of OpenFile(), opening a file already stored in the app's local
        /// folder directory.
        /// storage.
        /// </summary>
        /// <param name="fileToOpen">relative filename of file to open</param>
        public async void OpenFile(string fileToOpen)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileToOpen);

                if (file != null)
                {
                    await Xamarin.Essentials.Launcher.OpenAsync(fileToOpen);
                }
            }
            catch (FileNotFoundException)
            {
                // ignore exceptions
            }
            catch (Exception)
            {
                // ignore exceptions
            }
        }


        /// <summary>
        /// Implementation for picking a file on UWP platform.
        /// </summary>
        /// <param name="allowedTypes">
        /// Specifies one or multiple allowed types. When null, all file types
        /// can be selected while picking.
        /// On UWP, specify a list of extensions, like this: ".jpg", ".png".
        /// </param>
        /// <returns>
        /// File data object, or null when user cancelled picking file
        /// </returns>
        public async Task<FileResult> PickFolder()
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker
            {
                //{
                //    ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                //    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                //};
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            folderPicker.FileTypeFilter.Add("*");

            Windows.Storage.StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
            {
                return null;
            }

            StorageApplicationPermissions.FutureAccessList.Add(folder);
            return new FileResult(folder.Path);
        }

    }
}
