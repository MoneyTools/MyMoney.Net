using System;
using System.Diagnostics;
using System.IO;
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

        public static string ImportFileListFolder
        {
            get
            {
                var appdata = Path.Combine(AppDataPath, "Imports");
                if (!Directory.Exists(appdata))
                {
                    Directory.CreateDirectory(appdata);
                }

                return appdata;
            }
        }

        internal static bool IsFileQIF(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            if (extension == ".qif")
            {
                return true;
            }
            return false;
        }

        internal static bool IsFileOFX(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            if (extension == ".qfx" || extension == ".ofx")
            {
                return true;
            }
            return false;
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
    }
}
