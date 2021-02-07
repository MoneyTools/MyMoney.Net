using Android.Runtime;
using System;
using System.Threading.Tasks;
using VTeamWidgets.Abstractions;
using Xamarin.Essentials;

namespace VTeamWidgets
{
    [Preserve(AllMembers = true)]
    public class MyStorageImplementation_Android : IMyStorage
    {
        /// <summary>
        /// Implementation for Feature
        /// </summary>
        ///

        public MyStorageImplementation_Android()
        {
        }

        public bool FileExist(string pathAndFilenameToVerify)
        {
            return System.IO.File.Exists(pathAndFilenameToVerify);
        }

        public long GetFileTime(string pathTofile)
        {
            return System.IO.File.GetLastWriteTimeUtc(pathTofile).ToFileTimeUtc();
        }

        public bool CopyFile(string source, string destination)
        {
            try
            {
                System.IO.File.Copy(source, destination);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return false;
        }

        public Task<FileResult> PickFolder()
        {
            return null;
        }

        public async Task<FileResult> PickFile(string[] allowedTypes = null)
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
    }
}