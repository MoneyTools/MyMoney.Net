using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Walkabout.Data
{
    public class TableMapping : Attribute
    {
        List<ColumnMapping> columns;
        Type objectType;

        public string TableName { get; set; }

        public Type ObjectType 
        {
            get { return objectType; }
            set {
                objectType = value;
                columns = MappingEngine.GetColumnsFromObject(objectType);
            }
        }

        public List<ColumnMapping> Columns
        {
            get
            {
                if (columns == null)
                {
                    Debug.Assert(columns != null, "ObjectType should have been set by now");
                }
                return columns;
            }
            set { columns = value; }
        }

        public ColumnMapping FindColumn(string name)
        {
            foreach (ColumnMapping c in columns)
            {
                if (c.ColumnName == name) 
                    return c;
            }
            return null;
        }

        public ColumnMapping FindOldColumn(string name)
        {
            foreach (ColumnMapping c in columns)
            {
                if (!string.IsNullOrEmpty(c.OldColumnName ) && c.OldColumnName == name)
                    return c;
            }
            return null;
        }
    }

    public class ColumnMapping : Attribute
    {
        public string ColumnName { get; set; }
        public bool IsPrimaryKey { get; set; }
        public string OldColumnName { get; set; } // for migration
        public int MaxLength { get; set; } // because the bloody attribute initializer doesn't allow object initializers on the SqlType - I can't believe they did that !!
        public bool AllowNulls { get; set; }
        public int Precision { get; set; }
        public int Scale { get; set; }
        public Type SqlType { get; set; }

        public void GetSqlDefinition(StringBuilder sb)
        {
            GetPartialSqlDefinition(sb);

            if (this.IsPrimaryKey)
            {
                sb.Append(" PRIMARY KEY");
            }
            else if (!this.AllowNulls)
            {
                sb.Append(" NOT NULL");
            }

        }

        // not including NOT NULL part.
        public void GetPartialSqlDefinition(StringBuilder sb)
        {
            sb.Append(string.Format("  [{0}] ", this.ColumnName));
                    
            Type st = this.SqlType;
            if (st != null)
            {
                if (st == typeof(SqlBoolean))
                {
                    sb.Append("bit");
                }
                else if (st == typeof(SqlByte))
                {
                    sb.Append("tinyint");
                }
                else if (st == typeof(SqlInt32))
                {
                    sb.Append("int");
                }
                else if (st == typeof(SqlInt64))
                {
                    sb.Append("bigint");
                }
                else if (st == typeof(SqlMoney))
                {
                    sb.Append("money");
                }
                else if (st == typeof(SqlGuid))
                {
                    sb.Append("uniqueidentifier");
                }
                else if (st == typeof(SqlDecimal))
                {
                    sb.Append("decimal");
                    if (this.Precision > 0)
                    {
                        sb.Append(string.Format("({0},{1})", Math.Min(this.Precision, SqlDecimal.MaxPrecision),  Math.Min(this.Scale, SqlDecimal.MaxScale)));
                    }
                }
                else if (st == typeof(SqlDateTime))
                {
                    sb.Append("datetime");
                }
                else if (st == typeof(SqlChars))
                {
                    Debug.Assert(this.MaxLength != 0, "string properties must provide a MaxLength in the ColumnMapping");
                    if (this.MaxLength < 50)
                    {
                        sb.Append(string.Format("nchar({0})", this.MaxLength));
                    }
                    else
                    {
                        sb.Append(string.Format("nvarchar({0})", this.MaxLength));
                    }
                }
                else if (st == typeof(SqlAscii))
                {
                    Debug.Assert(this.MaxLength != 0, "string properties must provide a MaxLength in the ColumnMapping");
                    if (this.MaxLength < 50)
                    {
                        sb.Append(string.Format("char({0})", this.MaxLength));
                    }
                    else
                    {
                        sb.Append(string.Format("nvarchar({0})", this.MaxLength));
                    }
                }
                else
                {
                    throw new NotImplementedException(string.Format("SqlType {0} is not yet supported in the mapping engine", st.FullName));
                }
            }
            else
            {
                throw new Exception("SqlType is null, it should have been resolved by now");
            }

        }
    }

    /// <summary>
    /// This class can be used to map a property in a System.Object to a scalar field in the database.
    /// For example, Investments contains "Security" object, but in the database we store the Security.Id only.
    /// So the KeyProperty is the "Id" property of the Security object.
    /// </summary>
    class ColumnObjectMapping : ColumnMapping
    {
        public string KeyProperty { get; set; }
    }

    class MappingEngine
    {
        internal static List<ColumnMapping> GetColumnsFromObject(Type objectType)
        {
            List<ColumnMapping> result = new List<ColumnMapping>();
            foreach (PropertyInfo p in objectType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object[] attrs = p.GetCustomAttributes(typeof(ColumnMapping), false);
                if (attrs != null && attrs.Length > 0)
                {
                    Debug.Assert(attrs.Length == 1, "A property must have one and only one ColumnMapping");

                    ColumnMapping column = (ColumnMapping)attrs[0];
                    ResolveColumnType(p.PropertyType, column);
                    result.Add(column);
                }
            }
            return result;
        }

        internal static void ResolveColumnType(Type propertyType, ColumnMapping mapping)
        {
            // fill in the missing SqlType information based on default mapping from property type.
            if (mapping is ColumnObjectMapping)
            {
                ColumnObjectMapping co = (ColumnObjectMapping)mapping;
                string propName = co.KeyProperty;
                if (string.IsNullOrEmpty(propName))
                {
                    throw new Exception("ColumnObjectMapping must have a valid KeyProperty");
                }
                PropertyInfo pi = propertyType.GetProperty(propName);
                if (pi == null)
                {
                    throw new Exception(string.Format("Could not find KeyProperty named '{0}' on class '{1}'", propName, propertyType.FullName));
                }
                // get the dereferenced type.
                propertyType = pi.PropertyType;

            }

            if (propertyType.Name == "Nullable`1")
            {
                Type[] inner = propertyType.GetGenericArguments();
                propertyType = inner[0];
            }

            if (mapping.SqlType == null)
            {
                // the default mapping for things
                if (propertyType == typeof(int) || propertyType == typeof(uint) || propertyType == typeof(SqlInt32))
                {
                    mapping.SqlType = typeof(SqlInt32);
                }
                else if (propertyType == typeof(short) || propertyType == typeof(ushort) || propertyType == typeof(SqlInt16))
                {
                    mapping.SqlType = typeof(SqlInt16);
                }
                else if (propertyType == typeof(long) || propertyType == typeof(SqlInt64))
                {
                    mapping.SqlType = typeof(SqlInt64);
                }
                else if (propertyType == typeof(bool) || propertyType == typeof(SqlBoolean))
                {
                    mapping.SqlType = typeof(SqlBoolean);
                }
                else if (propertyType == typeof(decimal) || propertyType == typeof(SqlDecimal))
                {
                    mapping.SqlType = typeof(SqlDecimal);
                }
                else if (propertyType == typeof(byte) || propertyType == typeof(sbyte) || propertyType == typeof(SqlByte))
                {
                    mapping.SqlType = typeof(SqlByte);
                }
                else if (propertyType == typeof(DateTime) || propertyType == typeof(SqlDateTime))
                {
                    mapping.SqlType = typeof(SqlDateTime);
                }
                else if (propertyType == typeof(Guid) || propertyType == typeof(SqlGuid))
                {
                    mapping.SqlType = typeof(SqlGuid);
                }
                else if (propertyType.IsEnum)
                {
                    mapping.SqlType = typeof(SqlInt32);
                }
                else if (propertyType == typeof(string))
                {
                    mapping.SqlType = typeof(SqlChars);
                }
                else if (propertyType == typeof(char))
                {
                    mapping.SqlType = typeof(SqlChars);
                    mapping.MaxLength = 1;
                }
                else
                {
                    throw new NotImplementedException(string.Format("Default mapping for property type {0} is not yet supported in the mapping engine", propertyType.FullName));
                }
            }
        }

        

    }

}
