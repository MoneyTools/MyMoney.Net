using System;

namespace Walkabout.Ofx
{
    class HtmlResponseException : Exception
    {
        string html;

        public string Html
        {
            get { return this.html; }
            set { this.html = value; }
        }

        public HtmlResponseException(string msg, string html) : base(msg)
        {
            this.html = html;
        }
    }
}
