using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Xml.Linq;
using System.Globalization;

namespace Walkabout.Utilities
{
    /// <summary>
    /// This observable collection is handy for implementing Quick Search item filtering in 
    /// a consistent way across different types of collections.  For example, it parses the
    /// quick search string into tokens so you and do compound searches like "food & 50".
    /// The implementer subclasses from this collection and implements the IsMatch method 
    /// which will be passed the individual search tokens, like "food" and "50" in the
    /// above example.
    /// </summary>
    /// <typeparam name="T">The items in the collection</typeparam>
    public abstract class FilteredObservableCollection<T> : ObservableCollection<T> 
    {
        string filter;
        IEnumerable<T> original;

        protected FilteredObservableCollection(IEnumerable<T> collection) : base(collection)
        {
            this.original = collection;
        }

        protected FilteredObservableCollection(IEnumerable<T> collection, string filter)
        {
            this.original = collection;
            SetFilter(filter);
        }

        public string Filter
        {
            get { return this.filter; }
            set
            {
                if (this.filter != value)
                {
                    SetFilter(value);
                }
            }
        }


        private void SetFilter(string filter)
        {
            this.filter = filter;
            this.Clear();

            QuickFilterParser<T> parser = new QuickFilterParser<T>();
            Filter<T> expr = parser.Parse(filter);

            if (expr != null)
            {
                string dgml = expr.ToXml();
                Debug.WriteLine(dgml);
            }
            // Only include the items that match the filter expression.
            Stopwatch timer = new Stopwatch();
            timer.Start();
            int count = 0;
            int found = 0;
            foreach (T item in original)
            {
                count++;
                if (expr == null || expr.IsMatch(this, item))
                {
                    Add(item);
                }
            }
            timer.Stop();
            Debug.WriteLine("Filtered " + count + " items, found " + found + " matches in " + timer.ElapsedMilliseconds + " ms");
        }


        public abstract bool IsMatch(T item, FilterLiteral filterToken);

    }

    enum QuickFilterToken // also happen to be in order or precidence.
    {
        None,
        Literal,
        LeftParen,
        Or,
        And,
        RightParen,
    }

    internal class QuickFilterParser<T>
    {
        Stack<Filter<T>> stack = new Stack<Filter<T>>();

        private Filter<T> Combine(Filter<T> right, Filter<T> top)
        {
            if (right == null)
            {
                return top;
            }
            if (top == null)
            {
                return right;
            }

            if (right is FilterKeyword<T>)
            {
                FilterKeyword<T> keyword = (FilterKeyword<T>)right;
                if (top is FilterKeyword<T>)
                {
                    // easy case, we are parsing a string of words, so combine them back into a single literal to match.
                    FilterKeyword<T> keyword2 = (FilterKeyword<T>)top;
                    keyword.Keyword = new FilterLiteral(keyword.Keyword.Literal + " " + keyword2.Keyword.Literal);
                    return right;
                }                
            }

            // everything else is some sort of parse error, so this is error recover code...
            return right;
        }

        private Filter<T> Reduce(QuickFilterToken token)
        {
            while (stack.Count > 0)
            {
                Filter<T> top = stack.Pop();
                
                if (stack.Count == 0)
                {
                    return top;
                }

                Filter<T> op = stack.Peek();
                if (token > op.Precidence)
                {
                    return top;
                }

                op = stack.Pop();

                // now combine "top" and "op"
                if (op is FilterKeyword<T>)
                {
                    FilterKeyword<T> keyword = (FilterKeyword<T>)op;
                    op = Combine(keyword, top);
                }
                else if (op is FilterAnd<T>)
                {
                    FilterAnd<T> and = (FilterAnd<T>)op;
                    and.Right = Combine(and.Right, top);
                }
                else if (op is FilterOr<T>)
                {
                    FilterOr<T> or = (FilterOr<T>)op;
                    or.Right = Combine(or.Right, top);
                }
                else if (op is FilterParens<T>)
                {
                    FilterParens<T> parens = (FilterParens<T>)op;
                    parens.Expression = Combine(parens.Expression, top);
                }
                stack.Push(op);
            }
            return null;
        }

        /// <summary>
        ///  Filter expression can be a literal string, "Hello World"  or  it can contain logical 
        ///  conjunctions "or" and "and", like this "Hello and Hi" or "Hello & Hi" or "Hello or Hi" 
        ///  and it can have parentheses "Hello and (foo or bar)" and so on.
        /// </summary>
        public Filter<T> Parse(string quickFilter)
        {
            if (string.IsNullOrWhiteSpace(quickFilter))
            {
                return null;
            }

            foreach (Tuple<QuickFilterToken, string> token in GetFilterTokens(quickFilter))
            {
                string literal = token.Item2;  

                switch (token.Item1)
                {
                    case QuickFilterToken.Literal: // shift                        
                        stack.Push(new FilterKeyword<T>(new FilterLiteral(literal)));
                        break;
                    case QuickFilterToken.And: // reduce
                        stack.Push(new FilterAnd<T>()
                        {
                            Left = Reduce(QuickFilterToken.And)
                        });
                        break;
                    case QuickFilterToken.Or: // reduce
                        stack.Push(new FilterOr<T>()
                        {
                            Left = Reduce(QuickFilterToken.Or)
                        });
                        break;
                    case QuickFilterToken.LeftParen: // shift
                        stack.Push(new FilterParens<T>());
                        break;
                    case QuickFilterToken.RightParen: // reduce
                        Filter<T> op = Reduce(QuickFilterToken.RightParen);
                        FilterParens<T> parens = op as FilterParens<T> ;
                        if (parens != null)
                        {
                            // eliminate parens
                            stack.Push(parens.Expression);
                        }
                        else
                        {
                            // missing open parens, now what?
                            stack.Push(op);
                        }
                        break;
                }
            }

            return Reduce(QuickFilterToken.None);
        }

        /// <summary>
        /// This is the tokenizer, it returns a stream of tokens found in the given string.
        /// </summary>
        /// <param name="quickFilter">The string to tokenize</param>
        /// <returns></returns>
        private IEnumerable<Tuple<QuickFilterToken,string>> GetFilterTokens(string quickFilter)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0, n = quickFilter.Length; i < n; i++)
            {
                QuickFilterToken token = QuickFilterToken.None;
                string literal = null;
                bool previousCouldBeKeyword = false;
                bool thisCouldBeKeyword = false;

                char ch = quickFilter[i];
                if (ch == '&' || ch == '+')
                {
                    token = QuickFilterToken.And;
                    literal = ch.ToString();
                }
                else if (ch == '"')
                {
                    int j = quickFilter.IndexOf('"', i + 1);
                    if (j > i + 1)
                    {
                        sb.Append(quickFilter.Substring(i + 1, j - i - 1));
                        i = j;
                        token = QuickFilterToken.Literal;
                        literal = sb.ToString();
                        sb.Length = 0;
                        previousCouldBeKeyword = true;
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else if (ch == ',' || ch == '|')
                {
                    token = QuickFilterToken.Or;
                    literal = ch.ToString();
                }
                else if (ch == '(')
                {
                    token = QuickFilterToken.LeftParen;
                    literal = ch.ToString();
                    previousCouldBeKeyword = true;
                }
                else if (ch == ')')
                {
                    token = QuickFilterToken.RightParen;
                    literal = ch.ToString();
                }
                else if (Char.IsWhiteSpace(ch))
                {
                    token = QuickFilterToken.Literal;
                    literal = sb.ToString();
                    sb.Length = 0;
                    thisCouldBeKeyword = true;
                }
                else
                {
                    sb.Append(ch);
                }

                if (token != QuickFilterToken.None)
                {
                    string previous = sb.ToString();
                    if (!string.IsNullOrEmpty(previous)) 
                    {
                        if (previousCouldBeKeyword && string.Compare(previous, "and", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.And, previous);
                        }
                        else if (previousCouldBeKeyword && string.Compare(previous, "or", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Or, previous);
                        }
                        yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Literal, previous);
                    }
                    sb.Length = 0;

                    if (!string.IsNullOrEmpty(literal))
                    {
                        if (thisCouldBeKeyword && string.Compare(literal, "and", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.And, literal);
                        }
                        else if (thisCouldBeKeyword && string.Compare(literal, "or", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Or, literal);
                        }
                        else
                        {
                            yield return new Tuple<QuickFilterToken, string>(token, literal);
                        }
                    }
                }
            }
            
            if (sb.Length > 0) 
            {
                yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Literal, sb.ToString());                
            }
        }

    }


    abstract class Filter<T>
    {
        public abstract QuickFilterToken Precidence { get; }

        public abstract bool IsMatch(FilteredObservableCollection<T> collection, T item);

        public virtual string ToXml()
        {
            SimpleGraph g = new SimpleGraph();
            WriteTo(g);
            return g.ToXml("").OuterXml;
        }

        public abstract SimpleGraphNode WriteTo(SimpleGraph g);
    }

    class FilterKeyword<T> : Filter<T>
    {
        public FilterKeyword(FilterLiteral keyword)
        {
            this.Keyword = keyword;
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.Literal; } }

        public FilterLiteral Keyword { get; set; }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            return collection.IsMatch(item, Keyword);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            if (Keyword != null)
            {
                return g.AddOrGetNode(Keyword.Literal);
            }
            return null;
        }
    }

    class FilterAnd<T> : Filter<T>
    {
        public Filter<T> Left { get; set; }
        public Filter<T> Right { get; set; }

        public FilterAnd()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.And; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (Left == null && Right != null)
            {
                return Right.IsMatch(collection, item);
            }
            if (Right == null && Left != null)
            {
                return Left.IsMatch(collection, item);
            }
            return Left.IsMatch(collection, item) && Right.IsMatch(collection, item);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            var node = g.AddOrGetNode(Guid.NewGuid().ToString());
            node.Label = "And";
            if (Left != null)
            {
                var child = Left.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            if (Right != null)
            {
                var child = Right.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            return node;
        }
    }

    class FilterOr<T> : Filter<T>
    {
        public Filter<T> Left { get; set; }
        public Filter<T> Right { get; set; }

        public FilterOr()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.Or; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (Left == null && Right != null)
            {
                return Right.IsMatch(collection, item);
            }
            if (Right == null && Left != null)
            {
                return Left.IsMatch(collection, item);
            }
            return Left.IsMatch(collection, item) || Right.IsMatch(collection, item);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            var node = g.AddOrGetNode(Guid.NewGuid().ToString());
            node.Label = "Or";
            if (Left != null)
            {
                var child = Left.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            if (Right != null)
            {
                var child = Right.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            return node;
        }
    }

    class FilterParens<T> : Filter<T>
    {
        public Filter<T> Expression { get; set; }

        public FilterParens()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.LeftParen; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (Expression == null)
            {
                return false;
            }
            return Expression.IsMatch(collection, item);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            if (Expression != null)
            {
                return Expression.WriteTo(g);
            }
            return null;
        }
    }

    /// <summary>
    /// This class is used when filtering transactions.  It optimizes the parsing of the filter
    /// so it only happens once for all transactions or splits or investments being filtered.
    /// </summary>
    public class FilterLiteral
    {
        string _keyword;
        bool notDecimal;
        decimal? _decimal;
        bool notDate;
        DateTime? _date;

        public FilterLiteral(string keyword)
        {
            _keyword = keyword;
        }

        public string Literal { get { return _keyword; } }

        public bool MatchDecimal(decimal other)
        {
            return InternalMatchDecimal(other) || InternalMatchDecimal(-other);
        }
            
        private bool InternalMatchDecimal(decimal other)
        {
            if (notDecimal)
            {
                return false;
            }
            if (_decimal == null)
            {
                decimal d = 0;
                if (!decimal.TryParse(_keyword, out d))
                {
                    notDecimal = true;
                    return false;
                }
                _decimal = d;
            }
            return other == _decimal.Value;
        }

        internal bool MatchSubstring(string s)
        {
            bool rc = s != null && s.IndexOf(_keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            return rc;
        }

        internal bool MatchDate(DateTime dateTime)
        {
            if (notDate)
            {
                return MatchSubstring(dateTime.ToShortDateString());
            }
            if (_date == null)
            {
                DateTime d = DateTime.MinValue;
                if (!DateTime.TryParse(_keyword, CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.NoCurrentDateDefault, out d))
                {
                    notDate = true;
                    return false;
                }
                _date = d;
            }
            return _date.Value == dateTime;
       
        }
    }
}
