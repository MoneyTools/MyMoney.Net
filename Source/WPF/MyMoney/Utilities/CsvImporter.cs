using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Walkabout.Data;
using Walkabout.Migrate;

namespace Walkabout.Migrate
{
    public class CsvImporter : Importer
    {
        int _quoteChar;
        int _fieldDelimiter = ',';
        Account _account;
        List<StringBuilder> _fields = new List<StringBuilder>();
        int _fieldCount;

        public CsvImporter(MyMoney money, Account account) : base(money)
        {
            this._account = account;
        }

        public override int Import(string file)
        {
            int rows = 0;
            using (var reader = new StreamReader(file))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line != null)
                    {
                        while (ReadRecord(line))
                        {
                            if (_fieldCount > 0)
                            {
                                // first row might be row headers e.g. "Trans. Date,Post Date,Description,Amount,Category"

                            }
                            rows++;
                        }
                    }
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
