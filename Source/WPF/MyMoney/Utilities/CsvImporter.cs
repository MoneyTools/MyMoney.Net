using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using Walkabout.Data;
using Walkabout.Migrate;
using Walkabout.Dialogs;
using Walkabout.Views.Controls;

namespace Walkabout.Migrate
{
    public class CsvFieldMap
    {
        public CsvFieldMap() { }
        public string Header { get; set; }
        public string Field { get; set; }
    }

    public class CsvMap
    {
        public CsvMap() { }

        [XmlIgnore]
        public string FileName { get; set; }

        public List<CsvFieldMap> Fields { get; set; }

        public static CsvMap Load(string fileName)
        {
            if (File.Exists(fileName))
            {
                using (var reader = XmlReader.Create(fileName))
                {
                    var s = new XmlSerializer(typeof(CsvMap));
                    var map = (CsvMap)(s.Deserialize(reader));
                    map.FileName = fileName;
                    return map;
                }
            }
            return new CsvMap() { FileName = fileName };
        }

        public void Save()
        {
            var settings = new XmlWriterSettings() { Indent = true };
            using (var writer = XmlWriter.Create(this.FileName, settings))
            {
                var s = new XmlSerializer(typeof(CsvMap));
                s.Serialize(writer, this);
            }
        }
    }

    internal class UserCanceledException : Exception
    {
        public UserCanceledException(string msg) : base(msg) { }
    }

    internal class CsvTransactionImporter : CsvFieldWriter
    {
        MyMoney money;
        Account account;
        CsvMap map;
        List<TBag> typedData = new List<TBag>();
        // this is what we "can" import...
        string[] fields = new string[] { "Date", "Payee", "Memo", "Amount" };

        public CsvTransactionImporter(MyMoney money, Account account, CsvMap map)
        {
            this.money = money;
            this.account = account;
            this.map = map;
        }

        public void Commit()
        {
            // ok, we haved typed data, no exceptions, so merge with the account.
            if (typedData.Count == 0)
            {
                return;
            }
            try
            {
                this.money.BeginUpdate(this);
                var existing = this.money.Transactions.GetTransactionsFrom(this.account);
                // CSV doesn't provide an FITID so each import can create tons of duplicates.
                // So we need constant time lookup by date so that import can quickly match existing matching transactions.
                Dictionary<DateTime, object> indexed = new Dictionary<DateTime, object>();
                foreach (var t in existing)
                {
                    if (indexed.TryGetValue(t.Date, out object o))
                    {
                        if (o is List<Transaction> list)
                        {
                            list.Add(t);
                        }
                        else if (o is Transaction u)
                        {
                            var newList = new List<Transaction>();
                            newList.Add(u);
                            newList.Add(t);
                            indexed[t.Date] = newList;
                        }
                    }
                    else
                    {
                        indexed[t.Date] = t;
                    }
                }

                foreach (var bag in typedData)
                {
                    Transaction found = null;
                    if (indexed.TryGetValue(bag.Date, out object o))
                    {
                        if (o is List<Transaction> list)
                        {
                            foreach (var u in list)
                            {
                                if (u.Payee == bag.Payee && u.Amount == bag.Amount && u.Date == bag.Date)
                                {
                                    // found a perfect match so skip it!
                                    found = u;
                                    break;
                                }
                            }
                        }
                        else if (o is Transaction u)
                        {
                            if (u.Payee == bag.Payee && u.Amount == bag.Amount && u.Date == bag.Date)
                            {
                                // found a perfect match so skip it!
                                found = u;
                            }
                        }
                    }
                    if (found == null)
                    {
                        Transaction t = this.money.Transactions.NewTransaction(this.account);
                        t.Flags = TransactionFlags.Unaccepted;
                        t.Status = TransactionStatus.Electronic;
                        t.Payee = bag.Payee;
                        t.Amount = bag.Amount;
                        t.Memo = bag.Memo;
                        t.Date = bag.Date;
                        this.money.Transactions.Add(t);
                    }
                    else if (found.Status == TransactionStatus.None)
                    {
                        found.Status = TransactionStatus.Electronic;
                    }
                }
            }
            finally
            {
                this.money.EndUpdate();
            }
        }

        public override void WriteHeaders(IEnumerable<string> headers)
        {
            if (HeadersMatch(headers))
            {
                // we're good!
                return;
            }
            CsvImportDialog cd = new CsvImportDialog(fields);
            cd.SetHeaders(headers);
            if (cd.ShowDialog() == true)
            {
                this.map.Fields = cd.Mapping;
            }
            else
            {
                throw new UserCanceledException("User cancelled");
            }
        }

        bool HeadersMatch(IEnumerable<string> headers)
        {
            if (this.map.Fields != null)
            {
                int c = 0;
                foreach (var h in headers)
                {
                    if (c >= this.map.Fields.Count)
                    {
                        return false;
                    }
                    else if (this.map.Fields[c].Header != h)
                    {
                        return false;
                    }
                    c++;
                }
                return true;
            }
            return false;
        }

        public override void WriteRow(IEnumerable<string> values)
        {
            if (map == null) return;
            int col = 0;
            TBag t = new TBag();
            foreach (var s in values)
            {
                if (col < map.Fields.Count)
                {
                    var fm = map.Fields[col];
                    if (!string.IsNullOrEmpty(fm.Field))
                    {
                        MapField(t, fm.Field, s);
                    }
                }
                col++;
            }
            typedData.Add(t);
        }

        private void MapField(TBag t, string fieldName, string value)
        {
            // matches this.fields.
            switch (fieldName)
            {
                case "Date":
                    if (DateTime.TryParse(value, out DateTime d))
                    {
                        t.Date = d;
                    }
                    break;
                case "Payee":
                    Alias alias = this.money.Aliases.FindMatchingAlias(value);
                    if (alias != null)
                    {
                        if (alias.Payee.Name != value)
                        {
                            t.Memo = value;
                        }
                        t.Payee = alias.Payee;
                    }
                    else
                    {
                        t.Payee = this.money.Payees.FindPayee(value, true);
                    }
                    break;
                case "Memo":
                    t.Memo = value;
                    break;
                case "Amount":
                    if (decimal.TryParse(value, out decimal amount))
                    {
                        if (account.Type == AccountType.Credit)
                        {
                            // credit cards show payments as positive numbers (yeah positive for them!)
                            amount = -amount;
                        }
                        t.Amount = amount;
                    }
                    break;
            }
        }

        class TBag
        {
            public DateTime Date;
            public string Memo;
            public decimal Amount;
            public Payee Payee;
        }
    }

    public abstract class CsvFieldWriter
    {
        /// <summary>
        /// Try and map the given headers to target Transaction fields.
        /// </summary>
        /// <param name="headers">The headers found in the CSV file</param>
        public abstract void WriteHeaders(IEnumerable<string> headers);

        /// <summary>
        /// Now that headers are estabilished and we know which column
        /// maps to which field, we can import rows of data.
        /// </summary>
        /// <param name="values"></param>
        public abstract void WriteRow(IEnumerable<string> values);
    }

    public class CsvImporter : Importer
    {
        int _quoteChar;
        int _fieldDelimiter = ',';
        List<StringBuilder> _fields = new List<StringBuilder>();
        int _fieldCount;
        CsvFieldWriter _writer;

        public CsvImporter(MyMoney money, CsvFieldWriter writer) : base(money)
        {
            _writer = writer;
        }

        public override int Import(string file)
        {
            using (var reader = new StreamReader(file))
            {
                return ImportStream(reader);
            }
        }

        public int ImportStream(TextReader reader)
        {
            int rows = 0;
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                if (ReadRecord(line))
                {
                    if (_fieldCount > 0)
                    {
                        var values = (from f in _fields select f.ToString());
                        if (rows == 0)
                        {
                            // first row might be row headers e.g. "Trans. Date,Post Date,Description,Amount,Category"
                            _writer.WriteHeaders(values);
                        }
                        else
                        {
                            _writer.WriteRow(values);
                        }
                    }
                    rows++;
                }
            }
            return rows;
        }

        StringBuilder AddField()
        {
            if (_fieldCount == _fields.Count)
            {
                var builder = new StringBuilder();
                _fields.Add(builder);
                _fieldCount++;
                return builder;
            }
            var sb = _fields[_fieldCount++];
            sb.Length = 0;
            return sb;
        }

        public List<string> GetFields()
        {
            var result = new List<String>();
            for(int i = 0; i < _fieldCount; i++)
            {
                result.Add(_fields[i].ToString());
            }
            return result;
        }

        public bool ReadRecord(string line)
        {
            _fieldCount = 0;
            // read a record.
            int pos = 0;
            int len = line.Length;
            if (pos >= len) return false;
            char ch = line[pos++];
            while (pos < len && (ch == ' ' || ch == '\t'))
                ch = line[pos++];
            if (pos >= len) return false;

            while (pos < len)
            {
                StringBuilder sb = AddField();
                if (ch == '\'' || ch == '"')
                {
                    _quoteChar = ch;
                    var c = line[pos++];
                    bool done = false;
                    while (!done && pos < len)
                    {
                        while (pos < len && c != ch)
                        { 
                            // scan literal.
                            sb.Append(c);
                            c = line[pos++];
                        }
                        if (pos == len)
                        {
                            // error: hit end of line before matching quote!
                            done = true;
                        }
                        else // (c == ch)
                        {
                            done = true;
                            var next = line[pos++]; // peek next char
                            if (next == ch)
                            {
                                // it was an escaped quote sequence "" inside the literal
                                // so append a single " and consume the second end quote.
                                done = false;
                                sb.Append(next);
                                c = (pos < len) ? line[pos++] : '\0';
                            }
                            else
                            {
                                c = next;
                            }
                        }
                    }
                    // skip whitespace after closing quote up to next field delimiter.
                    while (pos < len && c == ' ')
                    {
                        c = line[pos++];
                    }
                    ch = c;
                }
                else
                {
                    // scan number, date, time, float, etc.
                    while (pos < len && ch != _fieldDelimiter)
                    {
                        sb.Append(ch);
                        ch = line[pos++]; // peek next char
                    }
                    if (ch != _fieldDelimiter)
                    {
                        sb.Append(ch);
                        ch = '\0';
                    }
                }
                if (ch == _fieldDelimiter)
                {                    
                    ch = line[pos++];
                }
            }
            return true;
        }
    }
}
