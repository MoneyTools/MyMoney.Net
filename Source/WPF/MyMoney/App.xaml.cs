using System;
using System.Collections.Generic;
using System.Windows;
using System.Diagnostics;
using System.IO;
using Walkabout.Utilities;
using Walkabout.Configuration;
using System.Xml.Linq;
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

                Process currentRunningInstanceOfMyMoney = null;

                if (SaveImportArgs())
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
                    this.Shutdown();
                    return;
                }


                // Load the application settings
                settings = LoadSettings(noSettings);

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
                SetDefaultSettings(s);

                if (s.ConfigFile == null || File.Exists(s.ConfigFile) == false)
                {
                    s.ConfigFile = ProcessHelper.ConfigFile;
                }

                if (File.Exists(s.ConfigFile) && !noSettings)
                {
                    s.Load(s.ConfigFile);
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("### Error reading settings: " + ex.Message);
            }

            Settings.TheSettings = s;
            return s;
        }

        private static void SetDefaultSettings(Settings s)
        {
            s.Theme = "Light"; // Default to this theme on the first ever run
            s.PlaySounds = true; // default true.
            s.TransferSearchDays = 5;
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
                NativeMethods.ShowWindow(currentRunningInstanceOfMyMoney.MainWindowHandle, NativeMethods.SW_SHOWNORMAL);
                NativeMethods.SetForegroundWindow(currentRunningInstanceOfMyMoney.MainWindowHandle);
            }

            return currentRunningInstanceOfMyMoney;
        }


        /// <summary>
        /// If the process was invoked with a QIF or QFX filename while MyMoney is already
        /// running then user double clicked an import file, so we copy the filename to
        /// a special file list of pending imports which the already a instance of MyMoney 
        /// is watching for changes.  
        /// </summary>
        /// <returns></returns>
        private static bool SaveImportArgs()
        {
            bool found = false;
            XDocument imports = LoadImportFileList();
            foreach (var fileToImport in GetValidImportFiles())
            {
                imports.Root.Add(new XElement("Import", new XAttribute("Path", fileToImport)));
                found = true;
            }
            if (found)
            {
                imports.Save(Walkabout.MainWindow.ImportFileListPath);
            }
            return found;
        }

        internal static XDocument LoadImportFileList()
        {
            var path = Walkabout.MainWindow.ImportFileListPath;
            if (File.Exists(path))
            {
                try
                {
                    return XDocument.Load(path);
                }
                catch { }
            }
            return new XDocument(new XElement("Imports"));
        }

        private static IEnumerable<string> GetValidImportFiles()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 1; i < args.Length; i++)
            {
                string importFile = args[i];
                if (ProcessHelper.IsFileQIF(importFile) || ProcessHelper.IsFileOFX(importFile))
                {
                    yield return importFile;
                }
            }
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
