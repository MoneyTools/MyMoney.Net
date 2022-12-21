using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Walkabout.Utilities
{
    /// <summary>
    /// This class takes care of cleaning up temp files, and if files are locked even during process
    /// termination - it saves a list of those files so the app can clean them up on next launch.
    /// </summary>
    public class TempFilesManager
    {
        private readonly DelayedActions actions = new DelayedActions();
        private static readonly TempFilesManager Instance = new TempFilesManager();

        public TempFilesManager()
        {
        }

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
            Instance.TryDeleteFile(fileToDelete);
        }

        private void TryDeleteFile(string fileToDelete)
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
                this.Add(fileToDelete);
            }
        }

        private readonly List<string> files = new List<string>();

        public static void AddTempFile(string file)
        {
            Instance.Add(file);
        }

        private void Add(string file)
        {
            lock (this.files)
            {
                this.files.Add(file);
            }

            this.actions.StartDelayedAction("Cleanup", this.DeleteAll, TimeSpan.FromSeconds(30));
        }

        public static bool HasTempFiles
        {
            get { return Instance.files.Count > 0; }
        }

        /// <summary>
        /// Remove the given file name from the list of temp files to cleanup.
        /// Call this is the file is no longer temporary.
        /// </summary>
        /// <param name="newFileName">The file name to remove from the list</param>
        internal static void RemoveTempFile(string newFileName)
        {
            lock (Instance.files)
            {
                foreach (string path in Instance.files)
                {
                    if (string.Compare(path, newFileName, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        Instance.files.Remove(path);
                    }
                }
            }
        }

        public static void Cleanup()
        {
            Instance.DeleteAll();
        }

        public void DeleteAll()
        {
            foreach (string file in this.files.ToArray())
            {
                lock (this.files)
                {
                    this.files.Remove(file);
                }
                DeleteFile(file);
            }
        }

        public static void Shutdown()
        {
            Instance.SaveTempFileList();
        }

        internal static void Initialize()
        {
            Instance.LoadTempFileList();
        }

        private void SaveTempFileList()
        {
            this.actions.CancelAll();
            XDocument doc = new XDocument(new XElement("Files"));
            foreach (string file in this.files.ToArray())
            {
                doc.Root.Add(new XElement("File", new XAttribute("Name", file)));
            }
            doc.Save(TempFileList);
        }

        private void LoadTempFileList()
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
                        Instance.files.Add(name);
                    }
                }
            }

            // now try and delete them!
            Cleanup();
        }
    }
}
