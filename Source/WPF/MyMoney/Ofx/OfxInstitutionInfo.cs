using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Walkabout.Data;

namespace Walkabout.Ofx
{
    /// <summary>
    /// This class represents a field that has date/time of last change information so we
    /// can merge do a better job of merging this information when it comes from multiple
    /// places.  THe end user can edit it, and we download updates from http://www.ofxhome.com 
    /// </summary>
    public class ChangeTrackedField
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public DateTime? LastChange { get; set; }
    }

    public class OfxInstitutionInfo
    {
        public string Id
        {
            get { return this.GetValue<string>("Id"); }
            set { this.SetValue("Id", value); }
        }

        public string Name
        {
            get { return this.GetValue<string>("Name"); }
            set { this.SetValue("Name", value); }

        }
        public string OfxVersion
        {
            get { return this.GetValue<string>("OfxVersion"); }
            set { this.SetValue("OfxVersion", value); }
        }
        public string Org
        {
            get { return this.GetValue<string>("Org"); }
            set { this.SetValue("Org", value); }
        }

        public string Fid
        {
            get { return this.GetValue<string>("Fid"); }
            set { this.SetValue("Fid", value); }
        }
        public string BankId
        {
            get { return this.GetValue<string>("BankId"); }
            set { this.SetValue("BankId", value); }
        }

        public string BrokerId
        {
            get { return this.GetValue<string>("BrokerId"); }
            set { this.SetValue("BrokerId", value); }
        }
        public string ProviderURL
        {
            get { return this.GetValue<string>("ProviderURL"); }
            set { this.SetValue("ProviderURL", value); }
        }
        public string SmallLogoURL
        {
            get { return this.GetValue<string>("SmallLogoURL"); }
            set { this.SetValue("SmallLogoURL", value); }
        }
        public string Website
        {
            get { return this.GetValue<string>("Website"); }
            set { this.SetValue("Website", value); }
        }
        public string OfxHomeId
        {
            get { return this.GetValue<string>("OfxHomeId"); }
            set { this.SetValue("OfxHomeId", value); }
        }
        public string MoneyDanceId
        {
            get { return this.GetValue<string>("MoneyDanceId"); }
            set { this.SetValue("MoneyDanceId", value); }
        }
        public string AppId
        {
            get { return this.GetValue<string>("AppId"); }
            set { this.SetValue("AppId", value); }
        }
        public string AppVer
        {
            get { return this.GetValue<string>("AppVer"); }
            set { this.SetValue("AppVer", value); }
        }
        public string LastError
        {
            get { return this.GetValue<string>("LastError"); }
            set { this.SetValue("LastError", value); }
        }

        public DateTime? LastConnection
        {
            get { return this.GetValue<DateTime?>("LastConnection"); }
            set { this.SetValue("LastConnection", value); }
        }

        public bool Existing
        {
            get { return this.GetValue<bool>("Existing"); }
            set { this.SetValue("Existing", value); }
        }

        public bool HasError
        {
            get
            {
                return !string.IsNullOrEmpty(this.LastError);
            }
        }

        // this field is not persisted or merged, it is a transient value used in memory only.
        public OnlineAccount OnlineAccount { get; set; }


        // we keep track of each field change
        private readonly Dictionary<string, ChangeTrackedField> fields = new Dictionary<string, ChangeTrackedField>();

        private T GetValue<T>(string name)
        {
            ChangeTrackedField field = null;
            this.fields.TryGetValue(name, out field);
            if (field == null || field.Value == null)
            {
                return default(T);
            }
            return (T)field.Value;
        }

        private void SetValue(string name, object value)
        {
            this.SetValue(name, value, DateTime.Now);
        }

        private void SetValue(string name, object value, DateTime? changed)
        {
            ChangeTrackedField field = null;
            this.fields.TryGetValue(name, out field);
            if (field == null)
            {
                field = new ChangeTrackedField() { Name = name };
                this.fields[name] = field;
            }
            if (field.Value != value)
            {
                field.Value = value;
                field.LastChange = changed;
            }
        }

        public override string ToString()
        {
            return this.Name ?? "";
        }

        internal static OfxInstitutionInfo Create(XElement e)
        {
            OfxInstitutionInfo result = new OfxInstitutionInfo();

            foreach (XElement child in e.Elements())
            {
                DateTime? lastChanged = null;
                string date = (string)child.Attribute("changed");
                if (!string.IsNullOrEmpty(date))
                {
                    DateTime dt;
                    if (DateTime.TryParse(date, out dt))
                    {
                        lastChanged = dt;
                    }
                }
                result.SetValue(child.Name.LocalName, child.Value, lastChanged);
            }
            return result;
        }

        private static DateTime? GetElementDateTime(XElement e, string name)
        {
            string s = GetElementString(e, name);
            if (!string.IsNullOrEmpty(s))
            {
                DateTime dt;
                if (DateTime.TryParse(s, out dt))
                {
                    return dt;
                }
            }
            return null;
        }

        public bool AddInfoFromOfxHome(XElement ofxHomeInfo)
        {
            /*
              <institution id="542">
                    <name>1st Advantage FCU</name>
                    <fid>251480563</fid>
                    <org>1st Advantage FCU</org>
                    <brokerid></brokerid>
                    <url>https://members.1stadvantage.org/scripts/isaofx.dll</url>
                    <ofxfail>0</ofxfail>
                    <sslfail>0</sslfail>
                    <lastofxvalidation>2011-10-21 22:00:07</lastofxvalidation>
                    <lastsslvalidation>2011-10-21 22:00:05</lastsslvalidation>
              </institution>
             */

            bool changed = false;

            DateTime? lastvalidation = null;

            string lastval = GetElementString(ofxHomeInfo, "lastofxvalidation");
            if (!string.IsNullOrEmpty(lastval))
            {
                DateTime dt;
                if (DateTime.TryParse(lastval, out dt))
                {
                    lastvalidation = dt;
                }
            }

            // the OfxHomeId field must already be set otherwise we wouldn't have been able
            // to get this update from OfxHome.com.

            changed |= this.SetIfNewer("Name", GetElementString(ofxHomeInfo, "name"), lastvalidation);
            changed |= this.SetIfNewer("Org", GetElementString(ofxHomeInfo, "org"), lastvalidation);

            if (string.IsNullOrEmpty(this.Org))
            {
                this.Org = GetElementString(ofxHomeInfo, "org");
                changed = true;
            }
            if (string.IsNullOrEmpty(this.Fid))
            {
                this.Fid = GetElementString(ofxHomeInfo, "fid");
                changed = true;
            }
            if (string.IsNullOrEmpty(this.BrokerId))
            {
                this.BrokerId = GetElementString(ofxHomeInfo, "brokerid");
                changed = true;
            }
            if (string.IsNullOrEmpty(this.ProviderURL))
            {
                this.ProviderURL = GetElementString(ofxHomeInfo, "url");
                changed = true;
            }

            return changed;
        }

        private bool SetIfNewer(string name, string value, DateTime? lastvalidation)
        {
            bool setit = false;
            ChangeTrackedField field;
            if (this.fields.TryGetValue(name, out field))
            {
                // see if ofxhome is more recent
                setit = lastvalidation.HasValue && (!field.LastChange.HasValue || field.LastChange.Value < lastvalidation.Value);
            }
            else
            {
                setit = true;
            }
            if (setit)
            {
                this.SetValue(name, value, lastvalidation);
            }
            return setit;
        }

        private static string GetElementString(XElement parent, string name)
        {
            XElement e = parent.Element(name);
            if (e != null)
            {
                return e.Value;
            }
            return null;
        }


        internal XElement ToXml()
        {
            XElement e = new XElement("institution");

            foreach (var field in this.fields)
            {
                ChangeTrackedField ctf = field.Value;
                XElement fx = new XElement(ctf.Name, ctf.Value);
                if (ctf.LastChange.HasValue)
                {
                    // remember when it was last changed.
                    fx.SetAttributeValue("changed", ctf.LastChange.Value);
                }
                e.Add(fx);
            }
            return e;
        }

        internal bool Merge(OfxInstitutionInfo profile)
        {
            Debug.Assert((string.IsNullOrEmpty(this.MoneyDanceId) || string.IsNullOrEmpty(profile.MoneyDanceId) || string.Compare(this.MoneyDanceId, profile.MoneyDanceId) == 0)
                && (string.IsNullOrEmpty(this.OfxHomeId) || string.IsNullOrEmpty(profile.OfxHomeId) || string.Compare(this.OfxHomeId, profile.OfxHomeId) == 0));

            bool changed = false;

            foreach (ChangeTrackedField field in profile.fields.Values)
            {
                bool setit = false;
                ChangeTrackedField ourField;
                if (this.fields.TryGetValue(field.Name, out ourField))
                {
                    if (ourField.Value != field.Value)
                    {
                        // see which one is newer.
                        setit = field.LastChange.HasValue && (!ourField.LastChange.HasValue || ourField.LastChange.Value < field.LastChange.Value);
                    }
                    else
                    {
                        // values are equal, no need for a change then.
                    }
                }
                else
                {
                    // new to us
                    setit = true;
                }
                if (setit)
                {
                    this.SetValue(field.Name, field.Value, field.LastChange);
                    changed = true;
                }
            }

            return changed;
        }


        /// <summary>
        /// This code can parse the Python script for MoneyDance in order to glean OfxInstitutionInfo.
        /// You can then merge that with the existing ofx-index data from ofxhome.com
        /// </summary>
        /// <param name="filename">The text file containing MoneyDance python code</param>
        /// <returns>The list of OfxInstitutionInfo found in the MoneyDance file</returns>
        public static List<OfxInstitutionInfo> ParseMoneyDancePythonScript(string filename)
        {
            List<OfxInstitutionInfo> result = new List<OfxInstitutionInfo>();

            string text = null;

            using (StreamReader reader = new StreamReader(filename))
            {
                text = reader.ReadToEnd();
            }

            for (int i = 0, n = text.Length; i < n; i++)
            {
                char ch = text[i];
                if (ch == '{')
                {
                    OfxInstitutionInfo info = new OfxInstitutionInfo();
                    result.Add(info);

                    string name = null;
                    string value = null;

                    for (++i; i < n; i++)
                    {
                        ch = text[i];
                        if (ch == '}')
                        {
                            // done
                            value = null;
                            break;
                        }
                        else if (ch == '"')
                        {
                            i++;
                            for (int j = i; i < n; j++)
                            {
                                ch = text[j];
                                if (ch == '"')
                                {
                                    value = text.Substring(i, j - i);
                                    i = j;
                                    break;
                                }
                            }

                            if (name != null)
                            {
                                // map to ofx-index format.
                                switch (name)
                                {
                                    case "fi_name":
                                        info.Name = value;
                                        break;
                                    case "fi_org":
                                        info.Org = value;
                                        break;
                                    case "id":
                                        info.MoneyDanceId = value;
                                        break;
                                    case "bootstrap_url":
                                        info.ProviderURL = value;
                                        break;
                                    case "fi_id":
                                        info.Fid = value;
                                        break;
                                    case "app_id":
                                        info.AppId = value;
                                        break;
                                    case "app_ver":
                                        info.AppVer = value;
                                        break;
                                    case "broker_id":
                                        info.BrokerId = value;
                                        break;
                                }

                                name = null;
                                value = null;
                            }
                        }
                        else if (ch == '=')
                        {
                            name = value;
                            value = null;
                        }
                    }
                }
            }

            return result;
        }

        private static List<OfxInstitutionInfo> providerListCache;
        private static readonly string OfxHomeProviderList = "http://www.ofxhome.com/api.php?all=yes";

        public static List<OfxInstitutionInfo> GetCachedBankList()
        {
            if (providerListCache != null)
            {
                return providerListCache;
            }

            // Check local cache.
            string fname = OfxProviderList;

            List<OfxInstitutionInfo> list = new List<OfxInstitutionInfo>();

            // This is the default master list (and can have updated values that ship with new versions of the app!)
            using (Stream s = typeof(OfxRequest).Assembly.GetManifestResourceStream("Walkabout.Ofx.OfxProviderList.xml"))
            {
                if (s != null)
                {
                    XDocument builtIndoc = XDocument.Load(s);
                    MergeProviderList(list, builtIndoc);
                }
            }


            if (File.Exists(fname))
            {
                try
                {
                    var cached = XDocument.Load(fname);
                    if (cached != null)
                    {
                        MergeProviderList(list, cached);
                    }
                }
                catch
                {
                    // rats, got corrupted, so start over.
                }
            }

            SaveList(list);
            return list;
        }

        private static List<OfxInstitutionInfo> LoadProviderList(XDocument doc)
        {
            List<OfxInstitutionInfo> list = new List<OfxInstitutionInfo>();
            Dictionary<string, OfxInstitutionInfo> map = new Dictionary<string, OfxInstitutionInfo>();

            foreach (XElement e in doc.Root.Elements())
            {
                OfxInstitutionInfo info = OfxInstitutionInfo.Create(e);
                if (info != null && info.Name != null)
                {
                    OfxInstitutionInfo other;
                    if (map.TryGetValue(info.Name, out other))
                    {
                        other.Merge(info);
                    }
                    else
                    {
                        list.Add(info);
                        map[info.Name] = info;
                    }
                }
            }

            return list;
        }

        public static async Task<List<OfxInstitutionInfo>> GetRemoteBankList(CancellationToken token)
        {
            if (providerListCache != null)
            {
                return providerListCache;
            }

            List<OfxInstitutionInfo> list = GetCachedBankList();

            // check if the OfxHome list has been updated.
            try
            {
                string url = OfxHomeProviderList;
                HttpClient client = new HttpClient();
                var msg = await client.GetAsync(url, token);
                if (msg.IsSuccessStatusCode)
                {
                    using (Stream s = await msg.Content.ReadAsStreamAsync(token))
                    {
                        XDocument all = XDocument.Load(s);
                        all.Save(Path.Combine(OfxRequest.OfxLogPath, "OfxHomeList.xml"));
                        MergeProviderList(list, all);
                    }
                }
                else
                {
                    Debug.WriteLine("GetRemoteBankList failed " + msg.StatusCode + ": " + msg.ReasonPhrase);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("GetRemoteBankList " + e.GetType().Name + ": " + e.Message);
            }

            // save list in case it was updated.
            SaveList(list);

            providerListCache = list;
            return list;
        }

        private static void UpdateCachedProfile(OfxInstitutionInfo profile)
        {
            List<OfxInstitutionInfo> list = GetCachedBankList();
            if (list.Contains(profile))
            {
                // already there.
                SaveList(list);
                return;
            }
            else
            {
                // find the same provider in our cacheList and merge the info
                foreach (OfxInstitutionInfo item in list)
                {
                    if (string.Compare(item.MoneyDanceId, profile.MoneyDanceId) == 0 ||
                        string.Compare(item.OfxHomeId, profile.OfxHomeId) == 0)
                    {
                        if (item.Merge(profile))
                        {
                            SaveList(list);
                        }
                        break;
                    }
                }
            }
        }
        private static string OfxProviderList
        {
            get
            {
                string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMoney");
                Directory.CreateDirectory(appdata);
                string fname = Path.Combine(appdata, "ofx-index.xml");
                return fname;
            }
        }

        public static void SaveList(List<OfxInstitutionInfo> list)
        {
            string fname = OfxProviderList;

            XDocument doc = new XDocument(new XElement("institutions"));
            foreach (OfxInstitutionInfo info in list)
            {
                doc.Root.Add(info.ToXml());
            }
            doc.Save(fname);
        }

        private static bool? IsMergable(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                // not enough info to be sure
                return null;
            }
            else if (string.Compare(a, b) == 0)
            {
                return true;
            }
            return false;
        }

        private static void MergeProviderList(List<OfxInstitutionInfo> result, XDocument doc)
        {
            try
            {

                /* The financial institution list from http://www.ofxhome.com/api.php?all=yes
                 * is in this format:
                    <institutionlist> 
                        <institutionid id="555" name="121 Financial Credit Union"/>         
                        ...             
                */
                foreach (XElement institution in doc.Root.Elements())
                {
                    OfxInstitutionInfo c = OfxInstitutionInfo.Create(institution);

                    string name = c.Name;
                    if (!string.IsNullOrEmpty(name))
                    {
                        bool exists = false;
                        foreach (OfxInstitutionInfo i in result)
                        {
                            bool? mergable = IsMergable(i.MoneyDanceId, c.MoneyDanceId);
                            if (!mergable.HasValue)
                            {
                                mergable = IsMergable(i.OfxHomeId, c.OfxHomeId);
                            }
                            if (!mergable.HasValue)
                            {
                                mergable = IsMergable(i.Name, c.Name);
                            }

                            if (mergable == true)
                            {
                                exists = true;
                                i.Merge(c);
                                break;
                            }
                        }
                        if (!exists)
                        {
                            // found a new one!
                            result.Add(c);
                        }
                    }
                }
            }
            catch
            {
                // todo: error handling...
            }
        }

        private static readonly string OfxHomeProviderInfo = "http://www.ofxhome.com/api.php?lookup={0}";

        public static OfxInstitutionInfo GetProviderInformation(OfxInstitutionInfo provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.OfxHomeId))
            {
                return null;
            }

            // update the cached information.
            string url = string.Format(OfxHomeProviderInfo, provider.OfxHomeId);
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "GET";
                HttpWebResponse r = request.GetResponse() as HttpWebResponse;
                using (Stream s = r.GetResponseStream())
                {
                    XDocument doc = XDocument.Load(s);
                    if (provider.AddInfoFromOfxHome(doc.Root))
                    {
                        UpdateCachedProfile(provider);
                    }
                }
            }
            catch
            {
                // todo: report error
            }
            return provider;
        }
    }
}