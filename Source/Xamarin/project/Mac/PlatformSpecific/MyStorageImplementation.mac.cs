using AppKit;
using Foundation;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using VTeamWidgets.Abstractions;
using Xamarin.Essentials;

namespace VTeamWidgets
{
    public class MyStorageImplementation_Apple : NSObject, IMyStorage
    {
        public bool FileExist(string pathAndFilenameToVerify)
        {
            return File.Exists(pathAndFilenameToVerify);
        }

        public long GetFileTime(string pathTofile)
        {
            return File.GetLastWriteTimeUtc(pathTofile).ToFileTimeUtc();
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
                Debug.WriteLine(ex.Message);
            }
            return false;
        }

        public Task<FileResult> PickFile(string[] allowedTypes)
        {
            // for consistency with other platforms, only allow selecting of a single file.
            // would be nice if we passed a "file options" to override picking multiple files & directories
            NSOpenPanel nSOpenPanel = new()
            {
                CanChooseFiles = true,
                AllowsMultipleSelection = false,
                CanChooseDirectories = false
            };
            var openPanel = nSOpenPanel;

            // macOS allows the file types to contain UTIs, filename extensions or a combination of the two.
            // If no types are specified, all files are selectable.
            if (allowedTypes != null)
            {
                openPanel.AllowedFileTypes = allowedTypes;
            }

            nint result = openPanel.RunModal();
            if (result == 1)
            {
                // Nab the first file
                NSUrl url = openPanel.Urls[0];

                if (url != null)
                {
                    var fr = new FileResult(url.Path);
                    fr.FileName = Path.GetFileName(fr.FullPath);

                    return Task.FromResult(fr);
                }
            }

            return null;
        }

        public void OpenFile(string fileToOpen)
        {
            try
            {
                if (!NSWorkspace.SharedWorkspace.OpenFile(fileToOpen))
                {
                    Debug.WriteLine($"Unable to open file at path: {fileToOpen}.");
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

        public Task<FileResult> PickFolder()
        {
            // for consistency with other platforms, only allow selecting of a single file.
            // would be nice if we passed a "file options" to override picking multiple files & directories
            NSOpenPanel nSOpenPanel1 = new()
            {
                CanChooseFiles = false,
                AllowsMultipleSelection = false,
                CanChooseDirectories = true
            };
            using (var nSOpenPanel = nSOpenPanel1)
            {
                NSOpenPanel openPanel = nSOpenPanel;


                nint result = openPanel.RunModal();
                if (result == 1)
                {
                    // Nab the first file
                    NSUrl url = openPanel.Urls[0];
                    return Task.FromResult(new FileResult(url.Path));
                }
            }

            return null;
        }
    }
}
