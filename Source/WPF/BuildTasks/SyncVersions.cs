using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MyMoneyBuildTasks
{
    public class SyncVersions : Task
    {
        [Required]
        public string MasterVersionFile { get; set; }

        [Required]
        public string CSharpVersionFile { get; set; }

        [Required]
        public string ApplicationProjectFile { get; set; }

        [Required]
        public string AppManifestFile { get; set; }

        [Required]
        public string UpdatesFile { get; set; }

        public override bool Execute()
        {
            if (!System.IO.File.Exists(this.MasterVersionFile))
            {
                Log.LogError("Cannot find master version file: " + this.MasterVersionFile);
                return false;
            }
            if (!System.IO.File.Exists(this.CSharpVersionFile))
            {
                Log.LogError("Cannot find C# version file: " + this.CSharpVersionFile);
                return false;
            }

            var doc = XDocument.Load(this.MasterVersionFile);
            var ns = doc.Root.Name.Namespace;
            var e = doc.Root.Element(ns + "PropertyGroup").Element(ns + "ApplicationVersion");
            string version = e.Value;

            Version v;
            if (string.IsNullOrEmpty(version) || !Version.TryParse(version, out v))
            {
                Log.LogError("Could not find valid valid version number in : " + this.MasterVersionFile);
                return false;
            }

            Log.LogMessage(MessageImportance.High, "SyncVersions to " + v.ToString());

            bool result = UpdateCSharpVersion(v);
            result &= UpdatePackageManifest(v);
            result &= UpdateApplicationProjectFile(v);
            result &= CheckUpdatesFile(v);
            return result;
        }

        private bool UpdateCSharpVersion(Version v)
        {
            // Fix these assembly attributes:
            // [assembly: AssemblyVersion("2.8.0.29")]
            // [assembly: AssemblyFileVersion("2.8.0.29")]
            bool changed = true;
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
                    File.WriteAllLines(this.CSharpVersionFile, lines);
                }
                catch (Exception ex)
                {
                    Log.LogError("file '" + CSharpVersionFile + "' edit failed: " + ex.Message);
                }
            }
            // return that there is no error.
            return true;
        }

        private bool UpdateApplicationProjectFile(Version v)
        {
            if (!System.IO.File.Exists(this.ApplicationProjectFile))
            {
                Log.LogError("ApplicationProjectFile file not found: " + this.ApplicationProjectFile);
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
                    Log.LogMessage(MessageImportance.High, "SyncVersions updating " + this.ApplicationProjectFile);
                    doc.Save(this.ApplicationProjectFile);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("file '" + this.AppManifestFile + "' edit failed: " + ex.Message);
                return false;
            }

            // return that there is no error.
            return true;
        }


        private bool UpdatePackageManifest(Version v)
        {
            if (!System.IO.File.Exists(this.AppManifestFile))
            {
                Log.LogError("AppManifest file not found: " + this.AppManifestFile);
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
                    Log.LogMessage(MessageImportance.High, "SyncVersions updating " + this.AppManifestFile);
                    doc.Save(this.AppManifestFile);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("file '" + this.AppManifestFile + "' edit failed: " + ex.Message);
                return false;
            }
            // return that there is no error.
            return true;
        }

        private bool CheckUpdatesFile(Version v)
        {
            if (!System.IO.File.Exists(this.UpdatesFile))
            {
                Log.LogError("File not found: " + this.UpdatesFile);
                return false;
            }

            try
            {
                XDocument doc = XDocument.Load(this.UpdatesFile);
                XNamespace ns = doc.Root.Name.Namespace;
                XElement firstVersion = doc.Root.Element("change");
                if (firstVersion == null || v.ToString() != (string)firstVersion.Attribute("version"))
                {
                    Log.LogMessage(MessageImportance.High, "Please remember to add new version section to : " + this.UpdatesFile);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("CheckUpdatesFile failed: " + ex.Message);
                return false;
            }
            // return that there is no error.
            return true;
        }
    }
}
