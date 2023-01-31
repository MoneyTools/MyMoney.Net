using System.Xml.Linq;

namespace UpdateVersion
{
    public class LogWriter
    {
        public void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void LogMessage(string message)
        {
            Console.WriteLine(message);
        }
    }

    public class SyncVersions
    {
        public LogWriter Log = new LogWriter();

        public string MasterVersionFile { get; set; }

        private string VersionPropsFile { get; set; }

        private string CSharpVersionFile { get; set; }

        private string ApplicationProjectFile { get; set; }

        private string AppManifestFile { get; set; }

        private string UpdatesFile { get; set; }

        private string PublishProfile { get; set; }

        public bool Execute()
        {
            string version = File.ReadAllText(this.MasterVersionFile).Trim();

            Version v;
            if (string.IsNullOrEmpty(version) || !Version.TryParse(version, out v))
            {
                this.Log.LogError("Could not find valid version number in : " + this.MasterVersionFile);
                return false;
            }

            this.Log.LogMessage("SyncVersions to " + v.ToString());

            var versionDir = Path.GetDirectoryName(this.MasterVersionFile);
            var solutionDir = Path.GetDirectoryName(versionDir);

            this.VersionPropsFile = Path.Combine(versionDir, "Version.props");
            this.CSharpVersionFile = Path.Combine(versionDir, "Version.cs");
            this.AppManifestFile = Path.Combine(solutionDir, "MoneyPackage", "Package.appxmanifest");
            this.ApplicationProjectFile = Path.Combine(solutionDir, "MyMoney", "MyMoney.csproj");
            this.UpdatesFile = Path.Combine(solutionDir, "MyMoney", "Setup", "changes.xml");
            this.PublishProfile = Path.Combine(solutionDir, "MyMoney", "Properties", "PublishProfiles", "ClickOnceProfile.pubxml");

            bool result = this.UpdateVersionProps(v, this.VersionPropsFile);
            result &= this.UpdateCSharpVersion(v);
            result &= this.UpdatePackageManifest(v);
            result &= this.UpdateApplicationProjectFile(v);
            result &= this.CheckUpdatesFile(v);
            result &= this.UpdateVersionProps(v, this.PublishProfile);
            return result;
        }

        private bool UpdateVersionProps(Version v, string projectFile)
        {
            if (!File.Exists(projectFile))
            {
                this.Log.LogError("Cannot find file: " + projectFile);
                return false;
            }

            try
            {
                bool changed = false;
                var doc = XDocument.Load(projectFile);
                var ns = doc.Root.Name.Namespace;
                var g = doc.Root.Element(ns + "PropertyGroup");
                var r = g.Element(ns + "ApplicationRevision");
                var e = g.Element(ns + "ApplicationVersion");
                var s = v.ToString();
                if (e.Value != s)
                {
                    e.Value = s;
                    changed = true;
                }
                var rev = v.MinorRevision.ToString();
                if (r.Value != rev)
                {
                    r.Value = rev;
                    changed = true;
                }
                if (changed)
                {
                    this.Log.LogMessage("SyncVersions updating " + projectFile);
                    doc.Save(projectFile);
                }
            }
            catch (Exception ex)
            {
                this.Log.LogError("file '" + projectFile + "' edit failed: " + ex.Message);
                return false;
            }
            return true;
        }

        private bool UpdateCSharpVersion(Version v)
        {
            if (!File.Exists(this.CSharpVersionFile))
            {
                this.Log.LogError("Cannot find Version.cs file: " + this.CSharpVersionFile);
                return false;
            }
            // Fix these assembly attributes:
            // [assembly: AssemblyVersion("2.8.0.29")]
            // [assembly: AssemblyFileVersion("2.8.0.29")]
            bool changed = false;
            string[] prefixes = new string[] { "[assembly: AssemblyVersion", "[assembly: AssemblyFileVersion" };
            string[] lines = File.ReadAllLines(this.CSharpVersionFile);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                foreach (var prefix in prefixes)
                {
                    if (line.StartsWith(prefix))
                    {
                        string expected = string.Format("{0}(\"{1}\")]", prefix, v);
                        if (line != expected)
                        {
                            lines[i] = expected;
                            changed = true;
                        }
                    }
                }
            }
            if (changed)
            {
                try
                {
                    this.Log.LogMessage("SyncVersions updating " + this.CSharpVersionFile);
                    File.WriteAllLines(this.CSharpVersionFile, lines);
                }
                catch (Exception ex)
                {
                    this.Log.LogError("file '" + this.CSharpVersionFile + "' edit failed: " + ex.Message);
                    return false;
                }
            }
            // return that there is no error.
            return true;
        }

        private bool UpdateApplicationProjectFile(Version v)
        {
            if (!File.Exists(this.ApplicationProjectFile))
            {
                this.Log.LogError("ApplicationProjectFile file not found: " + this.ApplicationProjectFile);
                return false;
            }
            try
            {
                bool changed = false;
                XDocument doc = XDocument.Load(this.ApplicationProjectFile);
                var ns = doc.Root.Name.Namespace;

                // ClickOnce is wrongly editing the project file to "add" these items, when in reality
                // they need to be inherited from version.props.
                List<XElement> toRemove = new List<XElement>();
                foreach (var e in doc.Root.Elements(ns + "PropertyGroup"))
                {
                    foreach (var f in e.Elements(ns + "ApplicationRevision"))
                    {
                        toRemove.Add(f);
                    }
                    foreach (var f in e.Elements(ns + "ApplicationVersion"))
                    {
                        toRemove.Add(f);
                    }
                }

                foreach (var e in toRemove)
                {
                    e.Remove();
                    changed = true;
                }

                if (changed)
                {
                    this.Log.LogMessage("SyncVersions updating " + this.ApplicationProjectFile);
                    doc.Save(this.ApplicationProjectFile);
                }
            }
            catch (Exception ex)
            {
                this.Log.LogError("file '" + this.AppManifestFile + "' edit failed: " + ex.Message);
                return false;
            }

            // return that there is no error.
            return true;
        }


        private bool UpdatePackageManifest(Version v)
        {
            if (!File.Exists(this.AppManifestFile))
            {
                this.Log.LogError("AppManifest file not found: " + this.AppManifestFile);
                return false;
            }

            try
            {
                string newVersion = v.ToString();
                bool changed = false;
                XDocument doc = XDocument.Load(this.AppManifestFile);
                var ns = doc.Root.Name.Namespace;
                foreach (var e in doc.Root.Elements(ns + "Identity"))
                {
                    var s = (string)e.Attribute("Version");
                    if (s != newVersion)
                    {
                        changed = true;
                        e.SetAttributeValue("Version", newVersion);
                    }
                }

                if (changed)
                {
                    this.Log.LogMessage("SyncVersions updating " + this.AppManifestFile);
                    doc.Save(this.AppManifestFile);
                }
            }
            catch (Exception ex)
            {
                this.Log.LogError("file '" + this.AppManifestFile + "' edit failed: " + ex.Message);
                return false;
            }
            // return that there is no error.
            return true;
        }

        private bool CheckUpdatesFile(Version v)
        {
            if (!File.Exists(this.UpdatesFile))
            {
                this.Log.LogError("File not found: " + this.UpdatesFile);
                return false;
            }

            try
            {
                XDocument doc = XDocument.Load(this.UpdatesFile);
                XNamespace ns = doc.Root.Name.Namespace;
                XElement firstVersion = doc.Root.Element("change");
                if (firstVersion == null || v.ToString() != (string)firstVersion.Attribute("version"))
                {
                    this.Log.LogMessage("Please remember to add new version section to : " + this.UpdatesFile);
                }
            }
            catch (Exception ex)
            {
                this.Log.LogError("CheckUpdatesFile failed: " + ex.Message);
                return false;
            }
            // return that there is no error.
            return true;
        }
    }

    internal class Program
    {
        private SyncVersions sv = new SyncVersions();

        public bool ParseCommandLine(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    switch (arg.Trim('-'))
                    {
                        case "help":
                            return false;
                        default:
                            this.sv.Log.LogError("### unexpected argument: " + arg);
                            return false;
                    }
                }
                else if (string.IsNullOrEmpty(this.sv.MasterVersionFile))
                {
                    this.sv.MasterVersionFile = arg;
                    if (!File.Exists(this.sv.MasterVersionFile))
                    {
                        this.sv.Log.LogError(string.Format("### version file '{0}' not found!", arg));
                        return false;
                    }
                }
                else
                {
                    this.sv.Log.LogError("### too many arguments");
                    return false;
                }
            }
            if (string.IsNullOrEmpty(this.sv.MasterVersionFile))
            {
                this.sv.Log.LogError("### missing argument");
                return false;
            }
            return true;
        }

        private void PrintUsage()
        {
            Console.WriteLine("Usage: UpdateVersion <root>");
            Console.WriteLine("Synchronizes the build version info from the given 'version.txt' file to all the");
            Console.WriteLine("other places that need to use this version string.");
        }

        private static int Main(string[] args)
        {
            Program p = new Program();
            if (!p.ParseCommandLine(args))
            {
                p.PrintUsage();
                return 1;
            }
            if (!p.sv.Execute())
            {
                return 1;
            }
            return 0;
        }
    }
}