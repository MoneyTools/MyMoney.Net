using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Data;
using System.Windows;

namespace Walkabout.Database
{
    public class MoneyDataObject : System.Windows.IDataObject
    {
        PersistentObject data;
        string xml;

        public MoneyDataObject(PersistentObject data)
        {
            this.data = data;
            this.xml = data.Serialize();
        }

        public object GetData(Type format)
        {
            if (format == typeof(string))
            {
                return this.xml;
            }
            else if (format == typeof(MoneyDataObject))
            {
                return this;
            }
            return null;
        }

        public object GetData(string format)
        {
            if (format == DataFormats.Text || format == DataFormats.GetDataFormat("XML").Name)
            {
                return this.xml;
            }
            else if (format == DataFormats.GetDataFormat(typeof(MoneyDataObject).FullName).Name)
            {
                return this;
            }
            return null;
        }

        public object GetData(string format, bool autoConvert)
        {
            return GetData(format);
        }

        public bool GetDataPresent(Type format)
        {
            return (format == typeof(string) ||
                    format == typeof(MoneyDataObject));
        }

        public bool GetDataPresent(string format)
        {
            return (format == DataFormats.Text ||
                format == DataFormats.GetDataFormat("XML").Name ||
                format == DataFormats.GetDataFormat(typeof(MoneyDataObject).FullName).Name);
        }

        public bool GetDataPresent(string format, bool autoConvert)
        {
            return GetDataPresent(format);
        }

        public string[] GetFormats()
        {
            return new string[3] {
                                     DataFormats.Text,
                                     DataFormats.GetDataFormat("XML").Name,
                                     DataFormats.GetDataFormat(typeof(MoneyDataObject).FullName).Name
                                 };
        }

        public string[] GetFormats(bool autoConvert)
        {
            return GetFormats();
        }

        public void SetData(object data)
        {
            throw new NotImplementedException();
        }

        public void SetData(Type format, object data)
        {
            throw new NotImplementedException();
        }

        public void SetData(string format, object data)
        {
            throw new NotImplementedException();
        }

        public void SetData(string format, bool autoConvert)
        {
            throw new NotImplementedException();
        }

        public void SetData(string format, object data, bool autoConvert)
        {
            throw new NotImplementedException();
        }
    }

}
