using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.AccessControl;
using System.IO;
using System.Diagnostics;

namespace Walkabout.Admin
{
    /// <summary>
    /// This program performs various setup tasks that need to be executed with admin permissions.
    /// </summary>
    public class Program
    {
        static int Main(string[] args)
        {
            List<Job> jobs = new List<Job>();
            string logFile = null;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg[0] == '-' || arg[0] == '/')
                    {
                        switch (arg.Substring(1).ToLowerInvariant())
                        {
                            case "addpermissions":
                                if (i + 3 < args.Length)
                                {
                                    jobs.Add(new Permissions(args[++i], args[++i], int.Parse(args[++i])));
                                }
                                else
                                {
                                    throw new ArgumentException("Expecting 3 parameters to AddPermissions argument");
                                }
                                break;
                            case "debug":
                                Console.WriteLine("Press attach debugger and press ENTER key to continue...");
                                Console.ReadLine();
                                break;
                            case "log":
                                logFile = args[++i];
                                break;
                        }
                    }
                }

                foreach (Job job in jobs) 
                {
                    job.Execute();
                }

                return 0;
            }
            catch (Exception e)
            {
                if (logFile != null)
                {
                    using (StreamWriter writer = new StreamWriter(logFile))
                    {
                        writer.WriteLine("### Exception: " + e.ToString());
                    }
                }
                return 1;
            }
        }

        // this is called from a DLL and it launches itself to do the work with admin permissions
        public static void AddPermissions(string dir, string account, FileSystemRights rights)
        {
            Uri uri = new Uri(typeof(Program).Assembly.Location);
            string exe = uri.LocalPath;

            string outFile = Path.Combine(Path.GetTempPath(), "MoneySetup.out");
            string args = string.Format("/AddPermissions \"{0}\" \"{1}\" \"{2}\" /Log \"{3}\"", dir, account, (int)FileSystemRights.FullControl, outFile);
            //if (Debugger.IsAttached)
            //{
            //    args += " /debug";
            //}
            ProcessStartInfo startInfo = new ProcessStartInfo(exe, args);
            startInfo.Verb = "runas";
            //if (!Debugger.IsAttached)
            {
                startInfo.CreateNoWindow = true;
            }

            Process p = Process.Start(startInfo);
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                string msg = "";
                if (File.Exists(outFile))
                {
                    using (StreamReader reader = new StreamReader(outFile))
                    {
                        msg = reader.ReadToEnd();
                    }
                    File.Delete(outFile);
                }
                throw new Exception("Unexpected error adding permissions to directory.\n" + msg);
            }
        }

    }

    public abstract class Job
    {
        public abstract void Execute();
    }

    public class Permissions : Job
    {
        string directory;
        string account;
        FileSystemRights rights;

        public Permissions(string directory, string account, int controlFlags)
        {
            this.directory = directory;
            this.account = account;
            this.rights = (FileSystemRights)controlFlags;
        }

        public override void Execute()
        {
            var security = new DirectorySecurity();
            security.AddAccessRule(new FileSystemAccessRule(account, rights, AccessControlType.Allow));
            Directory.SetAccessControl(directory, security);
        }
    }
}
