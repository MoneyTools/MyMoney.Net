using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Walkabout.Ofx
{
    class HtmlResponseException : Exception
    {
        string html;

        public string Html
        {
            get { return html; }
            set { html = value; }
        }

        public HtmlResponseException(string msg, string html) : base(msg)
        {
            this.html = html;
        }
    }
}
