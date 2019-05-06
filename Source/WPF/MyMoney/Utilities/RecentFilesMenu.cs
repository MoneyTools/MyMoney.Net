using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Walkabout.Utilities
{
    public class RecentFileEventArgs : EventArgs
    {
        public string FileName;
        public RecentFileEventArgs(string fname)
        {
            this.FileName = fname;
        }
    }

    class RecentFilesMenu
    {
        List<string> recentFiles = new List<string>();
        const int maxRecentFiles = 10;
        MenuItem parent;

        public event EventHandler<RecentFileEventArgs> RecentFileSelected;

        public RecentFilesMenu(MenuItem parent)
        {
            this.parent = parent;
        }

        public string[] ToArray()
        {
            return recentFiles.ToArray();
        }

        public void Clear()
        {
            recentFiles.Clear();
        }

        public void SetFiles(string[] files)
        {
            Clear();
            if (files != null)
            {
                foreach (string fileName in files)
                {
                    AddRecentFileName(fileName);
                }
            }
            SyncRecentFilesMenu();
        }

        bool addingFile;

        void AddRecentFileName(string fileName)
        {
            try
            {
                addingFile = true;
                if (this.recentFiles.Contains(fileName))
                {
                    this.recentFiles.Remove(fileName);
                }
                string fname = fileName;
                if (!System.IO.File.Exists(fileName))
                {
                    return; // ignore deleted files.
                }
                this.recentFiles.Add(fileName);                
                if (this.recentFiles.Count > maxRecentFiles)
                {
                    this.recentFiles.RemoveAt(0);
                }
            }
            catch (System.IO.IOException)
            {
                // ignore bad files
            }
            finally
            {
                addingFile = false;
            }
        }

        public void AddRecentFile(string fileName)
        {
            AddRecentFileName(fileName);
            SyncRecentFilesMenu();
        }

        void SyncRecentFilesMenu()
        {
            // Synchronize menu items.
            if (this.recentFiles.Count == 0)
            {
                this.parent.Items.Clear();
                return;
            }

            // Add most recent files first.
            for (int i = this.recentFiles.Count - 1, j = 0; i >= 0; i--, j++)
            {
                string filename = this.recentFiles[i];
                MenuItem item = null;
                if (this.parent.Items.Count > j)
                {
                    item = this.parent.Items[j] as MenuItem;
                }
                else
                {
                    item = new MenuItem();
                    item.Click += OnMenuItemClick;
                    this.parent.Items.Add(item);
                }
                item.Header = string.Format("_{0} {1}", j + 1, filename);
                item.Tag = filename;
            }

            // Remove any extra menu items.
            for (int i = this.parent.Items.Count - 1, n = this.recentFiles.Count; i > n; i--)
            {
                this.parent.Items.RemoveAt(i);
            }
        }

        private void OnMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.RecentFileSelected != null)
            {
                MenuItem item = (MenuItem)sender;
                this.RecentFileSelected(sender, new RecentFileEventArgs((string)item.Tag));
            }
        }

    }
}
