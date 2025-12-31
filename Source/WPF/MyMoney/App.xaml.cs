using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Xml.Linq;
using Walkabout.Configuration;
using Walkabout.Help;
using Walkabout.Utilities;
using Walkabout.WpfConverters;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

[assembly: CLSCompliant(true)]
namespace Walkabout
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// 
    /// TODO: .NET 10.0 doesn't support all the ClickOnce features of .NET 4.8
    /// See https://learn.microsoft.com/en-us/visualstudio/deployment/access-clickonce-deployment-properties-dotnet?view=vs-2022
    /// </summary>
    public partial class App : Application
    {
        private ILogger rootLog;
        private Log appLog;
        private string logsLocation;

        private void MyApplicationStartup(object sender, StartupEventArgs e)
        {
            Settings settings = null;

#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.AppInitialize))
            {
#endif
            var path = Path.Combine(Path.GetTempPath(), "MyMoney");
            var logs = Path.Combine(path, "Logs");
            Directory.CreateDirectory(logs);
            this.rootLog = new Log(logs);
            this.appLog = Log.GetLogger("App");
            Log.CheckCrashLog(path);

            Debug.WriteLine($"Writing logs to {logs}");
            appLog.Info("Launching MyMoney.Net");

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
            settings = this.LoadSettings(noSettings);

            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.OnAppDomainUnhandledException);
            TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException1;

#if PerformanceBlocks
            }
#endif

            // Lets run the application since there's no another instance running
            MainWindow mainWindow = new MainWindow(settings);
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            this.appLog?.Info($"App terminating with exit code {e.ApplicationExitCode}");
            this.appLog?.Dispose();
            this.rootLog?.Dispose();
            base.OnExit(e);
        }

        public Settings LoadSettings(bool noSettings)
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
                this.appLog.Error("### Error reading settings: " + ex.Message);
            }

            Settings.TheSettings = s;
            return s;
        }

        private static void SetDefaultSettings(Settings s)
        {
            s.Theme = "Light"; // Default to this theme on the first ever run
            s.PlaySounds = true; // default true.
            s.TransferSearchDays = 5;
            s.ImportOFXAsUTF8 = false;
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
            string applicationName = System.Windows.Application.ResourceAssembly.ManifestModule.Name;

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

        // stop re-entrancy
        private bool handlingException;

        private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            this.appLog.Error("Unhandled app exception", e.Exception);
            if (this.handlingException)
            {
                e.Handled = false;
            }
            else
            { 
                this.handlingException = true;
                UiDispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        e.Handled = this.HandleUnhandledException(e.Exception);
                    }
                    catch (Exception)
                    {
                    }
                    this.handlingException = false;
                }));
            }
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            this.appLog.Error("Unhandled app domain exception", e.ExceptionObject as Exception);
            if (!this.handlingException)
            {
                this.handlingException = true;
                UiDispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        this.HandleUnhandledException(e.ExceptionObject);
                    }
                    catch (Exception)
                    {
                    }
                    this.handlingException = false;
                }));
            }
        }

        private void TaskScheduler_UnobservedTaskException1(object sender, UnobservedTaskExceptionEventArgs e)
        {
            this.appLog.Error("Unhandled task scheduler exception", e.Exception);
            if (!this.handlingException)
            {
                this.handlingException = true;
                e.SetObserved();
                UiDispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        this.HandleUnhandledException(e.Exception);
                    }
                    catch (Exception)
                    {
                    }
                    this.handlingException = false;
                }));
            }
        }

        public bool HandleUnhandledException(object exceptionObject)
        {
            Exception ex = exceptionObject as Exception;
            if (ex is ValueConverterException)
            {
                // This exception can be raised if you drag/drop text in a numeric field that results in an invalid format.
                // We cannot catch this exception because there is no Money code on the stack except the originator of the
                // exception in WpfConverters.cs. Fixes issue #156.
                return true;
            }

            string message = null;
            string details = null;
            if (ex == null && exceptionObject != null)
            {
                ex = new Exception(exceptionObject.GetType().FullName + ": " + exceptionObject.ToString());
            }

            message = ex.Message;
            details = ex.ToString();

            try
            {
                MessageBoxEx.Show(message + " - " + Log.ReportLogging, "Unhandled Exception", details, MessageBoxButton.OK, MessageBoxImage.Error);

                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                if (mw != null && mw.IsVisible)
                {
                    mw.SaveIfDirty("Unhandled exception, do you want to save your changes?", null);
                }
                return true;
            }
            catch (Exception)
            {
                // hmmm, if we can't show the dialog then perhaps this is some sort of stack overflow.
                // save the details to a file, terminate the process 
                this.appLog.FatalUnhandledException(message, ex);
                return false;
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                this.HandleUnhandledException(e.Exception);
            }
            e.SetObserved();
        }

    }
}
