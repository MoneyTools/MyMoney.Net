using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Walkabout.Utilities
{
    public class CsvDocument
    {
        private int _quoteChar;
        private readonly int _fieldDelimiter = ',';
        private List<string> _headers = new List<string>();
        private List<List<string>> _rows = new List<List<string>>();
        private readonly List<StringBuilder> _fields = new List<StringBuilder>();
        private int _fieldCount;

        public CsvDocument()
        {
        }

        public List<string> Headers => this._headers;

        public List<List<string>> Rows => this._rows;

        public static CsvDocument Load(string fileName)
        {
            using (var reader = new StreamReader(fileName))
            {
                return Read(reader);
            }
        }


        public static CsvDocument Read(TextReader reader)
        {
            var doc = new CsvDocument();
            doc.ImportStream(reader);
            if (doc.Headers.Count == 0 || doc.Rows.Count == 0)
            {
                throw new Exception(".csv file is empty");
            }
            return doc;
        }

        private int ImportStream(TextReader reader)
        {
            int rows = 0;
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                if (this.ReadRecord(line))
                {
                    if (this._fieldCount > 0)
                    {
                        var values = from f in this._fields select f.ToString();
                        if (rows == 0)
                        {
                            // first row should be row headers e.g. "Trans. Date,Post Date,Description,Amount,Category"
                            this._headers = this.GetFields();
                        }
                        else
                        {
                            var row = this.GetFields();
                            this._rows.Add(row);
                        }
                    }
                    rows++;
                }
            }
            return rows;
        }

        private List<string> GetFields()
        {
            var result = new List<string>(); 
            for (int i = 0; i < this._fieldCount; i++)
            {
                result.Add(this._fields[i].ToString());
            }
            return result;
        }

        private StringBuilder AddField()
        {
            if (this._fieldCount == this._fields.Count)
            {
                var builder = new StringBuilder();
                this._fields.Add(builder);
                this._fieldCount++;
                return builder;
            }
            var sb = this._fields[this._fieldCount++];
            sb.Length = 0;
            return sb;
        }

        private bool ReadRecord(string line)
        {
            this._fieldCount = 0;
            // read a record.
            int pos = 0;
            int len = line.Length;
            if (pos >= len)
            {
                return false;
            }

            char ch = line[pos++];
            while (pos < len && (ch == ' ' || ch == '\t'))
            {
                ch = line[pos++];
            }

            if (pos >= len)
            {
                return false;
            }

            while (pos < len)
            {
                StringBuilder sb = this.AddField();
                if (ch == '\'' || ch == '"')
                {
                    this._quoteChar = ch;
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
                    while (pos < len && ch != this._fieldDelimiter)
                    {
                        sb.Append(ch);
                        ch = line[pos++]; // peek next char
                    }
                    if (ch != this._fieldDelimiter)
                    {
                        sb.Append(ch);
                        ch = '\0';
                    }
                }
                if (ch == this._fieldDelimiter && pos < len)
                {
                    ch = line[pos++];
                }
            }
            return true;
        }
    }
}
