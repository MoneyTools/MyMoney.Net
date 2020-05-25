using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Charts;
using Walkabout.Controls;
using Walkabout.Configuration;
using System.Xml;
using Walkabout.Help;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

[assembly: CLSCompliant(true)]
namespace Walkabout
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    
    public partial class App : Application
    {
        
        void MyApplicationStartup(object sender, StartupEventArgs e)
        {
            Settings settings = null;

#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.AppInitialize))
            {
#endif
                HelpService.Initialize();

                CleanUpOlderSpecialImportFile();

                Process currentRunningInstanceOfMyMoney = null;

                if (IsCommandLineContainingValidImportFile())
                {
                    // Application is running Process command line args
                    currentRunningInstanceOfMyMoney = BringToFrontApplicationIfAlreadyRunning();
                }

                bool noSettings = false;

                foreach (string arg in e.Args)
                {
                    if (string.Compare(arg, "/nosettings", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        noSettings = true;
                    }
                }

                if (currentRunningInstanceOfMyMoney != null)
                {

                    // Let the currently running application handle the IMPORT file
                    // we can close this instance 
                    Shutdown();
                    return;
                }

                // this is a fallback theme for things not specified in more specific themes.
                ProcessHelper.SetTheme(0, "Themes/Generic.xaml");

                if (Environment.OSVersion.Version >= new Version(6, 2))
                {
                    // windows 8
                    ProcessHelper.SetTheme(1, "Themes/GenericWindows8.xaml");
                }
                else
                {
                    // windows 7
                    ProcessHelper.SetTheme(1, "Themes/GenericWindows7.xaml");
                }

                // Load the application settings
                settings = LoadSettings(noSettings);

                if (string.IsNullOrEmpty(settings.Theme))
                {
                    // this theme is the most tested right now...
                    ProcessHelper.SetTheme(2, "Themes/Theme-VS2010.xaml");
                }
                else
                {
                    // set the user selected theme
                    ProcessHelper.SetTheme(2, settings.Theme);
                }

#if PerformanceBlocks
            }
#endif

            // Lets run the application since there's no another instance running
            MainWindow mainWindow = new MainWindow(settings);
            mainWindow.Show();
        }

        public static Settings LoadSettings(bool noSettings)
        {
            // make sure the directory exists.
            ProcessHelper.CreateSettingsDirectory();

            if (Settings.TheSettings != null)
            {
                return Settings.TheSettings;
            }

            Settings s = new Settings(!noSettings);
            try
            {
                if (s.ConfigFile == null || File.Exists(s.ConfigFile) == false)
                {
                    s.ConfigFile = ProcessHelper.ConfigFile;
                }

                if (File.Exists(s.ConfigFile) && !noSettings)
                {
                    using (XmlTextReader r = new XmlTextReader(s.ConfigFile))
                    {
                        s.ReadXml(r);
                    }
                }

            }
            catch
            {
            }

            Settings.TheSettings = s;
            return s;
        }

        /// <summary>
        /// If the process was invoke with a QIF file as argument
        /// we copy the file to the "well-known" folder where MyMoney.exe will be listening on
        /// If there's already a instance of MyMoney running we bring it to the foreground
        /// </summary>
        /// <returns></returns>
        private static Process BringToFrontApplicationIfAlreadyRunning()
        {
            Process currentRunningInstanceOfMyMoney = null;

            //-------------------------------------------------------------
            // Do we already have and instance of the application already running
            //
            currentRunningInstanceOfMyMoney = FindCurrentRunningMoneyApplication();
            if (currentRunningInstanceOfMyMoney != null)
            {
                // The application is already running so bring it to the foreground
                NativeMethods.ShowWindow(currentRunningInstanceOfMyMoney.MainWindowHandle, NativeMethods.SW_SHOWMAXIMIZED);
                NativeMethods.SetForegroundWindow(currentRunningInstanceOfMyMoney.MainWindowHandle);
            }

            return currentRunningInstanceOfMyMoney;
        }

        static void CleanUpOlderSpecialImportFile()
        {
            try
            {
                // bugbug: what if two downloads are done in quick succession and running Money app is blocked on a message box
                // such that it cannot process the file immediately.  We could lose data here.
                TempFilesManager.DeleteFile(FullPathToSpecialImportFileQIF);
                TempFilesManager.DeleteFile(FullPathToSpecialImportFileOFX);
            }
            catch (Exception ex)
            {
                // Be resilient if we throw and exception the application would not start

                //
                // We can't use MessageBoxEX here because this method is called before the main window get created
                //
                MessageBox.Show("Error cleaning up old IMPORT FILES:" + Environment.NewLine + ex.Message);
            }

        }


        /// <summary>
        /// If the process was invoke with a QIF file as argument
        /// we copy the file to the "well-known" folder where MyMoney.exe will be listening on
        /// If there's already a instance of MyMoney running we bring it to the foreground
        /// </summary>
        /// <returns></returns>
        private static bool IsCommandLineContainingValidImportFile()
        {

            string fileToImport = IsValidImportingFileType();
            if (string.IsNullOrEmpty(fileToImport) == false)
            {
                return CopySuppliedImportFileToListeningFolder(fileToImport);
            }

            return false;
        }

        private static string FullPathToSpecialImportFileQIF
        {
            get
            {
                return Path.Combine(ProcessHelper.GetAndUnsureLocalUserAppDataPath, Walkabout.MainWindow.SpecialImportFileNameQif);
            }
        }

        private static string FullPathToSpecialImportFileOFX
        {
            get
            {
                return Path.Combine(ProcessHelper.GetAndUnsureLocalUserAppDataPath, Walkabout.MainWindow.SpecialImportFileNameOfx);
            }
        }

        private static bool CopySuppliedImportFileToListeningFolder(string fileToImport)
        {
            //-------------------------------------------------------------
            // Copy the file passed on the command line to a well know folder that will be picked up by the application for importing
            //
            string fileToUseAsImport = null;

            if (IsFileQIF(fileToImport))
            {
                fileToUseAsImport = FullPathToSpecialImportFileQIF;
            }

            if (IsFileOFX(fileToImport))
            {
                fileToUseAsImport = FullPathToSpecialImportFileOFX;
            }

            if (String.IsNullOrEmpty(fileToUseAsImport) == false)
            {
                // Lets queue up the file by copying it in the folder where the main App will look for it
                if (File.Exists(fileToImport))
                {
                    try
                    {
                        File.Copy(fileToImport, fileToUseAsImport, true);
                        return true;
                    }
                    catch (Exception e)
                    {
                        MessageBoxEx.Show("Error importing file> " + fileToImport + Environment.NewLine + e.Message);
                    }

                }
            }
            return false;
        }

        private static string IsValidImportingFileType()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length == 2)
            {
                string importFile = args[1];


                if (IsFileQIF(importFile))
                {
                    return importFile;
                }

                if (IsFileOFX(importFile))
                {
                    return importFile;
                }

            }
            return string.Empty;
        }

        static bool IsFileQIF(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            if (extension == ".qif")
            {
                return true;
            }
            return false;
        }

        static bool IsFileOFX(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            if (extension == ".qfx" || extension == ".ofx")
            {
                return true;
            }
            return false;
        }

        private static Process FindCurrentRunningMoneyApplication()
        {

            // What's the process name of this application running
            string applicationName = ((System.Windows.Application.ResourceAssembly).ManifestModule).Name;

            // Strip the file extension
            applicationName = Path.GetFileNameWithoutExtension(applicationName);

            // Let see if this application is part of the application currently running
            Process[] processes = Process.GetProcessesByName(applicationName);

            foreach (Process p in processes)
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    return p;
                }
            }

            return null;
        }


      



    }


}
