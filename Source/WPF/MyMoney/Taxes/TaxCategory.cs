using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Taxes
{
    public enum SortOrder
    {
        None, // 'N'
        SortByAsset, // 'A'
        SortByCategory, // 'C'
        SortByPayee, // 'P'
    }

    public class TaxForm
    {
        /// <summary>
        /// The tax form name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The categories associated with this form
        /// </summary>
        public List<TaxCategory> Categories { get; set; }
    }

    /// <summary>
    /// This class describes a tax category supported by TXF format
    /// </summary>
    public class TaxCategory
    {
        /// <summary>
        /// A valud from 0-2.  0 means this is a Form, not a valid category.
        /// 1 means it is a section in a form, also not a valid category.
        /// 2 means it is a valid category to use in a TXF export.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// The tax form this category is associated with.
        /// </summary>
        public string FormName { get { return (this.Form != null) ? this.Form.Name : ""; } }

        /// <summary>
        /// The tax form this category is associated with.
        /// </summary>
        public TaxForm Form { get; set; }

        /// <summary>
        /// The unique reference number
        /// </summary>
        public int RefNum { get; set; }

        /// <summary>
        /// The name of the category or form.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether multiple records can be exported for this category
        /// </summary>
        public bool MultipleAllowed { get; set; }

        /// <summary>
        /// Get the sort order required for this category
        /// </summary>
        public SortOrder SortOrder { get; set; }

        /// <summary>
        /// Get the default sign for this category.  
        /// 1 for 'I', -1 for 'E' and 0 for 'B' or 'S' meaning no default.
        /// </summary>
        public int DefaultSign { get; set; }

        /// <summary>
        /// The record format, valid values are 0-6.
        /// </summary>
        public int RecordFormat { get; set; }

        /// <summary>
        /// Reference to line in an IRS form.
        /// </summary>
        public string IrsRef { get; set; }

        internal static TaxCategory Parse(string line)
        {
            List<string> tokens = GetTokens(line);
            if (tokens.Count != 8)
            {
                return null;
            }
            return new TaxCategory()
            {
                Level = ParseInt(tokens[0]),
                RefNum = ParseInt(tokens[1]),
                Name = tokens[2],
                MultipleAllowed = tokens[3] == "Y",
                SortOrder = ParseSortOrder(tokens[4]),
                DefaultSign = ParseSign(tokens[5]),
                RecordFormat = ParseInt(tokens[6]),
                IrsRef = tokens[7]
            };
        }

        internal static int ParseInt(string s)
        {
            int i = 0;
            int.TryParse(s, out i);
            return i;
        }

        internal static List<string> GetTokens(string line)
        {
            List<string> tokens = new List<string>();
            string token;

            StringBuilder sb = new StringBuilder();
            for (int i = 0, n = line.Length; i < n; i++)
            {
                bool push = i == n - 1;

                char ch = line[i];
                if (ch == '"')
                {
                    int j = line.IndexOf('"', i + 1);
                    if (j > i + 1)
                    {
                        push = true;
                        sb.Append(line.Substring(i + 1, j - i - 1));
                        i = j;
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else if (char.IsWhiteSpace(ch))
                {
                    push = true;
                }
                else
                {
                    sb.Append(ch);
                }

                if (push)
                {
                    token = sb.ToString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        tokens.Add(token);
                    }
                    sb.Length = 0;
                }
            }

            return tokens;
        }

        internal static SortOrder ParseSortOrder(string s)
        {
            switch (s)
            {
                case "A":
                    return SortOrder.SortByAsset;
                case "C":
                    return SortOrder.SortByCategory;
                case "P":
                    return SortOrder.SortByPayee;
                default:
                case "N":
                    return SortOrder.None;
            }
        }

        internal static int ParseSign(string s)
        {
            switch (s)
            {
                case "E":
                    return -1;
                case "I":
                    return 1;
                default:
                    return 0;
            }
        }

        public override string ToString()
        {
            return this.Name;
        }

        /// <summary>
        /// Generated by TaxCategoryCollection.GenerateReport
        /// </summary>
        public IDictionary<string, List<Transaction>> Groups { get; set; }

    }

    public class TaxCategoryCollection : List<TaxCategory>
    {
        private const string TableStartMarker = "^ START OF TABLE";
        private const string TableEndMarker = "^ END OF TABLE";

        public TaxCategoryCollection()
        {
            this.Load();
        }

        private static Dictionary<string, TaxForm> forms = null;
        private static Dictionary<int, TaxCategory> byRef = null;

        public TaxCategory Find(int refNum)
        {
            TaxCategory c = null;
            byRef.TryGetValue(refNum, out c);
            return c;
        }

        public TaxForm FindForm(string name)
        {
            TaxForm f = null;
            forms.TryGetValue(name, out f);
            return f;
        }

        public TaxForm[] GetForms()
        {
            return forms.Values.ToArray();
        }

        private void Load()
        {
            if (forms == null)
            {
                forms = new Dictionary<string, TaxForm>();
                byRef = new Dictionary<int, TaxCategory>();

                TaxForm form = null;
                bool started = false;
                string spec = ProcessHelper.GetEmbeddedResource("Walkabout.Taxes.TxfSpec.txt");
                using (StringReader reader = new StringReader(spec))
                {
                    int linenumber = 0;
                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        linenumber++;
                        if (!started && line.StartsWith(TableStartMarker))
                        {
                            started = true;
                        }
                        else if (started && line.StartsWith(TableEndMarker))
                        {
                            break;
                        }
                        else if (started)
                        {
                            // ok, we have a line to parse.
                            TaxCategory c = TaxCategory.Parse(line);
                            if (c != null)
                            {
                                if (c.Level == 0)
                                {
                                    form = new TaxForm()
                                    {
                                        Name = c.Name,
                                        Categories = new List<TaxCategory>()
                                    };
                                    forms[c.Name] = form;
                                }
                                else
                                {
                                    if (form != null)
                                    {
                                        c.Form = form;
                                        form.Categories.Add(c);
                                    }
                                    if (c.RefNum != 0)
                                    {
                                        byRef[c.RefNum] = c;
                                    }
                                }
                            }
                        }
                        line = reader.ReadLine();
                    }
                }
            }

            // now outside of this cache, we need to load the cached values into our collection.
            foreach (var tc in byRef.Values)
            {
                this.Add(tc);
            }
        }

        /// <summary>
        /// Populate the "Groups" property on the tax categories that are in use in the given date range.
        /// </summary>
        public List<TaxCategory> GenerateGroups(MyMoney money, bool investmentsOnly, DateTime startDate, DateTime endDate)
        {
            List<TaxCategory> result = new List<TaxCategory>();
            TaxCategoryCollection taxCategories = new TaxCategoryCollection();
            Dictionary<TaxCategory, List<Category>> map = new Dictionary<TaxCategory, List<Category>>();

            // find out if we have any tax related categories...
            foreach (Category c in money.Categories.GetCategories())
            {
                if (c.TaxRefNum != 0)
                {
                    TaxCategory tc = taxCategories.Find(c.TaxRefNum);
                    if (tc != null)
                    {
                        List<Category> list = null;
                        if (!map.TryGetValue(tc, out list))
                        {
                            map[tc] = list = new List<Category>();
                        }
                        list.Add(c);
                    }
                }
            }

            if (map.Count == 0)
            {
                return null;
            }

            foreach (KeyValuePair<TaxCategory, List<Category>> pair in map)
            {
                TaxCategory tc = pair.Key;
                List<Category> list = pair.Value;

                SortedDictionary<string, List<Transaction>> groups = new SortedDictionary<string, List<Transaction>>();

                foreach (Category c in list)
                {
                    // don't allow child categories, require exact category match.
                    var rows = money.Transactions.GetTransactionsByCategory(c, null, (cat) => { return c == cat; });
                    foreach (Transaction t in rows)
                    {
                        if (investmentsOnly && t.Investment == null)
                        {
                            continue;
                        }
                        if (t.TaxDate < startDate || t.TaxDate >= endDate)
                        {
                            continue;
                        }
                        if (t.Account.IsTaxDeferred || t.Account.IsTaxFree)
                        {
                            continue;
                        }

                        string group = null;

                        switch (tc.SortOrder)
                        {
                            case SortOrder.SortByAsset:
                                group = t.Account.Name;
                                break;
                            case SortOrder.SortByCategory:
                                group = c.Name;
                                break;
                            case SortOrder.None:
                            case SortOrder.SortByPayee:
                                group = t.PayeeName;
                                break;
                        }

                        if (!string.IsNullOrEmpty(group))
                        {
                            List<Transaction> tlist = null;
                            if (!groups.TryGetValue(group, out tlist))
                            {
                                groups[group] = tlist = new List<Transaction>();
                            }
                            tlist.Add(t);
                        }
                    }
                }

                if (groups.Count > 0)
                {
                    tc.Groups = groups;
                    result.Add(tc);
                }
            }

            return result;
        }

    }
}
