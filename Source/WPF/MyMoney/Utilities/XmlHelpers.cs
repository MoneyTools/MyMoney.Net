using System;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Walkabout.Utilities
{
    /// <summary>
    /// XLinq extensions
    /// </summary>
    public static class XmlExtensions
    {
        public static XElement SelectExpectedElement(this XContainer n, string xpath)
        {
            XElement result = SelectElement(n, xpath);
            if (result == null)
            {
                throw new Exception(string.Format("Expected element not found: '{0}'", xpath));
            }
            return result;
        }

        /// <summary>
        /// The nice thing about XPath is you can do a deep query just to see if the element exists,
        /// that is a but cumbersome in XLinq because you have to check for null at each stage along the way.
        /// </summary>
        public static XElement SelectElement(this XContainer n, string xpath)
        {
            return n.XPathSelectElement(xpath);
        }

        public static string SelectElementValue(this XElement element, string xpath)
        {
            XElement node = element.SelectElement(xpath);
            if (node != null)
            {
                return node.Value.Trim();
            }
            return null;
        }

        public static decimal SelectElementValueAsDecimal(this XElement element, string xpath)
        {
            XElement node = element.SelectElement(xpath);
            decimal result = 0;
            if (node != null)
            {
                decimal.TryParse(node.Value.Trim(), out result);
            }
            return result;
        }

        public static bool SelectElementValueAsYesNo(this XElement element, string xpath)
        {
            XElement node = element.SelectElement(xpath);
            if (node != null)
            {
                node.Value.ConvertYesNoToBoolean();
            }
            return false;
        }

        public static bool ConvertYesNoToBoolean(this string s)
        {
            if (s == null)
            {
                return false;
            }
            switch (s.Trim().ToLowerInvariant())
            {
                case "yes":
                case "y":
                    return true;
            }
            return false;
        }

        // ====================================== XmlElement extension ========================================

        public static string GetRequiredAttribute(this XmlElement cdef, string name, string view)
        {
            string s = cdef.GetAttribute(name);
            if (s == null)
            {
                throw new ApplicationException(string.Format("Column missing required {0} attribute in view {0}", name, view));
            }
            return s;
        }

        public static bool? GetBooleanAttribute(this XmlElement cdef, string name)
        {
            string s = cdef.GetAttribute(name);
            if (!string.IsNullOrEmpty(s))
            {
                return Boolean.Parse(s);
            }
            return null;
        }

        public static int? GetIntegerAttribute(this XmlElement cdef, string name)
        {
            string s = cdef.GetAttribute(name);
            if (!string.IsNullOrEmpty(s))
            {
                return Int32.Parse(s);
            }
            return null;
        }

        public static T? GetEnumAttribute<T>(this XmlElement cdef, string name) where T : struct
        {
            string s = cdef.GetAttribute(name);
            if (!string.IsNullOrEmpty(s))
            {
                return (T)Enum.Parse(typeof(T), s);
            }
            return null;
        }
    }
}
