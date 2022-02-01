using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Media;

namespace FindUnusedStyles
{
    class Program
    {
        static XName emptyName;

        static void PrintUsage()
        {
            Console.WriteLine("Usage: FindUnusedStyles [--import dir]* dir");
            Console.WriteLine("Loads all .xaml files and reports any x:Key names that are unreferenced.");
            Console.WriteLine("Optional resource dictionaries can be imported via --import arguments.");
        }
        
        [STAThread]
        static int Main(string[] args)
        {
            List<string> imports = new List<string>();
            string dir = null;
            for(int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--import" && i + 1 < args.Length)
                {
                    imports.Add(args[++i]);
                }
                else if (dir == null)
                {
                    dir = arg;
                }
                else
                {
                    Console.WriteLine("Too many directories provided");
                    PrintUsage();
                    return 1;
                }
            }

            if (dir == null)
            {
                PrintUsage();
                return 1;
            }

            Grid g = new Grid(); // ensure WPF types are loaded.
            new Program().Process(imports, dir);

            return 0;
        }


        Dictionary<string, Type> cache = new Dictionary<string, Type>();

        private void CacheWpfTypes()
        {
            var baseTypes = new Type[] { typeof(Grid), typeof(SolidColorBrush) };
            foreach (var baseType in baseTypes)
            {
                foreach (var t in baseType.Assembly.GetTypes())
                {
                    if (!t.IsGenericType)
                    {
                        cache[t.Name] = t;
                    }
                }
            }
        }

        private void Process(List<string> imports, string dir)
        {
            CacheWpfTypes();
            emptyName = XName.Get("_empty_");

            Styles global = new Styles(cache);

            foreach (var import in imports)
            {
                List<string> importFiles = new List<string>();
                FindXamlFiles(import, importFiles);
                foreach (var path in importFiles)
                {
                    var doc = LoadXaml(path);
                    if (doc != null)
                    {
                        // don't record import file name as a signal we don't care about
                        // unreferenced styles in that import.
                        FindStyles(null, doc.Root, null, global, false);
                    }
                }
            }

            List<string> files = new List<string>();
            FindXamlFiles(dir, files);

            List<XDocument> documents = new List<XDocument>();
            

            // Load global resource dictionaries first.
            foreach (var xaml in files)
            {
                var doc = LoadXaml(xaml);
                if (doc != null)
                {
                    Console.WriteLine("{0}: {1}", doc.Root.Name.LocalName, xaml);
                    if (doc.Root.Name.LocalName == "ResourceDictionary")
                    {
                        FindStyles(xaml, doc.Root, null, global, true);
                    }
                    else
                    {
                        doc.AddAnnotation(xaml);
                        documents.Add(doc);
                    }
                }
            }

            // Now check the references.
            foreach (var doc in documents)
            {
                if (doc.Root.Name.LocalName != "ResourceDictionary")
                {
                    var a = doc.Root.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"));
                    Styles local = new Styles(global);
                    var location = doc.Annotation<string>();
                    FindStyles(location, doc.Root, null, local, true);
                    WalkResources(location, doc.Root, null, local);
                    local.ReportUnreferenced();
                }
            }

            global.ReportUnreferenced();

            Console.WriteLine();
            Console.WriteLine("SystemControl resource references");
            Console.WriteLine("=================================");
            foreach (var item in sysControlReferences.Keys)
            {
                Console.WriteLine(item);
            }
        }

        private void FindXamlFiles(string dir, List<string> files)
        {
            foreach(var file in Directory.GetFiles(dir, "*.xaml"))
            {
                files.Add(file);
            }

            foreach(var child in Directory.GetDirectories(dir))
            {
                FindXamlFiles(child, files);
            }
        }

        SortedDictionary<string, XName> sysControlReferences = new SortedDictionary<string, XName>();
        Dictionary<string, XElement> keyedResources = new Dictionary<string, XElement>();

        private XDocument LoadXaml(string xaml)
        {
            try
            {
                return XDocument.Load(xaml);                                 
            } 
            catch (Exception ex)
            {
                WriteError("Error loading {0}: {1}", xaml, ex.Message);
            }
            return null;
        }

        const string XmlNsUri = "http://www.w3.org/2000/xmlns/";
        const string XamlNsUri = "http://schemas.microsoft.com/winfx/2006/xaml";
        const string XamlTypeName = "{http://schemas.microsoft.com/winfx/2006/xaml}Type";
        const string XamlStaticName = "{http://schemas.microsoft.com/winfx/2006/xaml}Static";
        const string ClrNamespacePrefix = "clr-namespace:";

        XName QualifyName(string name, NamespaceScope scope)
        {
            var parts = name.Split(':');
            if (parts.Length == 2)
            {
                var prefix = parts[0];
                var localName = parts[1];
                var ns = scope.FindPrefix(prefix);
                if (ns != null)
                {
                    if (ns.StartsWith(ClrNamespacePrefix))
                    {
                        ns = ns.Substring(ClrNamespacePrefix.Length);
                    }
                    int i = ns.IndexOf(";assembly=");
                    if (i > 0)
                    {
                        ns = ns.Substring(0, i);
                    }
                    return XName.Get(localName, ns);
                }
            }
            return XName.Get(name);
        }
        
        private XName ParseTargetType(string value, NamespaceScope scope)
        {
            if (value.StartsWith("{"))
            {
                // e.g. "{x:Type c:CloseBox}"
                value = value.Trim(BindingChars);
                var parts = value.Split(WhitespaceChars, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var name = QualifyName(parts[0], scope);
                    var type = QualifyName(parts[1], scope);
                    if (name == XamlTypeName || name == XamlStaticName)
                    {
                        return type;
                    }
                    else
                    {
                        Program.WriteError("Unexpected target type: {0}", value);
                    }
                }
                return QualifyName(parts[0], scope);
            }
            else
            {
                return QualifyName(value, scope);
            }
        }

        private XName GetTargetTypeName(XElement root, XAttribute a)
        {
            if (a != null)
            {
                switch (a.Name.LocalName)
                {
                    case "CellStyle":
                        return "DataGridCell";
                    case "RowStyle":
                        return "DataGridRow";
                    case "TextBlockStyle":
                        switch (root.Name.LocalName)
                        {
                            case "TransactionNumericColumn":
                                return "TextBlock";
                        }
                        break;
                    case "HeaderStyle":
                        switch (root.Name.LocalName)
                        {
                            case "DataGridTemplateColumn":
                                return "DataGridColumnHeader";
                        }
                        break;
                    case "HeaderContainerStyle":
                        switch (root.Name.LocalName)
                        {
                            case "GridViewColumn":
                                return "GridViewColumnHeader";
                            default:
                                Console.WriteLine("???");
                                break;
                        }
                        break;
                    case "ContainerStyle":
                        switch (root.Name.LocalName)
                        {
                            case "GroupStyle":
                                return "GroupItem";
                        }
                        break;
                    case "ItemContainerStyle":
                        switch (root.Name.LocalName)
                        {
                            case "TreeView":
                                return "TreeViewItem";
                            case "ComboBox":
                            case "FilteringComboBox":
                                return "ComboBoxItem";
                            case "ListBox":
                                return "ListBoxItem";
                            default:
                                Console.WriteLine("???");
                                break;
                        }
                        break;
                }
            }

            switch (root.Name.LocalName)
            {
                case "TextBlockStyle":
                    return "TextBlock";
            }
            if (root.Name.NamespaceName.StartsWith(ClrNamespacePrefix))
            {
                return XName.Get(root.Name.LocalName, root.Name.NamespaceName.Substring(ClrNamespacePrefix.Length));
            }

            return root.Name;
        }

        private void FindStyles(string filePath, XElement root, NamespaceScope scope, Styles styles, bool checkRefs)
        {
            var local = new NamespaceScope(scope);
            XName targetType = null;
            XName key = null;

            AddNamespaces(root, local);

            foreach (var a in root.Attributes())
            {
                if (a.Name.LocalName == "TargetType")
                {
                    targetType = ParseTargetType(a.Value, local);
                }
                else if (a.Name.LocalName == "Key" && a.Name.Namespace == XamlNsUri)
                {
                    key = ParseTargetType(a.Value, local);
                }
                else if (a.Name.LocalName == "DataType")
                {
                    // todo: check DataType references.
                }
                else if (checkRefs && a.Name.LocalName != "xmlns" && a.Name.Namespace != XmlNsUri && checkRefs)
                {
                    CheckReferences(GetTargetTypeName(root, a), a.Value, local, styles);
                }
            }

            if (key != null || targetType != null)
            {
                styles.AddStyle(filePath, key, targetType, root);
            }

            bool isDict = root.Name.LocalName == "ResourceDictionary" || root.Name.LocalName.EndsWith(".Resources");

            // Check for any nested styles first.
            foreach (var e in root.Elements())
            {
                if (isDict || e.Name.LocalName == "ResourceDictionary" || e.Name.LocalName.EndsWith(".Resources"))
                {
                    FindStyles(filePath, e, local, styles, checkRefs);
                }
            }
        }

        private string StripClrPrefix(string s) { 
            if (s.StartsWith(ClrNamespacePrefix))
            {
                return s.Substring(ClrNamespacePrefix.Length);
            }
            return s;
        }


        private void AddNamespaces(XElement e, NamespaceScope scope)
        {
            foreach (var a in e.Attributes())
            {
                if (a.Name.LocalName == "xmlns")
                {
                    scope.AddPrefix("", StripClrPrefix(a.Value));
                }
                else if (a.Name.Namespace == XmlNsUri)
                {
                    scope.AddPrefix(a.Name.LocalName, StripClrPrefix(a.Value));
                }
            }
        }

        private void WalkResources(string fileName, XElement root, NamespaceScope scope, Styles styles)
        {
            var local = new NamespaceScope(scope);
            AddNamespaces(root, local);

            foreach (var a in root.Attributes())
            {
                if (a.Name.LocalName == "TargetType")
                {
                    // should have already been found in FindStyles.
                }
                else if (a.Name.LocalName == "Key" && a.Name.Namespace == XamlNsUri)
                {
                    // should have already been found in FindStyles.
                }
                else if (a.Name.LocalName == "DataType")
                {
                    // todo: check type references
                }
                else if (a.Name.LocalName != "xmlns" && a.Name.Namespace != XmlNsUri)
                {
                    CheckReferences(GetTargetTypeName(root, a), a.Value, local, styles);
                }
            }

            // record possible reference to a TargetType of the matching element name.
            styles.FindStyle(GetTargetTypeName(root, null), Program.emptyName);

            // Now we have the "usage" of styles, either something in a UserControl, or a ControlTemplate in a ResourceDictionary.
            foreach (var e in root.Elements())
            {
                // find any local styles.
                if (e.Name.LocalName.EndsWith(".Resources"))
                {
                    FindStyles(fileName, e, local, styles, true);
                }
            }

            // Now we have the "usage" of styles, either something in a UserControl, or a ControlTemplate in a ResourceDictionary.
            foreach (var e in root.Elements())
            {
                // create local scope for any resources defined in these controls.
                var localStyles = styles;
                if (!e.Name.LocalName.EndsWith(".Resources"))
                {
                    localStyles = new Styles(styles);
                }
                WalkResources(fileName, e, local, localStyles);
                if (localStyles != styles)
                {
                    localStyles.ReportUnreferenced();
                }
            }
        }

        static char[] BindingChars = new char[] { '{', '}' };
        static char[] WhitespaceChars = new char[] { ' ', '\t', '\r', '\n' };

        private void CheckReferences(XName element, string value, NamespaceScope local, Styles localStyles)
        {
            if (value.StartsWith("{"))
            {
                value = value.Trim(BindingChars);
                string[] parts = value.Split(WhitespaceChars, StringSplitOptions.RemoveEmptyEntries);
                var name = QualifyName(parts[0], local);
                if (name == "DynamicResource" || name == "StaticResource" || name == "{http://schemas.microsoft.com/winfx/2006/xaml}Static")
                {
                    int i = value.IndexOfAny(WhitespaceChars);
                    if (i < 0)
                    {
                        Console.WriteLine("???");
                    }
                    else
                    {
                        var reference = ParseTargetType(value.Substring(i).Trim(), local); 
                        var style = localStyles.FindStyle(element, reference);
                        if (style == null)
                        {
                            Program.WriteError("Resource {0} not found", reference.ToString());
                        }
                    }
                }
                else if (name == "Binding" || name == "TemplateBinding")
                {
                    var args = value.Split(',');
                    foreach(var arg in args)
                    {
                        var nameValue = arg.Trim().Split('=');
                        if (nameValue.Length == 2 && nameValue[0] != "StringFormat")
                        {
                            if (nameValue[0] == "AncestorType")
                            {
                                // todo: check type references.
                            }
                            else
                            {
                                CheckReferences(element, nameValue[1], local, localStyles);
                            }
                        }
                    }
                }
                else if (name == "RelativeSource")
                {
                    if (parts.Length == 2)
                    {
                        string reference = parts[1];
                        if (reference != "Self" && reference != "TemplatedParent" && reference != "FindAncestor")
                        {
                            Console.WriteLine("???");
                        }
                    }
                }
                else if (name == "{http://schemas.microsoft.com/winfx/2006/xaml}Null")
                {
                    // do nothing
                }
                else if (name == "{http://schemas.microsoft.com/expression/blend/2008}DesignInstance")
                {
                    // todo?
                }
                else
                {
                    Console.WriteLine("???");
                }
            }
        }

        public static void WriteError(string msg, params string[] args)
        {
            string formatted = msg;
            if (args.Length != 0)
            {
                formatted = string.Format(msg, args);
            }
            var saved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(formatted);
            Console.ForegroundColor = saved;
        }

        public static void WriteWarning(string msg, params string[] args)
        {
            string formatted = msg;
            if (args.Length != 0)
            {
                formatted = string.Format(msg, args);
            }
            var saved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(formatted);
            Console.ForegroundColor = saved;
        }

        class NamespaceScope
        {
            public string Prefix;
            public string Namespace;
            public NamespaceScope Parent;
            public Dictionary<string, string> Namespaces = new Dictionary<string, string>();

            public NamespaceScope() { }

            public NamespaceScope(NamespaceScope parent)
            {
                this.Parent = parent;
            }

            public void AddPrefix(string prefix, string uri)
            {
                Namespaces[prefix] = uri;
            }

            public string FindPrefix(string prefix)
            {
                if (this.Namespaces.ContainsKey(prefix)) {
                    return this.Namespaces[prefix];
                }
                else if (this.Parent != null)
                {
                    return this.Parent.FindPrefix(prefix);
                }
                return null;
            }
        }

        class StyleInfo
        {
            public string FileName;
            public XElement Element;
            public XName Key;
            public XName TargetType;
            public long RefCount;
        }

        class Styles
        {
            public Styles Parent;
            public Dictionary<XName, StyleInfo> keys = new Dictionary<XName, StyleInfo>();
            public Dictionary<XName, Dictionary<XName, StyleInfo>> targetTypes = new Dictionary<XName, Dictionary<XName, StyleInfo>>();
            Dictionary<string, Type> cache;

            public Styles(Dictionary<string, Type> cache) { this.cache = cache; }

            public Styles(Styles parent)
            {
                this.Parent = parent;
                this.cache = parent.cache;
            }

            internal void AddStyle(string fileName, XName key, XName targetType, XElement e)
            {
                if (key == null)
                {
                    key = Program.emptyName; // style matches all target types.
                }
                Dictionary<XName, StyleInfo> s = this.keys;
                if (targetType != null)
                {
                    // key is scoped to this type.
                    if (!targetTypes.TryGetValue(targetType, out Dictionary<XName, StyleInfo> d))
                    {
                        d = new Dictionary<XName, StyleInfo>();
                        targetTypes[targetType] = d;
                    }
                    s = d;
                }
                if (s.ContainsKey(key))
                {
                    if (targetType == null)
                    {
                        // Program.WriteError("Duplicate key {0}", key.ToString());
                    }
                    else if (key == Program.emptyName)
                    {
                        // Program.WriteError("Duplicate unnamed resource for TargetType {0}", targetType.ToString());
                    }
                    else
                    {
                        // Program.WriteError("Duplicate key {0} for TargetType {1}", key.ToString(), targetType.ToString());
                    }
                }
                else
                {
                    s[key] = new StyleInfo()
                    {
                        FileName = fileName,
                        Element = e,
                        TargetType = targetType,
                        Key = key,
                        RefCount = 0
                    };
                }
            }

            private Type GetWpfType(string typeName)
            {
                if (cache.TryGetValue(typeName, out var type))
                {
                    return type;
                }

                switch (typeName)
                {
                    case "DynamicResource":
                        return null;
                    // this are a ModernWpf types.
                    case "SplitButton":
                        return typeof(ContentControl);
                    // this are a Money.Net types.
                    case "TrendGraph":
                        // this is a Money.Net type.
                        return typeof(UserControl);
                    case "CustomizableButton":
                        // this is a Money.Net type.
                        return typeof(Button);
                    case "MoneyDataGrid":
                        // this is a Money.Net type.
                        return typeof(DataGrid);
                    case "FilteringComboBox":
                        return typeof(ComboBox);
                    case "BaseDialog":
                        return typeof(Window);
                    case "TransactionNumericColumn":
                        return typeof(DataGridColumn);
                }
                return null;
            }

            private XElement LookupTargetType(XName name, XName key)
            {
                // key is scoped to this type.
                if (targetTypes.TryGetValue(name, out Dictionary<XName, StyleInfo> d))
                {
                    if (d.ContainsKey(key))
                    {
                        var si = d[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }
                else if (targetTypes.TryGetValue(name, out Dictionary<XName, StyleInfo> d2))
                {
                    if (d2.ContainsKey(key))
                    {
                        var si = d2[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }
                return null;
            }

            private XElement FindTargetType(Type type, XName key)
            {
                XName name = type.Name;
                // key is scoped to this type.
                XElement result = LookupTargetType(name, key);
                if (result != null)
                {
                    return result;
                }
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    // key is scoped to this type.
                    name = XName.Get(type.Name, type.Namespace);
                    result = LookupTargetType(name, key);
                    if (result != null)
                    {
                        return result;
                    }
                }


                // check base types!
                if (type.BaseType != null)
                {
                    result = FindTargetType(type.BaseType, key);
                }
                return result;
            }

            private XElement FindTargetType(XName typeName, XName key)
            {
                // key is scoped to this type.
                if (targetTypes.TryGetValue(typeName, out Dictionary<XName, StyleInfo> d))
                {
                    if (d.ContainsKey(key))
                    {
                        var si = d[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }
                else if (targetTypes.TryGetValue(typeName.LocalName, out Dictionary<XName, StyleInfo> d2))
                {
                    if (d2.ContainsKey(key))
                    {
                        var si = d2[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }

                // check base types!
                Type t = GetWpfType(typeName.LocalName);
                if (t != null)
                {
                    return FindTargetType(t, key);
                }
                return null;
            }

            public XElement FindStyle(XName typename, XName key)
            {
                // Check more specific target type match first.
                if (typename != null)
                {
                    XElement e = FindTargetType(typename, key);
                    if (e != null)
                    {
                        return e;
                    }
                }
                if (this.keys.ContainsKey(key))
                {
                    var si = this.keys[key];
                    si.RefCount++;
                    return si.Element;
                }
                if (Parent != null)
                {
                    return Parent.FindStyle(typename, key);
                }
                return null;
            }

            internal void ReportUnreferenced()
            {
                foreach (var key in keys.Keys)
                {
                    var si = keys[key];
                    if (si.RefCount == 0 && si.FileName != null)
                    {
                        Program.WriteWarning("Unreferenced style '{0}' from {1}", si.Key.ToString(), si.FileName);
                    }
                }

                foreach (var targetType in targetTypes.Keys)
                {
                    var map = targetTypes[targetType];
                    foreach(var key in map.Keys)
                    {
                        var si = map[key];
                        if (si.RefCount == 0 && si.FileName != null)
                        {
                            Program.WriteWarning("Unreferenced style '{0}' for target type '{1}' from {2}", si.Key.ToString(), si.TargetType.ToString(), si.FileName);
                        }
                    }
                }
            }
        }
    }
}
