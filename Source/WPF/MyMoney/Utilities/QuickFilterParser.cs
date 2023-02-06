using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Walkabout.Utilities
{
    internal enum QuickFilterToken // also happen to be in order or precidence.
    {
        None,
        Literal,
        LeftParen,
        RightParen,
        Not,
        Or,
        And,
    }

    internal class QuickFilterParser<T>
    {
        private readonly Stack<Filter<T>> stack = new Stack<Filter<T>>();

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
            while (this.stack.Count > 0)
            {
                Filter<T> top = this.stack.Pop();

                if (this.stack.Count == 0)
                {
                    return top;
                }

                Filter<T> op = this.stack.Peek();
                if (token > op.Precidence)
                {
                    return top;
                }

                op = this.stack.Pop();

                // now combine "top" and "op"
                if (op is FilterKeyword<T>)
                {
                    FilterKeyword<T> keyword = (FilterKeyword<T>)op;
                    op = this.Combine(keyword, top);
                }
                else if (op is FilterAnd<T>)
                {
                    FilterAnd<T> and = (FilterAnd<T>)op;
                    and.Right = this.Combine(and.Right, top);
                }
                else if (op is FilterOr<T>)
                {
                    FilterOr<T> or = (FilterOr<T>)op;
                    or.Right = this.Combine(or.Right, top);
                }
                else if (op is FilterNot<T>)
                {
                    FilterNot<T> not = (FilterNot<T>)op;
                    not.Right = this.Combine(not.Right, top);
                }
                else if (op is FilterParens<T>)
                {
                    FilterParens<T> parens = (FilterParens<T>)op;
                    parens.Expression = this.Combine(parens.Expression, top);
                }
                this.stack.Push(op);
            }
            return null;
        }

        /// <summary>
        ///  Filter expression can be a literal string, "Hello World"  or  it can contain logical 
        ///  conjunctions "or" and "and", like this "Hello and Hi" or "Hello & Hi" or "Hello or Hi" 
        ///  and it can have parentheses "Hello and (foo or bar)" and so on.  It can also have 
        ///  a "not" or "!" for negation.
        /// </summary>
        public Filter<T> Parse(string quickFilter)
        {
            if (string.IsNullOrWhiteSpace(quickFilter))
            {
                return null;
            }

            foreach (Tuple<QuickFilterToken, string> token in this.GetFilterTokens(quickFilter))
            {
                string literal = token.Item2;

                switch (token.Item1)
                {
                    case QuickFilterToken.Literal: // shift                        
                        this.stack.Push(new FilterKeyword<T>(new FilterLiteral(literal)));
                        break;
                    case QuickFilterToken.And: // reduce
                        this.stack.Push(new FilterAnd<T>()
                        {
                            Left = this.Reduce(QuickFilterToken.And)
                        });
                        break;
                    case QuickFilterToken.Or: // reduce
                        this.stack.Push(new FilterOr<T>()
                        {
                            Left = this.Reduce(QuickFilterToken.Or)
                        });
                        break;
                    case QuickFilterToken.Not: // reduce
                        this.stack.Push(new FilterNot<T>() { });
                        break;
                    case QuickFilterToken.LeftParen: // shift
                        this.stack.Push(new FilterParens<T>());
                        break;
                    case QuickFilterToken.RightParen: // reduce
                        Filter<T> op = this.Reduce(QuickFilterToken.RightParen);
                        FilterParens<T> parens = op as FilterParens<T>;
                        if (parens != null)
                        {
                            // eliminate parens
                            this.stack.Push(parens.Expression);
                        }
                        else
                        {
                            // missing open parens, now what?
                            this.stack.Push(op);
                        }
                        break;
                }
            }

            return this.Reduce(QuickFilterToken.None);
        }

        /// <summary>
        /// This is the tokenizer, it returns a stream of tokens found in the given string.
        /// </summary>
        /// <param name="quickFilter">The string to tokenize</param>
        /// <returns></returns>
        private IEnumerable<Tuple<QuickFilterToken, string>> GetFilterTokens(string quickFilter)
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
                    int j = i + 1;
                    for (; j < n; j++)
                    {
                        char sh = quickFilter[j];
                        if (sh == '\\' && j + 1 < n && quickFilter[j + 1] == '"')
                        {
                            // escaped double quote!
                            j++;
                            sb.Append('"');
                        }
                        else if (sh == '"')
                        {
                            j++;
                            break;
                        }
                        else
                        {
                            sb.Append(sh);
                        }
                    }
                    token = QuickFilterToken.Literal;
                    literal = sb.ToString();
                    sb.Length = 0;
                    previousCouldBeKeyword = true;
                    i = j;
                }
                else if (ch == '|') // we do not recognize "," as "or" because "," exists as a thousands separator in $amounts, so that would be ambiguous.
                {
                    token = QuickFilterToken.Or;
                    literal = ch.ToString();
                }
                else if (ch == '!')
                {
                    token = QuickFilterToken.Not;
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
                else if (char.IsWhiteSpace(ch))
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
                    sb.Length = 0;

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
                        else if (previousCouldBeKeyword && string.Compare(previous, "not", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Not, previous);
                        }
                        yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Literal, previous);
                    }

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
                        else if (thisCouldBeKeyword && string.Compare(literal, "not", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            yield return new Tuple<QuickFilterToken, string>(QuickFilterToken.Not, literal);
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

    internal abstract class Filter<T>
    {
        public abstract QuickFilterToken Precidence { get; }

        public abstract bool IsMatch(FilteredObservableCollection<T> collection, T item);

        public virtual string ToXml()
        {
            SimpleGraph g = new SimpleGraph();
            this.WriteTo(g);
            return g.ToXml("").OuterXml;
        }

        public abstract SimpleGraphNode WriteTo(SimpleGraph g);
    }

    internal class FilterKeyword<T> : Filter<T>
    {
        public FilterKeyword(FilterLiteral keyword)
        {
            this.Keyword = keyword;
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.Literal; } }

        public FilterLiteral Keyword { get; set; }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            return collection.IsMatch(item, this.Keyword);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            if (this.Keyword != null)
            {
                return g.AddOrGetNode(this.Keyword.Literal);
            }
            return null;
        }
    }

    internal class FilterAnd<T> : Filter<T>
    {
        public Filter<T> Left { get; set; }
        public Filter<T> Right { get; set; }

        public FilterAnd()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.And; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (this.Left == null && this.Right != null)
            {
                return this.Right.IsMatch(collection, item);
            }
            if (this.Right == null && this.Left != null)
            {
                return this.Left.IsMatch(collection, item);
            }
            return this.Left.IsMatch(collection, item) && this.Right.IsMatch(collection, item);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            var node = g.AddOrGetNode(Guid.NewGuid().ToString());
            node.Label = "And";
            if (this.Left != null)
            {
                var child = this.Left.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            if (this.Right != null)
            {
                var child = this.Right.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            return node;
        }
    }

    internal class FilterOr<T> : Filter<T>
    {
        public Filter<T> Left { get; set; }
        public Filter<T> Right { get; set; }

        public FilterOr()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.Or; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (this.Left == null && this.Right != null)
            {
                return this.Right.IsMatch(collection, item);
            }
            if (this.Right == null && this.Left != null)
            {
                return this.Left.IsMatch(collection, item);
            }
            return this.Left.IsMatch(collection, item) || this.Right.IsMatch(collection, item);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            var node = g.AddOrGetNode(Guid.NewGuid().ToString());
            node.Label = "Or";
            if (this.Left != null)
            {
                var child = this.Left.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            if (this.Right != null)
            {
                var child = this.Right.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            return node;
        }
    }

    internal class FilterNot<T> : Filter<T>
    {
        public Filter<T> Right { get; set; }

        public FilterNot()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.Not; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (this.Right != null)
            {
                return !this.Right.IsMatch(collection, item);
            }
            return false;
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            var node = g.AddOrGetNode(Guid.NewGuid().ToString());
            node.Label = "Not";
            if (this.Right != null)
            {
                var child = this.Right.WriteTo(g);
                g.GetOrAddLink(node.Id, child.Id);
            }
            return node;
        }
    }

    internal class FilterParens<T> : Filter<T>
    {
        public Filter<T> Expression { get; set; }

        public FilterParens()
        {
        }

        public override QuickFilterToken Precidence { get { return QuickFilterToken.LeftParen; } }

        public override bool IsMatch(FilteredObservableCollection<T> collection, T item)
        {
            if (this.Expression == null)
            {
                return false;
            }
            return this.Expression.IsMatch(collection, item);
        }

        public override SimpleGraphNode WriteTo(SimpleGraph g)
        {
            if (this.Expression != null)
            {
                return this.Expression.WriteTo(g);
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
        private readonly string _keyword;
        private bool notDecimal;
        private decimal? _decimal;
        private bool notDate;
        private DateTime? _date;

        public FilterLiteral(string keyword)
        {
            this._keyword = keyword;
        }

        public string Literal { get { return this._keyword; } }

        public bool MatchDecimal(decimal other)
        {
            return this.InternalMatchDecimal(other) || this.InternalMatchDecimal(-other);
        }

        private bool InternalMatchDecimal(decimal other)
        {
            if (this.notDecimal)
            {
                return false;
            }
            if (this._decimal == null)
            {
                decimal d = 0;
                if (!decimal.TryParse(this._keyword, out d))
                {
                    this.notDecimal = true;
                    return false;
                }
                this._decimal = d;
            }
            return other == this._decimal.Value;
        }

        internal bool MatchSubstring(string s)
        {
            bool rc = s != null && s.IndexOf(this._keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            return rc;
        }

        internal bool MatchDate(DateTime dateTime)
        {
            if (this.notDate)
            {
                return this.MatchSubstring(dateTime.ToShortDateString());
            }
            if (this._date == null)
            {
                DateTime d = DateTime.MinValue;
                if (!DateTime.TryParse(this._keyword, CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.NoCurrentDateDefault, out d))
                {
                    this.notDate = true;
                    return false;
                }
                this._date = d;
            }
            return this._date.Value == dateTime;

        }
    }
}
