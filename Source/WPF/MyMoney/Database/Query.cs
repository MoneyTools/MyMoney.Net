using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Walkabout.Data
{

    public enum Field
    {
        None,
        Accepted,
        Account,
        Budgeted,
        Category,
        Date,
        Deposit,
        Memo,
        Number,
        Payee,
        Payment,
        SalesTax,
        Status,
    }


    public enum Operation
    {
        None,
        Contains,
        Equals,
        GreaterThan,
        GreaterThanEquals,
        LessThan,
        LessThanEquals,
        NotContains,
        NotEquals,
        Regex,
    }

    public enum Conjunction
    {
        None,
        And,
        Or
    }

    public interface IQueryable
    {
        bool Matches(object o, Operation op);
    }

    public class QueryRow : IXmlSerializable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        Field field;
        string value;
        Operation op;
        Conjunction con;
        Regex regex;

        public QueryRow()
        {
        }

        public QueryRow(Conjunction con, Field field, Operation op, string value)
        {
            this.con = con;
            this.field = field;
            this.op = op;
            this.value = value;
            Init();
        }

        private void Init()
        {
            if (op == Data.Operation.Regex)
            {
                regex = new Regex(this.value);
            }
        }

        public Field Field
        {
            get { return this.field; }
            set 
            {   this.field = value;
                RaisePropertyChanged("Field");
            }
        }

        public Operation Operation
        {
            get { return this.op; }
            set 
            {
                this.op = value;
                Init();
                RaisePropertyChanged("Operation");
            }
        }

        public static string GetOperationDisplayString(Operation op)
        {
            switch (op)
            {
                case Operation.Contains:
                    return "contains";
                case Operation.Equals:
                    return "=";
                case Operation.GreaterThan:
                    return ">";
                case Operation.GreaterThanEquals:
                    return ">=";
                case Operation.LessThan:
                    return "<";
                case Operation.LessThanEquals:
                    return "<=";
                case Operation.NotContains:
                    return "not contains";
                case Operation.NotEquals:
                    return "!=";
                case Operation.Regex:
                    return "regex";
                case Operation.None:
                default:
                    break;
            }
            return "";
        }

        public string OperationDisplay
        {
            get
            {
                return GetOperationDisplayString(this.Operation);
            }
            set
            {
                Operation op = Operation.None;
                switch (value)
                {
                    case "contains":
                        op = Operation.Contains;
                        break;
                    case "=":
                        op = Operation.Equals;
                        break;
                    case ">":
                        op = Operation.GreaterThan;
                        break;
                    case ">=":
                        op = Operation.GreaterThanEquals;
                        break;
                    case "<":
                        op = Operation.LessThan;
                        break;
                    case "<=":
                        op = Operation.LessThanEquals;
                        break;
                    case "not contains":
                        op = Operation.NotContains;
                        break;
                    case "!=":
                        op = Operation.NotEquals;
                        break;
                    case "regex":
                        op = Operation.Regex;
                        break;
                    default:
                        break;
                }
                this.Operation = op;
            }
        }

        public string Value
        {
            get { return this.value; }
            set 
            { 
                this.value = value;
                Init();
                RaisePropertyChanged("Value");
            }
        }

        public Conjunction Conjunction
        {
            get { return this.con; }
            set 
            { 
                this.con = value;
                RaisePropertyChanged("Conjunction");
            }
        }

        static bool TryParseBoolean(string value, bool defaultValue)
        {
            bool result;
            if (bool.TryParse(value, out result))
                return result;
            return defaultValue;
        }

        static decimal TryParseDecimal(string value, decimal defaultValue)
        {
            decimal result;
            if (decimal.TryParse(value, out result))
                return result;
            return defaultValue;
        }

        static int TryParseInteger(string value, int defaultValue)
        {
            int result;
            if (Int32.TryParse(value, out result))
                return result;
            return defaultValue;
        }

        static DateTime TryParseDate(string value, DateTime defaultValue)
        {
            DateTime result;
            if (DateTime.TryParse(value, out result))
                return result;
            return defaultValue;
        }

        public bool Matches(IQueryable q)
        {
            if (q != null)
            {
                return q.Matches(this.value, this.op);
            }
            return string.IsNullOrEmpty(value);
        }

        public bool Matches(bool value)
        {
            switch (this.op)
            {
                case Operation.None:
                    return false;
                case Operation.Contains:
                    return this.Matches(value.ToString());
                case Operation.Equals:
                    return value == TryParseBoolean(this.value, !value);
                case Operation.GreaterThan:
                    return this.Matches(value ? 1 : 0);
                case Operation.GreaterThanEquals:
                    return this.Matches(value ? 1 : 0);
                case Operation.LessThan:
                    return this.Matches(value ? 1 : 0);
                case Operation.LessThanEquals:
                    return this.Matches(value ? 1 : 0);
                case Operation.NotContains:
                    return this.Matches(value.ToString());
                case Operation.NotEquals:
                    return value == TryParseBoolean(this.value, value);
                case Operation.Regex:
                    return this.Matches(value.ToString());

            }
            return false;
        }

        public bool Matches(decimal value)
        {
            switch (this.op)
            {
                case Operation.None:
                    return false;
                case Operation.Contains:
                    return this.Matches(value.ToString());
                case Operation.Equals:
                    return value == TryParseDecimal(this.value, Decimal.MaxValue);
                case Operation.GreaterThan:
                    return value >= TryParseDecimal(this.value, Decimal.MaxValue);
                case Operation.GreaterThanEquals:
                    return value >= TryParseDecimal(this.value, Decimal.MaxValue);
                case Operation.LessThan:
                    return value < TryParseDecimal(this.value, Decimal.MinValue);
                case Operation.LessThanEquals:
                    return value <= TryParseDecimal(this.value, Decimal.MinValue);
                case Operation.NotContains:
                    return this.Matches(value.ToString());
                case Operation.NotEquals:
                    return value != TryParseDecimal(this.value, Decimal.MaxValue);
                case Operation.Regex:
                    return this.Matches(value.ToString());
            }
            return false;
        }

        public bool Matches(int value)
        {
            switch (this.op)
            {
                case Operation.None:
                    return false;
                case Operation.Contains:
                    return this.Matches(value.ToString());
                case Operation.Equals:
                    return Math.Abs(value) == Math.Abs(TryParseInteger(this.value, Int32.MaxValue));
                case Operation.GreaterThan:
                    return Math.Abs(value) >= Math.Abs(TryParseInteger(this.value, Int32.MaxValue));
                case Operation.GreaterThanEquals:
                    return Math.Abs(value) >= Math.Abs(TryParseInteger(this.value, Int32.MaxValue));
                case Operation.LessThan:
                    return Math.Abs(value) < Math.Abs(TryParseInteger(this.value, Int32.MaxValue));
                case Operation.LessThanEquals:
                    return Math.Abs(value) <= Math.Abs(TryParseInteger(this.value, Int32.MaxValue));
                case Operation.NotContains:
                    return this.Matches(value.ToString());
                case Operation.NotEquals:
                    return Math.Abs(value) != Math.Abs(TryParseInteger(this.value, Int32.MaxValue));
                case Operation.Regex:
                    return this.Matches(value.ToString());
            }
            return false;
        }

        public bool Matches(string value)
        {
            if (value == null) value = "";
            string s = this.value;
            if (s == null) s = "";

            switch (this.op)
            {
                case Operation.None:
                    return false;
                case Operation.Contains:
                    if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(value);
                    if (string.IsNullOrEmpty(value)) return string.IsNullOrEmpty(s);
                    return value.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
                case Operation.Equals:
                    return string.Compare(s, value, StringComparison.OrdinalIgnoreCase) == 0;
                case Operation.GreaterThan:
                    return string.Compare(s, value, StringComparison.OrdinalIgnoreCase) > 0;
                case Operation.GreaterThanEquals:
                    return string.Compare(s, value, StringComparison.OrdinalIgnoreCase) >= 0;
                case Operation.LessThan:
                    return string.Compare(s, value, StringComparison.OrdinalIgnoreCase) < 0;
                case Operation.LessThanEquals:
                    return string.Compare(s, value, StringComparison.OrdinalIgnoreCase) <= 0;
                case Operation.NotContains:
                    if (string.IsNullOrEmpty(s)) return !string.IsNullOrEmpty(value);
                    if (string.IsNullOrEmpty(value)) return !string.IsNullOrEmpty(s);
                    return value.IndexOf(s, StringComparison.OrdinalIgnoreCase) < 0;
                case Operation.NotEquals:
                    return string.Compare(s, value, StringComparison.OrdinalIgnoreCase) != 0;
                case Operation.Regex:
                    return this.regex.IsMatch(value);
            }
            return false;
        }

        public bool Matches(DateTime value)
        {
            switch (this.op)
            {
                case Operation.None:
                    return false;
                case Operation.Contains:
                    return this.Matches(value.ToString());
                case Operation.Equals:
                    return value == TryParseDate(this.value, DateTime.MaxValue);
                case Operation.GreaterThan:
                    return value >= TryParseDate(this.value, DateTime.MaxValue);
                case Operation.GreaterThanEquals:
                    return value >= TryParseDate(this.value, DateTime.MaxValue);
                case Operation.LessThan:
                    return value < TryParseDate(this.value, DateTime.MinValue);
                case Operation.LessThanEquals:
                    return value <= TryParseDate(this.value, DateTime.MinValue);
                case Operation.NotContains:
                    return this.Matches(value.ToString());
                case Operation.NotEquals:
                    return value != TryParseDate(this.value, DateTime.MaxValue);
                case Operation.Regex:
                    return this.Matches(value.ToString());
            }
            return false;
        }

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void WriteXml(XmlWriter w)
        {
            w.WriteElementString("Field", this.Field.ToString());
            w.WriteElementString("Operation", this.Operation.ToString());
            w.WriteElementString("Value", this.Value);
            w.WriteElementString("Conjunction", this.Conjunction.ToString());
        }

        public static QueryRow ReadQuery(XmlReader r)
        {
            QueryRow q = new QueryRow();
            q.ReadXml(r);
            return q;
        }

        public void ReadXml(XmlReader r)
        {
            if (r.IsEmptyElement)
            {
                r.Read();
                return;
            }
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    switch (r.Name)
                    {
                        case "Field":
                            this.Field = (Field)Enum.Parse(typeof(Field), r.ReadString());
                            break;
                        case "Operation":
                            this.Operation = (Operation)Enum.Parse(typeof(Operation), r.ReadString());
                            break;
                        case "Value":
                            this.Value = r.ReadString();
                            break;
                        case "Conjunction":
                            this.Conjunction = (Conjunction)Enum.Parse(typeof(Conjunction), r.ReadString());
                            break;
                    }
                }
            }
        }
    }


}
