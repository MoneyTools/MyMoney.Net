using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace Walkabout.Utilities
{
    public static class ProcessHelper
    {
        public static string StartupPath
        {
            get
            {
                Process p = Process.GetCurrentProcess();
                string exe = p.MainModule.FileName;
                return Path.GetDirectoryName(exe);
            }
        }

        public static string MainExecutable
        {
            get
            {
                Process p = Process.GetCurrentProcess();
                return p.MainModule.FileName;
            }
        }

        public static string GetAndUnsureLocalUserAppDataPath
        {
            get
            {
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                appdata = Path.Combine(appdata, "Walkabout\\MyMoney");

                if (Directory.Exists(appdata) == false)
                {
                    Directory.CreateDirectory(appdata);
                }

                return appdata;
            }
        }

        public static string GetEmbeddedResource(string name)
        {
            using (Stream s = typeof(ProcessHelper).Assembly.GetManifestResourceStream(name))
            {
                StreamReader reader = new StreamReader(s);
                return reader.ReadToEnd();
            }
        }

        public static XDocument GetEmbeddedResourceAsXml(string name)
        {
            using (Stream s = typeof(ProcessHelper).Assembly.GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    throw new Exception("Internal error, missing resource: " + name);
                }
                return XDocument.Load(s);
            }
        }

        public static bool ExtractEmbeddedResourceAsFile(string name, string path)
        {
            using (Stream s = typeof(ProcessHelper).Assembly.GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[64000];
                    int len = 0;
                    while ((len = s.Read(buffer, 0, buffer.Length)) > 0) 
                    {
                        fs.Write(buffer, 0, len);
                    }
                    fs.Close();
                }
            }
            return true;
        }




        public static void CreateSettingsDirectory()
        {
            string path = ConfigFile;
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        public static string ConfigFile
        {
            get
            {
                string user = Environment.GetEnvironmentVariable("USERNAME");
                return System.IO.Path.Combine(AppDataPath, user + ".settings");
            }
        }

        public static string AppDataPath
        {
            get
            {
                string user = Environment.GetEnvironmentVariable("USERNAME");
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMoney");
                return folder;
            }
        }

        /// <summary>
        /// Set theme with ordering
        ///   index [0] is for Generic.xaml 
        ///   index [1] is for Aero*.xaml OS specific theme
        ///   index [2] is for the currently user selected theme
        /// </summary>
        /// <param name="index"></param>
        /// <param name="themeToAdd"></param>
        static public void SetTheme(int index, string themeToAdd)
        {
            if (themeToAdd != null)
            {
                Uri themeUri = new Uri(themeToAdd, UriKind.Relative);
                try
                {
                    ResourceDictionary theme = (ResourceDictionary)Application.LoadComponent(themeUri);
                    if (Application.Current.Resources.MergedDictionaries.Count - 1 < index)
                    {
                        Application.Current.Resources.MergedDictionaries.Add(theme);
                    }
                    else
                    {
                        Application.Current.Resources.MergedDictionaries[index] = theme;
                    }
                }
                catch
                {
                    // Survive not find the theme set by the user
                }
            }
        }
    }
}
