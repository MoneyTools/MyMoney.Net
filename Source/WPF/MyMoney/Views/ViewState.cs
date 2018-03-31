using System;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Configuration;

namespace Walkabout.Interfaces.Views
{

    public class ViewState : IXmlSerializable
    {

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public virtual void ReadXml(XmlReader reader)
        {
        }

        public virtual void WriteXml(XmlWriter writer)
        {
        }

        protected static T ReadEnum<T>(XmlReader r)
        {
            string s = r.ReadString();
            try
            {
                return (T)Enum.Parse(typeof(T), s);
            }
            catch (Exception)
            {
            }
            return default(T);
        }

        protected static bool ReadBoolean(XmlReader r)
        {
            string s = r.ReadString();
            bool value = false;
            try
            {
                value = bool.Parse(s);
            }
            catch (Exception)
            {
            }
            return value;
        }

        protected static int ReadInt(XmlReader r, int defaultValue)
        {
            string s = r.ReadString();
            try
            {
                return Int32.Parse(s);
            }
            catch (Exception)
            {
            }
            return defaultValue;
        }


        protected static Size DeserializeSize(XmlReader r)
        {
            Size s = new Size();
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "Width")
                    {
                        s.Width = Int32.Parse(r.ReadString());
                    }
                    else if (r.Name == "Height")
                    {
                        s.Height = Int32.Parse(r.ReadString());
                    }
                }
            }
            return s;
        }

        protected static void SerializeSize(XmlWriter w, string name, Size s)
        {
            w.WriteStartElement(name);
            w.WriteElementString("Width", s.Width.ToString());
            w.WriteElementString("Height", s.Height.ToString());
            w.WriteEndElement();
        }
    }

}
