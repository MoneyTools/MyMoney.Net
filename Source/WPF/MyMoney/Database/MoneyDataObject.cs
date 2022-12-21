using System;
using System.Windows;
using Walkabout.Data;

namespace Walkabout.Database
{
    public class MoneyDataObject : System.Windows.IDataObject
    {
        private readonly PersistentObject data;
        private readonly string xml;

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
            return this.GetData(format);
        }

        public bool GetDataPresent(Type format)
        {
            return format == typeof(string) ||
                    format == typeof(MoneyDataObject);
        }

        public bool GetDataPresent(string format)
        {
            return format == DataFormats.Text ||
                format == DataFormats.GetDataFormat("XML").Name ||
                format == DataFormats.GetDataFormat(typeof(MoneyDataObject).FullName).Name;
        }

        public bool GetDataPresent(string format, bool autoConvert)
        {
            return this.GetDataPresent(format);
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
            return this.GetFormats();
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
