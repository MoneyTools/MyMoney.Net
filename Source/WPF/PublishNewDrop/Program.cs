using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PublishNewDrop
{
    class Program
    {
        string user;
        string password;
        string source;
        string target;
        bool nodelete;

        static int Main(string[] args)
        {
            Program p = new Program();
            if (p.ParseCommandLine(args))
            {
                try
                {
                    p.Run();
                }
                catch (Exception e)
                {
                    Console.WriteLine("### Error: " + e.Message);
                    return 1;
                }
            }
            else
            {
                PrintUsage();
                return 1;
            }
            return 0;
        }

        private void Run()
        {
            if (nodelete) {
                Walkabout.Utilities.FtpUtilities.CopySubtree(source, target, user, password);            
            } else {
                Walkabout.Utilities.FtpUtilities.MirrorDirectory(source, target, user, password);            
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("PublishNewDrop [options] source target");
            Console.WriteLine("Mirrors the local directory up on the target FTP server directory");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("/user username\tThe FTP user name");
            Console.WriteLine("/password password\tThe FTP user password");
            Console.WriteLine("/nodelete\tDo not delete files at target that do not exist on source");
        }

        bool ParseCommandLine(string[] args)
        {
            for (int i = 0, n = args.Length; i < n; i++)
            {
                string arg = args[i];
                if (arg[0] == '-' || arg[0] == '/')
                {
                    switch (arg.Substring(1).ToLowerInvariant())
                    {
                        case "user":
                            if (i + 1 < n)
                            {
                                user = args[++i];
                            }
                            else
                            {
                                Console.WriteLine("Missing username argument");
                            }
                            break;
                        case "password":
                            if (i + 1 < n)
                            {
                                password = args[++i];
                            }
                            else
                            {
                                Console.WriteLine("Missing password argument");
                            } break;
                        case "nodelete":
                            nodelete = true;
                            break;
                        case "?":
                        case "help":
                            return false;
                    }
                }
                else if (source == null)
                {
                    source = arg;
                }
                else if (target == null)
                {
                    target = arg;
                }
                else
                {
                    Console.WriteLine("Too many command line arguments");
                    return false;
                }
            }
            if (string.IsNullOrEmpty(source))
            {
                Console.WriteLine("Missing source directory");
                return false;
            }
            if (string.IsNullOrEmpty(target))
            {
                Console.WriteLine("Missing target directory");
                return false;
            }
            if (string.IsNullOrEmpty(user))
            {
                Console.WriteLine("Missing username option");
                return false;
            }
            if (string.IsNullOrEmpty(this.password))
            {
                Console.WriteLine("Missing password option");
                return false;
            }
            return true;
        }
    }
}
