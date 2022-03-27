using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Walkabout.Utilities
{
    public static class FileHelpers
    {
        /// <summary>
        /// Given 2 file names, get the the folder containing relativeTo to the fileName.
        /// </summary>
        public static string GetRelativePath(string fileName, string relativeTo)
        {
            var folder = Path.GetDirectoryName(relativeTo).Trim(Path.DirectorySeparatorChar);
            var p1 = fileName.Split(Path.DirectorySeparatorChar);
            var p2 = folder.Split(Path.DirectorySeparatorChar);
            if (string.Compare(p1[0], p2[0], StringComparison.OrdinalIgnoreCase) != 0)
            {
                // different drives? Then there is no relative path.
                return fileName;
            }
            int i = 0;
            
            // find common paths
            for (i = 0; i < p1.Length && i < p2.Length; i++)
            {
                var a = p1[i];
                var b = p2[i];
                if (string.Compare(a, b, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    break;
                }
            }

            if (i == p2.Length)
            {
                // then fileName is a child of folder
                return fileName.Substring(folder.Length + 1);
            }

            // then we have to ".." our way up from folder to our common parent, then down into fileName
            StringBuilder sb = new StringBuilder();
            int j = i;
            for (; i < p1.Length - 1; i++)
            {
                if (sb.Length > 0)
                {
                    sb.Append(Path.DirectorySeparatorChar);
                }
                sb.Append("..");
            }

            for (; j < p1.Length; j ++)
            {
                if (sb.Length > 0)
                {
                    sb.Append(Path.DirectorySeparatorChar);
                }
                sb.Append(p1[j]);
            }

            return sb.ToString();
        }

        public static bool FilesIdentical(string file1, string file2)
        {
            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);
            if (info1.Length != info2.Length)
            {
                return false;
            }
            var a = File.ReadAllBytes(file1);
            var b = File.ReadAllBytes(file2);
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
