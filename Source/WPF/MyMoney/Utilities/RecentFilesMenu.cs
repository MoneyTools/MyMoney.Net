﻿using System;
using System.Collections.Generic;
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

    internal class RecentFilesMenu
    {
        private readonly List<string> recentFiles = new List<string>();
        private const int maxRecentFiles = 10;
        private readonly MenuItem parent;

        public event EventHandler<RecentFileEventArgs> RecentFileSelected;

        public RecentFilesMenu(MenuItem parent)
        {
            this.parent = parent;
        }

        public string[] ToArray()
        {
            return this.recentFiles.ToArray();
        }

        public void Clear()
        {
            this.recentFiles.Clear();
        }

        public void SetFiles(string[] files)
        {
            this.Clear();
            if (files != null)
            {
                foreach (string fileName in files)
                {
                    this.AddRecentFileName(fileName);
                }
            }
            this.SyncRecentFilesMenu();
        }

        private void AddRecentFileName(string fileName)
        {
            try
            {
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
        }

        public void AddRecentFile(string fileName)
        {
            this.AddRecentFileName(fileName);
            this.SyncRecentFilesMenu();
        }

        private void SyncRecentFilesMenu()
        {
            // Synchronize menu items.
            this.parent.Items.Clear();

            // Add most recent files first.
            for (int i = this.recentFiles.Count - 1, j = 0; i >= 0; i--, j++)
            {
                string filename = this.recentFiles[i];
                MenuItem item = new MenuItem();
                item.Click += this.OnMenuItemClick;
                this.parent.Items.Add(item);
                item.Header = string.Format("_{0} {1}", j + 1, filename);
                item.Tag = filename;
            }
        }

        private void OnMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (RecentFileSelected != null)
            {
                MenuItem item = (MenuItem)sender;
                RecentFileSelected(sender, new RecentFileEventArgs((string)item.Tag));
            }
        }

    }
}
