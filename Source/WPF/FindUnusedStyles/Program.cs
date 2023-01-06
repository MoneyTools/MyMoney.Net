using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace FindUnusedStyles
{
    internal class Program
    {
        private static XName emptyName;

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: FindUnusedStyles [--import dir]* dir");
            Console.WriteLine("Loads all .xaml files and reports any x:Key names that are unreferenced.");
            Console.WriteLine("Optional resource dictionaries can be imported via --import arguments.");
        }

        [STAThread]
        private static int Main(string[] args)
        {
            List<string> imports = new List<string>();
            string dir = null;
            for (int i = 0; i < args.Length; i++)
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

            Console.WriteLine("Checking folder: [{0}]", dir);

            Grid g = new Grid(); // ensure WPF types are loaded.
            new Program().Process(imports, dir);

            return 0;
        }

        private readonly Dictionary<string, Type> cache = new Dictionary<string, Type>();

        private void CacheWpfTypes()
        {
            var baseTypes = new Type[] { typeof(Grid), typeof(SolidColorBrush) };
            foreach (var baseType in baseTypes)
            {
                foreach (var t in baseType.Assembly.GetTypes())
                {
                    if (!t.IsGenericType)
                    {
                        this.cache[t.Name] = t;
                    }
                }
            }
        }

        private void Process(List<string> imports, string dir)
        {
            this.CacheWpfTypes();
            emptyName = XName.Get("_empty_");

            Styles global = new Styles(this.cache);

            foreach (var import in imports)
            {
                List<string> importFiles = new List<string>();
                this.FindXamlFiles(import, importFiles);
                foreach (var path in importFiles)
                {
                    var doc = this.LoadXaml(path);
                    if (doc != null)
                    {
                        this.FindStyles(null, doc.Root, null, global);
                    }
                }
            }

            List<string> files = new List<string>();
            this.FindXamlFiles(dir, files);

            List<XDocument> documents = new List<XDocument>();

            // Load global resource dictionaries first.
            foreach (var xaml in files)
            {
                var doc = this.LoadXaml(xaml);
                if (doc != null)
                {
                    Console.WriteLine("{0}: {1}", doc.Root.Name.LocalName, xaml);
                    if (doc.Root.Name.LocalName == "ResourceDictionary")
                    {
                        this.FindStyles(xaml, doc.Root, null, global);
                        this.CheckStyleReferences(xaml, doc.Root, null, global);
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
                    var location = doc.Annotation<string>();
                    Styles local = new Styles(location, global);
                    this.WalkResources(location, doc.Root, null, local);
                    local.ReportUnreferenced();
                }
            }

            global.ReportUnreferenced();

            Console.WriteLine();
            Console.WriteLine("SystemControl resource references");
            Console.WriteLine("=================================");
            foreach (var item in this.sysControlReferences.Keys)
            {
                Console.WriteLine(item);
            }
        }

        private void FindXamlFiles(string dir, List<string> files)
        {
            foreach (var file in Directory.GetFiles(dir, "*.xaml"))
            {
                files.Add(file);
            }

            foreach (var child in Directory.GetDirectories(dir))
            {
                this.FindXamlFiles(child, files);
            }
        }

        private readonly SortedDictionary<string, XName> sysControlReferences = new SortedDictionary<string, XName>();
        private readonly Dictionary<string, XElement> keyedResources = new Dictionary<string, XElement>();

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

        private const string XmlNsUri = "http://www.w3.org/2000/xmlns/";
        private const string XamlNsUri = "http://schemas.microsoft.com/winfx/2006/xaml";
        private const string XamlTypeName = "{http://schemas.microsoft.com/winfx/2006/xaml}Type";
        private const string XamlStaticName = "{http://schemas.microsoft.com/winfx/2006/xaml}Static";
        private const string ClrNamespacePrefix = "clr-namespace:";

        private XName QualifyName(string name, NamespaceScope scope)
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
                    var name = this.QualifyName(parts[0], scope);
                    var type = this.QualifyName(parts[1], scope);
                    if (name == XamlTypeName || name == XamlStaticName)
                    {
                        return type;
                    }
                    else
                    {
                        Program.WriteError("Unexpected target type: {0}", value);
                    }
                }
                return this.QualifyName(parts[0], scope);
            }
            else
            {
                return this.QualifyName(value, scope);
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
                            case "ListView":
                                return "ListViewItem";
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

        private bool HasParent(XElement e, string parentName)
        {
            var p = e.Parent;
            while (p != null)
            {
                if (p.Name.LocalName == parentName)
                {
                    return true;
                }

                p = p.Parent;
            }
            return false;
        }

        private void FindStyles(string filePath, XElement root, NamespaceScope scope, Styles styles)
        {
            var local = new NamespaceScope(scope);
            XName targetType = null;
            XName key = null;

            this.AddNamespaces(root, local);

            foreach (var a in root.Attributes())
            {
                if (a.Name.LocalName == "TargetType")
                {
                    targetType = this.ParseTargetType(a.Value, local);
                }
                else if (a.Name.LocalName == "Key" && a.Name.Namespace == XamlNsUri)
                {
                    key = this.ParseTargetType(a.Value, local);
                }
                else if (a.Name.LocalName == "DataType")
                {
                    // todo: check DataType references.
                }
            }

            if (key != null || targetType != null)
            {
                if (root.Name.LocalName == "ControlTemplate" && targetType != null && key == null
                    && this.HasParent(root, "Style"))
                {
                    // If this control template is inside a <Style> then it is not an independently
                    // addressable resource. 
                }
                else
                {
                    styles.AddStyle(filePath, key, targetType, root);
                }
            }

            // Check for any nested styles first.
            foreach (var e in root.Elements())
            {
                // If we find a nested .Resources within a resource then it's styles
                // are not global, we will create localstyles for this one later.
                if (!e.Name.LocalName.EndsWith(".Resources"))
                {
                    this.FindStyles(filePath, e, local, styles);
                }
            }
        }


        private void CheckStyleReferences(string filePath, XElement root, NamespaceScope scope, Styles styles)
        {
            var local = new NamespaceScope(scope);
            XName targetType = null;
            XName key = null;

            this.AddNamespaces(root, local);

            foreach (var a in root.Attributes())
            {
                if (a.Name.LocalName == "TargetType")
                {
                    targetType = this.ParseTargetType(a.Value, local);
                }
                else if (a.Name.LocalName == "Key" && a.Name.Namespace == XamlNsUri)
                {
                    key = this.ParseTargetType(a.Value, local);
                }
                else if (a.Name.LocalName == "DataType")
                {
                    // todo: check DataType references.
                }
                else if (a.Name.LocalName != "xmlns" && a.Name.Namespace != XmlNsUri)
                {
                    this.CheckReferences(this.GetTargetTypeName(root, a), a.Value, local, styles);
                }
            }
            Styles localStyles = null;
            // see if this element contains local styles
            foreach (var e in root.Elements())
            {
                // find any local styles.
                if (e.Name.LocalName.EndsWith(".Resources"))
                {
                    if (localStyles == null)
                    {
                        localStyles = new Styles(filePath + "+" + e.Name, styles);
                        styles = localStyles;
                    }
                    this.FindStyles(filePath, e, local, localStyles);
                }
            }

            // Check for any nested styles first.
            foreach (var e in root.Elements())
            {
                this.CheckStyleReferences(filePath, e, local, styles);
            }

            if (localStyles != null)
            {
                localStyles.ReportUnreferenced();
            }
        }

        private string StripClrPrefix(string s)
        {
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
                    scope.AddPrefix("", this.StripClrPrefix(a.Value));
                }
                else if (a.Name.Namespace == XmlNsUri)
                {
                    scope.AddPrefix(a.Name.LocalName, this.StripClrPrefix(a.Value));
                }
            }
        }

        private void WalkResources(string fileName, XElement root, NamespaceScope scope, Styles styles)
        {
            var local = new NamespaceScope(scope);
            this.AddNamespaces(root, local);

            string codeRef = null;
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
                else if (a.Name.LocalName.Contains("CodeRef"))
                {
                    codeRef = a.Value;
                }
                else if (a.Name.LocalName != "xmlns" && a.Name.Namespace != XmlNsUri)
                {
                    this.CheckReferences(this.GetTargetTypeName(root, a), a.Value, local, styles);
                }
            }

            Styles localStyles = null;
            // see if this element contains local styles
            foreach (var e in root.Elements())
            {
                // find any local styles.
                if (e.Name.LocalName.EndsWith(".Resources"))
                {
                    if (localStyles == null)
                    {
                        localStyles = new Styles(fileName + "+" + e.Name, styles);
                        styles = localStyles;
                    }
                    this.FindStyles(fileName, e, local, localStyles);
                }
            }

            if (!string.IsNullOrEmpty(codeRef))
            {
                // ah then the named styles are referenced from code, so record that fact
                foreach (var part in codeRef.Split(','))
                {
                    if (string.IsNullOrWhiteSpace(part))
                    {
                        continue;
                    }
                    XName reference = this.QualifyName(part.Trim(), local);
                    var style = styles.FindStyle(null, reference);
                    if (style == null)
                    {
                        // might have been a TargetTyped resource
                        XName targetType = styles.FindTargetType(reference);
                        if (targetType != null)
                        {
                            style = styles.FindStyle(targetType, reference);
                            if (style == null)
                            {
                                // might be a unnamed key TargetType only match
                                style = styles.FindStyle(targetType, emptyName);
                            }
                        }
                        if (style == null)
                        {
                            Program.WriteError("CodeRef {0} not found", reference.ToString());
                        }
                    }
                }
            }

            // record possible reference to a TargetType of the matching element name.
            styles.FindStyle(this.GetTargetTypeName(root, null), Program.emptyName);

            // Now we have the "usage" of styles, either something in a UserControl, or a ControlTemplate in a ResourceDictionary.
            foreach (var e in root.Elements())
            {
                this.WalkResources(fileName, e, local, styles);
            }

            if (localStyles != null)
            {
                localStyles.ReportUnreferenced();
            }
        }

        private class BindingInfo
        {
            public XName Path;
            public string RelativeSource;
            public XName Converter;
            public string ConverterParameter;
            public string Mode;
            public string ElementName;
            public string FallbackValue;
            public string StringFormat;
            public string AncestorType;
            public string Source;
        }

        private XName ParseResourceReference(string value, NamespaceScope scope)
        {
            value = value.Trim(BindingChars).Trim();
            string[] parts = value.Split(WhitespaceChars, StringSplitOptions.RemoveEmptyEntries);
            var name = this.QualifyName(parts[0], scope);
            if (name == "DynamicResource" || name == "{http://schemas.modernwpf.com/2019}DynamicColor" ||
                name == "StaticResource" || name == XamlStaticName)
            {
                int i = value.IndexOfAny(WhitespaceChars);
                if (i < 0)
                {
                    Console.WriteLine("???");
                }
                else
                {
                    var resourceName = value.Substring(i).Trim();
                    return this.ParseTargetType(resourceName, scope);
                }
            }
            else if (name == "Binding" || name == "TemplateBinding")
            {
                var args = value.Split(',');
                foreach (var arg in args)
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
                            // CheckReferences(element, nameValue[1], local, localStyles);
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
            return null;
        }

        private XName QualifyPath(string value, NamespaceScope scope)
        {
            if (value.StartsWith("("))
            {
                value = value.Trim('(', ')').Trim();
            }
            var pos = value.LastIndexOf('.');
            if (pos >= 0)
            {
                value = value.Substring(pos + 1);
            }
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            return this.QualifyName(value, scope);
        }

        private BindingInfo ParseBinding(string value, NamespaceScope scope)
        {
            BindingInfo bindingInfo = new BindingInfo();
            value = value.Trim(BindingChars).Trim();
            var parts = value.Split(',');
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("Binding"))
                {
                    trimmed = trimmed.Substring("Binding".Length).Trim();
                }
                else if (trimmed.StartsWith("TemplateBinding"))
                {
                    trimmed = trimmed.Substring("TemplateBinding".Length).Trim();
                }

                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var nameValue = trimmed.Split('=');
                if (nameValue.Length == 1)
                {
                    // {Binding IsClosed,
                    bindingInfo.Path = this.QualifyPath(nameValue[0], scope);
                }
                else
                {
                    switch (nameValue[0])
                    {
                        case "Path":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.Path = this.QualifyPath(nameValue[1].Trim(), scope);
                            }
                            break;
                        case "Converter":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.Converter = this.ParseResourceReference(nameValue[1].Trim(), scope);
                            }
                            break;
                        case "ConverterParameter":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.ConverterParameter = nameValue[1];
                            }
                            break;
                        case "RelativeSource":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.RelativeSource = nameValue[1];
                            }
                            break;
                        case "Mode":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.Mode = nameValue[1];
                            }
                            break;
                        case "ElementName":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.ElementName = nameValue[1];
                            }
                            break;
                        case "FallbackValue":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.FallbackValue = nameValue[1];
                            }
                            break;
                        case "StringFormat":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.StringFormat = nameValue[1];
                            }
                            break;
                        case "AncestorType":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.AncestorType = nameValue[1];
                            }
                            break;
                        case "Source":
                            if (nameValue.Length > 1)
                            {
                                bindingInfo.Source = nameValue[1];
                            }
                            break;
                        case "ValidatesOnDataErrors":
                        case "ValidatesOnExceptions":
                            break;
                        default:
                            break;
                    }
                }
            }
            return bindingInfo;
        }

        private static readonly char[] BindingChars = new char[] { '{', '}' };
        private static readonly char[] WhitespaceChars = new char[] { ' ', '\t', '\r', '\n' };

        private void CheckReferences(XName element, string value, NamespaceScope local, Styles localStyles)
        {
            XName reference = null;
            if (value.StartsWith("{Binding") || value.StartsWith("{TemplateBinding"))
            {
                var binding = this.ParseBinding(value, local);
                reference = binding.Converter;
                if (binding.Source != null)
                {
                    var sourceRef = this.ParseResourceReference(binding.Source, local);
                    var style = localStyles.FindStyle(element, sourceRef);
                    if (style == null && !this.Whitelisted(sourceRef))
                    {
                        Program.WriteError("Resource {0} not found", sourceRef.ToString());
                    }
                }
            }
            else if (value.StartsWith("{"))
            {
                reference = this.ParseResourceReference(value, local);
            }
            if (reference != null)
            {
                var style = localStyles.FindStyle(element, reference);
                if (style == null && !this.Whitelisted(reference))
                {
                    Program.WriteError("Resource {0} not found", reference.ToString());
                }
            }
        }

        private bool Whitelisted(XName reference)
        {
            switch (reference.LocalName)
            {
                case "SystemAccentColor":
                case "SystemAccentColorLight1":
                case "DefaultTextBoxStyle":
                case "DefaultDataGridColumnHeaderStyle":
                case "SymbolThemeFontFamily":
                case "SystemAccentColorDark1":
                case "DefaultTreeViewItemStyle":
                case "DefaultDataGridCellStyle":
                case "DefaultDataGridStyle":
                case "DefaultComboBoxStyle":
                case "DefaultButtonStyle":
                case "DefaultLabelStyle":
                case "DefaultDatePickerStyle":
                case "DefaultListBoxItemStyle":
                case "Visibility.Visible":
                    return true;
                default:
                    break;
            }
            return false;
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

        private class NamespaceScope
        {
            public NamespaceScope Parent;
            public Dictionary<string, string> Namespaces = new Dictionary<string, string>();

            public NamespaceScope() { }

            public NamespaceScope(NamespaceScope parent)
            {
                this.Parent = parent;
            }

            public void AddPrefix(string prefix, string uri)
            {
                this.Namespaces[prefix] = uri;
            }

            public string FindPrefix(string prefix)
            {
                if (this.Namespaces.ContainsKey(prefix))
                {
                    return this.Namespaces[prefix];
                }
                else if (this.Parent != null)
                {
                    return this.Parent.FindPrefix(prefix);
                }
                return null;
            }
        }

        private class StyleInfo
        {
            public string FileName;
            public XElement Element;
            public XName Key;
            public XName TargetType;
            public long RefCount;
        }

        private class Styles
        {
            public Styles Parent;
            public string FileName;
            public Dictionary<XName, StyleInfo> keys = new Dictionary<XName, StyleInfo>();
            public Dictionary<XName, Dictionary<XName, StyleInfo>> targetTypes = new Dictionary<XName, Dictionary<XName, StyleInfo>>();
            private readonly Dictionary<string, Type> cache;

            public Styles(Dictionary<string, Type> cache) { this.cache = cache; }

            public Styles(string fileName, Styles parent)
            {
                this.FileName = fileName;
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
                    if (!this.targetTypes.TryGetValue(targetType, out Dictionary<XName, StyleInfo> d))
                    {
                        d = new Dictionary<XName, StyleInfo>();
                        this.targetTypes[targetType] = d;
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

            private IEnumerable<XName> GetWpfChildTypes(XName typeName)
            {
                var type = this.GetWpfType(typeName.LocalName);
                if (type != null)
                {
                    switch (type.Name)
                    {
                        case "DataGrid":
                            yield return "DataGridRow";
                            yield return "DataGridCell";
                            yield return "DataGridColumn";
                            yield return "DataGridRowHeader";
                            yield return "DataGridTextColumn";
                            yield return "DataGridColumnHeader";
                            break;
                        case "ListBox":
                            yield return "ListBoxItem";
                            break;
                        case "ListView":
                            yield return "ListViewItem";
                            break;
                        case "GridView":
                            yield return "GridViewItem";
                            yield return "GridViewColumn";
                            yield return "GridViewColumnHeader";
                            break;
                        case "ComboBox":
                            yield return "ComboBoxItem";
                            break;
                        case "ContextMenu":
                        case "Menu":
                            yield return "MenuItem";
                            yield return "Separator";
                            break;
                        case "ToolBar":
                            yield return "ToolBarItem";
                            yield return "ToolBarTray";
                            break;
                        case "TabControl":
                            yield return "TabItem";
                            break;
                        case "FlowDocument":
                            yield return "Table";
                            yield return "TableCell";
                            yield return "TextBlock";
                            break;
                        case "ContentPresenter":
                            yield return "TextBlock";
                            break;
                    }
                }
            }

            private Type GetWpfType(string typeName)
            {
                if (this.cache.TryGetValue(typeName, out var type))
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
                if (this.targetTypes.TryGetValue(name, out Dictionary<XName, StyleInfo> d))
                {
                    if (d.ContainsKey(key))
                    {
                        var si = d[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }
                else if (this.targetTypes.TryGetValue(name, out Dictionary<XName, StyleInfo> d2))
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
                XElement result = this.LookupTargetType(name, key);
                if (result != null)
                {
                    return result;
                }
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    // key is scoped to this type.
                    name = XName.Get(type.Name, type.Namespace);
                    result = this.LookupTargetType(name, key);
                    if (result != null)
                    {
                        return result;
                    }
                }


                // check base types!
                if (type.BaseType != null)
                {
                    result = this.FindTargetType(type.BaseType, key);
                }
                return result;
            }

            private XElement FindTargetType(XName typeName, XName key)
            {
                // key is scoped to this type.
                if (this.targetTypes.TryGetValue(typeName, out Dictionary<XName, StyleInfo> d))
                {
                    if (d.ContainsKey(key))
                    {
                        var si = d[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }
                else if (this.targetTypes.TryGetValue(typeName.LocalName, out Dictionary<XName, StyleInfo> d2))
                {
                    if (d2.ContainsKey(key))
                    {
                        var si = d2[key];
                        si.RefCount++;
                        return si.Element;
                    }
                }

                // check base types!
                Type t = this.GetWpfType(typeName.LocalName);
                if (t != null)
                {
                    return this.FindTargetType(t, key);
                }
                return null;
            }

            public XElement FindStyle(XName typename, XName key)
            {
                // Check more specific target type match first.
                if (typename != null)
                {
                    if (key == Program.emptyName)
                    {
                        // then this type reference might imply some child type references,
                        // for example a <DataGrid> implies we will be using styles defined
                        // for <DataGridRow>, <DataGridCell>, etc.
                        foreach (XName childType in this.GetWpfChildTypes(typename))
                        {
                            this.FindStyle(childType, Program.emptyName);
                        }
                    }
                    XElement e = this.FindTargetType(typename, key);
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
                if (this.Parent != null)
                {
                    return this.Parent.FindStyle(typename, key);
                }
                return null;
            }

            internal void ReportUnreferenced()
            {
                foreach (var key in this.keys.Keys)
                {
                    var si = this.keys[key];
                    if (si.RefCount == 0 && si.FileName != null && !this.WhiteListResource(si.FileName))
                    {
                        Program.WriteWarning("Unreferenced style {0} from {1}", si.Key.ToString(), si.FileName);
                    }
                }

                foreach (var targetType in this.targetTypes.Keys)
                {
                    var map = this.targetTypes[targetType];
                    foreach (var key in map.Keys)
                    {
                        var si = map[key];
                        if (si.RefCount == 0 && si.FileName != null && !this.WhiteListResource(si.FileName))
                        {
                            if (si.Key == emptyName && this.WhiteListType(si.TargetType))
                            {
                                // skip it.
                            }
                            else
                            {
                                Program.WriteWarning("Unreferenced style {0} for target type {1} from {2}", si.Key.ToString(), si.TargetType.ToString(), si.FileName);
                            }
                        }
                    }
                }
            }

            private bool WhiteListType(XName targetType)
            {
                switch (targetType.LocalName)
                {
                    case "TextBlock":
                        return true;
                }
                return false;
            }

            private bool WhiteListResource(string filePath)
            {
                string filename = System.IO.Path.GetFileName(filePath);
                switch (filename)
                {
                    case "Compact.xaml":
                        return true;
                }
                return false;
            }

            internal XName FindTargetType(XName key)
            {
                foreach (var targetType in this.targetTypes.Keys)
                {
                    var map = this.targetTypes[targetType];
                    if (map.ContainsKey(key))
                    {
                        return targetType;
                    }
                }
                if (this.Parent != null)
                {
                    return this.Parent.FindTargetType(key);
                }
                return null;
            }
        }
    }
}
