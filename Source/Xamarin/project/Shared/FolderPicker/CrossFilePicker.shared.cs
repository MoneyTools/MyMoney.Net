using System;
using System.IO;
using VTeamWidgets.Abstractions;

namespace VTeamWidgets
{
    /// <summary>
    /// Cross-platform FilePicker implementation
    /// </summary>
    public static class MyXPlatform
    {

        /// <summary>
        /// Current file picker plugin implementation to use
        /// </summary>
        public static IMyStorage Current { get; set; } = null;

        public static bool CopyFile(string source, string destination)
        {
            try
            {
                Current.CopyFile(source, destination);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return false;
        }

        public static bool MakeBackupCopy(string pathToFile)
        {
            // free up the obseleted working file
            string newBackupFiles = pathToFile + " backup " + DateTime.Now.ToFileTime();
            try
            {
                File.Move(pathToFile, newBackupFiles);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return false;
        }
    }
}