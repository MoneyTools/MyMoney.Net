using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace Walkabout.Utilities
{
    /// <summary>
    /// This class takes care of cleaning up temp files, and if files are locked even during process
    /// termination - it saves a list of those files so the app can clean them up on next launch.
    /// </summary>
    public static class TempFilesManager
    {
        public static string TempFileList
        {
            get
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMoney");
                return System.IO.Path.Combine(folder, "tempfiles.xml");
            }
        }


        /// <summary>
        /// Resilient file delete 
        /// </summary>
        /// <param name="fileToDelete"></param>
        public static void DeleteFile(string fileToDelete)
        {
            try
            {
                if (File.Exists(fileToDelete))
                {
                    if (File.GetAttributes(fileToDelete) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(fileToDelete, FileAttributes.Normal);
                    }
                    File.Delete(fileToDelete);
                }
            }
            catch (Exception)
            {
                AddTempFile(fileToDelete);
            }
        }


        static List<string> files = new List<string>();

        public static void AddTempFile(string file)
        {
            files.Add(file);
        }

        public static bool HasTempFiles
        {
            get { return files.Count > 0; }
        }

        /// <summary>
        /// Remove the given file name from the list of temp files to cleanup.
        /// Call this is the file is no longer temporary.
        /// </summary>
        /// <param name="newFileName">The file name to remove from the list</param>
        internal static void RemoveTempFile(string newFileName)
        {
            lock (files)
            {
                foreach (string path in files)
                {
                    if (string.Compare(path, newFileName, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        files.Remove(path);
                        return;
                    }
                }
            }
        }

        public static void Cleanup()
        {            
            foreach (string file in files.ToArray())
            {
                files.Remove(file);
                DeleteFile(file);
            }
        }

        public static void Shutdown()
        {
            XDocument doc = new XDocument(new XElement("Files"));
            foreach (string file in files.ToArray())
            {
                doc.Root.Add(new XElement("File", new XAttribute("Name", file)));
            }
            doc.Save(TempFileList);
        }

        internal static void Initialize()
        {
            string fname = TempFileList;
            if (File.Exists(fname))
            {
                XDocument doc = XDocument.Load(fname);
                foreach (XElement file in doc.Root.Elements("File"))
                {
                    string name = (string)file.Attribute("Name");
                    if (!string.IsNullOrEmpty(name) && File.Exists(name))
                    {
                        files.Add(name);
                    }
                }
            }
            // now try and delete them!
            Cleanup();
        }

    }
}
