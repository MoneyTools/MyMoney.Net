using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace Walkabout.Utilities
{
    public class FtpUtilities
    {
        /// <summary>
        /// Copies all files and directories from the source directory up to the target FTP directory
        /// using the given user name and password.
        /// </summary>
        /// <param name="source">Source directory containing files and directories to be copied</param>
        /// <param name="target">Target directory relative to FTP server</param>
        /// <param name="user">The FTP user name</param>
        /// <param name="password">The FTP password</param>
        /// <returns>Throws exception on failure</returns>
        public static void CopySubtree(string source, string target, string user, string password)
        {
            List<string> files = new List<string>();
            List<string> subdirs = new List<string>();
            ListDirectory(target, user, password, files, subdirs);

            foreach (string dir in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(dir); 
                if (!subdirs.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    CreateTargetDirectory(target + '/' + name, user, password);
                }

                CopySubtree(dir, target + '/' + name, user, password);
            }

            Console.WriteLine("Copying directory: " + target);

            CopyFiles(source, target, user, password);
        }

        private static void CreateTargetDirectory(string target, string user, string password)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(target);
            request.Credentials = new NetworkCredential(user, password);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            using (var response = (FtpWebResponse)request.GetResponse())
            {
                // 257 "MyMoney/download//Application Files/MyMoney_1_0_0_198" directory created.
                if (response.StatusCode != FtpStatusCode.PathnameCreated)
                {
                    throw new IOException("### failed to create target directory: " + target + "\r\n" + response.StatusDescription);
                }
            }
            return;
        }

        private static char[] WhitespaceChars = new char[] { ' ', '\t' };

        private static void ListDirectory(string target, string user, string password, List<string> files, List<string> subDirectories)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(target);
            request.Credentials = new NetworkCredential(user, password);
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            FtpWebResponse response = null;

            try
            {
                using (response = (FtpWebResponse)request.GetResponse())
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string line = reader.ReadLine();
                        while (line != null)
                        {
                            // format: "08-21-12  08:12PM       <DIR>          Icons"
                            int i = line.IndexOf("<DIR>");
                            if (i > 0)
                            {
                                string name = line.Substring(39);
                                subDirectories.Add(name);
                            }
                            else
                            {
                                string name = line.Substring(39);
                                files.Add(name);
                            }
                            line = reader.ReadLine();
                        }
                    }
                    return; // we're good, it exists.
                }
            }
            catch
            {
                // doesn't exist.
            }
        }

        public static void DeleteSubtree(string target, string user, string password)
        {
            Console.WriteLine("Deleting target directory: " + target);

            List<string> files = new List<string>();
            List<string> subdirs = new List<string>();
            ListDirectory(target, user, password, files, subdirs);

            // bugbug: can't find a way to get FtpWebRequest to show me these...
            // so we assume they are there...
            if (!target.Contains("_vti"))
            {
                subdirs.Add("_vti_cnf");
                subdirs.Add("_vti_pvt");
                subdirs.Add("_vti_script");
                subdirs.Add("_vti_txt");
            }

            foreach (string file in files)
            {
                DeleteFile(target + "/" + file, user, password);
            }

            foreach (string dir in subdirs)
            {
                DeleteSubtree(target + "/" + dir, user, password);
            }

            // now it is empty, we should be able to remove it.
            RemoveDirectory(target, user, password);
        }

        private static void RemoveDirectory(string target, string user, string password)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(target);
            request.Credentials = new NetworkCredential(user, password);
            request.Method = WebRequestMethods.Ftp.RemoveDirectory;
            try
            {
                using (var response = (FtpWebResponse)request.GetResponse())
                {
                    // "250 RMD command successful.\r\n"
                    var state = response.StatusDescription;
                    if (response.StatusCode != FtpStatusCode.FileActionOK)
                    {
                        throw new IOException("### failed to delete target: " + target + "\r\n" + state);
                    }
                }
            }
            catch
            {
            }
            return;
        }

        public static void DeleteFile(string targetFile, string user, string password)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(targetFile);
            request.Credentials = new NetworkCredential(user, password);
            request.Method = WebRequestMethods.Ftp.DeleteFile;           
            using (var response = (FtpWebResponse)request.GetResponse())
            {
                // "250 DELE command successful.\r\n"
                var state = response.StatusDescription;
                if (response.StatusCode != FtpStatusCode.FileActionOK)
                {
                    throw new IOException("### failed to delete target: " + targetFile + "\r\n" + state);
                }
                Console.WriteLine("Deleted " + targetFile);
            }
            return;
        }


        /// <summary>
        /// Copy the files from the source directory to the target directory.
        /// </summary>
        /// <param name="source">Source directory</param>
        /// <param name="target">FTP target directory</param>
        /// <param name="user">FTP username</param>
        /// <param name="password">FTP password</param>
        /// <returns>The file names that were found and uploaded</returns>
        private static IEnumerable<string> CopyFiles(string source, string target, string user, string password)
        {
            List<string> files = new List<string>();
            foreach (string fileName in Directory.GetFiles(source))
            {
                string name = Path.GetFileName(fileName);
                files.Add(fileName);

                Console.Write("Uploading file: " + name + " ...");

                try
                {
                    WebRequest request = (FtpWebRequest)WebRequest.Create(target + "/" + name);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential(user, password);
                    using (Stream s = request.GetRequestStream())
                    {
                        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                        {
                            CopyTo(fs, s);
                        }
                    }

                    Console.WriteLine();
                    FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                    using (response)
                    {
                        var state = response.StatusDescription;
                        if (response.StatusCode != FtpStatusCode.ClosingData)
                        {
                            throw new IOException("### failed to copy file: " + name + "\r\n" + state);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("### Error : " + e.Message);
                }
            }
            return files;
        }

        /// <summary>
        /// Copies all files and directories from the source directory up to the target FTP directory
        /// using the given user name and password and deletes any files or directories at the target
        /// that do not exist in the source.
        /// </summary>
        /// <param name="source">Source directory containing files and directories to be copied</param>
        /// <param name="target">Target directory relative to FTP server</param>
        /// <param name="user">The FTP user name</param>
        /// <param name="password">The FTP password</param>
        /// <returns>The number of files that failed to upload</returns>
        public static void MirrorDirectory(string source, string target, string user, string password)
        {          
            List<string> files = new List<string>();
            List<string> subdirs = new List<string>();
            ListDirectory(target, user, password, files, subdirs);

            foreach (string dir in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(dir);
                if (!subdirs.Contains(name))
                {
                    CreateTargetDirectory(target + '/' + name, user, password);
                }
                else
                {
                    subdirs.Remove(name);
                }

                MirrorDirectory(dir, target + '/' + name, user, password);
            }

            // Delete stale subdirectories that should no longer exist on the server.
            foreach (string staleDir in subdirs)
            {
                DeleteSubtree(target + '/' + staleDir, user, password);
            }

            Console.WriteLine("Mirroring directory: " + target);

            // depth first, so do inner files before we change the outer file, this
            // way we don't screw up top level ClickOnce setup files if inner files fail to copy.
            MirrorFiles(source, target, new HashSet<string>(files), user, password);
        }

        private static void MirrorFiles(string source, string target, HashSet<string> targetFiles, string user, string password)
        {

            foreach (string fileName in Directory.GetFiles(source))
            {
                string name = Path.GetFileName(fileName);
                targetFiles.Remove(name);

                Console.Write("Uploading file: " + name + " ...");
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(target + '/' + name);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(user, password);
                using (Stream s = request.GetRequestStream())
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        CopyTo(fs, s);
                    }
                }

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    var state = response.StatusDescription;
                    string[] parts = state.Split(' ');
                    int code = (int)response.StatusCode;
                    if (parts.Length > 0)
                    {
                        int.TryParse(parts[0], out code);
                    }
                    if (code != 226)
                    {
                        throw new Exception("Failed to upload file: " + target);
                    }
                    Console.WriteLine("ok");
                }
            }

            // Delete the stale files that should no longer be on the server.
            foreach (string old in targetFiles)
            {
                DeleteFile(target + "/" + old, user, password);
            }

        }

        private static void CopyTo(Stream inStream, Stream outStream)
        {
            int size = 64000;
            byte[] buffer = new byte[size];
            int len = inStream.Read(buffer, 0, size);
            while (len > 0)
            {
                outStream.Write(buffer, 0, len);
                len = inStream.Read(buffer, 0, size);
            }
        }

    }
}
