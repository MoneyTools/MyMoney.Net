// #define DEBUG_DATABASE
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Walkabout.Data
{
    public class SqliteDatabase : SqlServerDatabase
    {
        public SqliteDatabase()
        {
        }

        /// <summary>
        /// Return true if SQL CE is installed.
        /// </summary>
        public static bool IsSqliteInstalled
        {
            get
            {
                return true;
            }
        }

        public static string OfficialSqliteFileExtension = ".mmdb";

        private SQLiteConnection sqliteConnection;


        // bugbug: there's some sort of horrible exponential performance bug in the System.Data.Sqlite wrappers.
        // so for now we have to return false, even though that too is slow.  
        public override bool SupportsBatchUpdate { get { return false; } }

        protected override string GetConnectionString(bool includeDatabase)
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = this.DatabasePath;
            if (!string.IsNullOrEmpty(this.Password))
            {
                builder.Password = this.Password;
            }
            return builder.ConnectionString;
        }

        public override void OnPasswordChanged(string oldPassword, string newPassword)
        {
            this.Connect();
            this.sqliteConnection.ChangePassword(newPassword);

            // if ChangePassword succeeds the next operation needs a new connection with the new password.
            this.Disconnect();
        }

        public override string GetDatabaseFullPath()
        {
            return this.DatabasePath;
        }

        public override void Create()
        {
            string connectionString = this.GetConnectionString(true);

            if (!File.Exists(this.DatabasePath))
            {
                this.LazyCreateTables();
            }
        }

        public override void Delete()
        {
            this.Disconnect();
            if (File.Exists(this.DatabasePath))
            {
                File.Delete(this.DatabasePath);
            }
        }

        public static SqliteDatabase Restore(string backup, string databaseFile, string password)
        {
            string fullBackupPath = Path.GetFullPath(backup);
            string fullDatabasePath = Path.GetFullPath(databaseFile);

            var result = new SqliteDatabase()
            {
                DatabasePath = fullBackupPath,
                Password = password
            };

            result.Connect(); // make sure we can connect to it.
            result.Disconnect();

            // Ok, then we're good to copy it.
            File.Copy(fullBackupPath, fullDatabasePath, true);

            return new SqliteDatabase()
            {
                DatabasePath = fullDatabasePath,
                Password = password,
                BackupPath = fullBackupPath
            };
        }

        public override DbFlavor DbFlavor
        {
            get { return Data.DbFlavor.Sqlite; }
        }

        public override bool UpgradeRequired
        {
            get
            {
                return false;
            }
        }

        public override bool SupportsUserLogin => false;

        public override void Upgrade()
        {
            // TBD
        }

        public override DbConnection Connect()
        {
            if (this.sqliteConnection == null || this.sqliteConnection.State != ConnectionState.Open)
            {
                string constr = this.GetConnectionString(true);
                this.sqliteConnection = new SQLiteConnection(constr);
                this.sqliteConnection.Open();
            }
            return this.sqliteConnection;
        }

        public override void Disconnect()
        {
            if (this.sqliteConnection != null && this.sqliteConnection.State == ConnectionState.Open)
            {
                try
                {
                    using (this.sqliteConnection)
                    {
                        this.sqliteConnection.Close();
                    }
                }
                catch { }
            }
            this.sqliteConnection = null;
        }

        public override bool Exists
        {
            get
            {
                return File.Exists(this.DatabasePath);
            }
        }

        public override bool TableExists(string name)
        {
            //object result = ExecuteScalar("select * from INFORMATION_SCHEMA.tables where table_name = '" + mapping.TableName + "'");
            object result = this.ExecuteScalar("SELECT tbl_name FROM sqlite_master where tbl_name='" + name + "'");
            return result != null;
        }

        /// <summary>
        /// Get the schema of the given table
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>Returns the list of columns found</returns>
        public override List<ColumnMapping> GetTableSchema(string tableName)
        {
            // get the CREATE TABLE statement, the second and subsequent lines are the column information.
            string sql = this.ExecuteScalar("select sql from sqlite_master where tbl_name='" + tableName + "'").ToString();

            // replace comma column separator with newline.
            sql = sql.Replace(",", "\r\n");

            List<ColumnMapping> columns = new List<ColumnMapping>();

            // parse it to find the columns and their type infomration.
            using (StringReader reader = new StringReader(sql))
            {
                bool first = true;
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    if (!first)
                    {
                        line = line.Trim();
                        if (line == ")")
                        {
                            // done
                            break;
                        }
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }


                        ColumnMapping c = this.ParseColumnSql(line.TrimEnd(new char[] { ' ', '\t', '\r', '\n', ',' }));
                        if (c != null)
                        {
                            columns.Add(c);
                        }
                    }
                    first = false;
                }
            }

            return columns;
        }

        /// <summary>
        /// Parse a line of SQL that creates a column and return the mapping for it.
        /// </summary>
        /// <param name="line"></param>
        /// <returns>A ColumnMapping</returns>
        private ColumnMapping ParseColumnSql(string line)
        {
            // eg:   [Id] int NOT NULL,

            ColumnMapping result = new ColumnMapping();
            result.AllowNulls = true;

            int state = 0;

            for (int i = 0, n = line.Length; i < n; i++)
            {
                string id = null;
                char ch = line[i];
                if (char.IsWhiteSpace(ch))
                {
                    // skip whitespace.
                    continue;
                }

                if (ch == '[')
                {
                    // scan for closing bracket.
                    int j = line.IndexOf(']', i);
                    if (j < 0)
                    {
                        return null;
                    }
                    id = line.Substring(i + 1, j - i - 1);
                    i = j;
                }
                else if (state == 2)
                {
                    // take rest of line then.
                    id = line.Substring(i, n - i);
                    i = n;
                }
                else
                {
                    int j = line.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' }, i);
                    if (j < 0)
                    {
                        // take rest of line then.
                        id = line.Substring(i, n - i);
                        i = n;
                    }
                    else
                    {
                        id = line.Substring(i, j - i);
                        i = j;
                    }
                }

                id = id.Trim();
                if (state == 0)
                {
                    result.ColumnName = id;
                    state++;
                }
                else if (state == 1)
                {
                    // ok, now we have a sql data type.
                    this.ParseSqlType(id, result);
                    state++;
                }
                else
                {
                    // NOT NULL
                    // PRIMARY KEY
                    if (id.ToUpperInvariant().Contains("NOT NULL"))
                    {
                        result.AllowNulls = false;
                    }
                    if (id.ToUpperInvariant().Contains("PRIMARY KEY"))
                    {
                        result.IsPrimaryKey = true;
                        result.AllowNulls = false;
                    }
                }
            }
            return result;
        }

        private void ParseSqlType(string type, ColumnMapping result)
        {
            // split nvarchar(20) into [nvarchar][20].
            // split decimal(8,12) into [decimal[9][12]
            string[] parts = type.Split('(', ',', ')');
            Type columnType = null;
            bool hasLength = false;
            bool hasPrecision = false;
            switch (parts[0])
            {
                case "int":
                case "integer":
                case "numeric":
                    columnType = typeof(SqlInt32);
                    break;
                case "char":
                    columnType = typeof(SqlAscii);
                    hasLength = true;
                    break;
                case "nchar":
                case "nvarchar":
                    columnType = typeof(SqlChars);
                    hasLength = true;
                    break;
                case "money":
                    columnType = typeof(SqlMoney);
                    hasPrecision = true;
                    break;
                case "datetime":
                    columnType = typeof(SqlDateTime);
                    break;
                case "uniqueidentifier":
                    columnType = typeof(SqlGuid);
                    break;
                case "decimal":
                    columnType = typeof(SqlDecimal);
                    hasPrecision = true;
                    break;
                case "bigint":
                    columnType = typeof(SqlInt64);
                    break;
                case "smallint":
                    columnType = typeof(SqlInt16);
                    break;
                case "tinyint":
                    columnType = typeof(SqlByte);
                    break;
                case "float":
                    columnType = typeof(SqlSingle);
                    break;
                case "real":
                    columnType = typeof(SqlDouble);
                    break;
                case "bit":
                    columnType = typeof(SqlBoolean);
                    break;
                default:
                    throw new NotImplementedException(string.Format("SQL type '{0}' is not supported by the mapping engine", parts[0]));
            }
            result.SqlType = columnType;

            if (hasLength)
            {
                if (parts.Length > 1)
                {
                    int len = 0;
                    if (int.TryParse(parts[1], out len))
                    {
                        result.MaxLength = len;
                    }
                }
            }
            else if (hasPrecision)
            {
                if (parts.Length > 1)
                {
                    int precision = 0;
                    if (int.TryParse(parts[1], out precision))
                    {
                        result.Precision = precision;
                    }
                }
                if (parts.Length > 2)
                {
                    int scale = 0;
                    if (int.TryParse(parts[2], out scale))
                    {
                        result.Scale = scale;
                    }
                }
            }


        }

        public override object ExecuteScalar(string cmd)
        {
            Debug.Assert(this.DbFlavor == DbFlavor.Sqlite);

            if (cmd == null || cmd.Trim().Length == 0)
            {
                return null;
            }

            this.AppendLog(cmd);
            object result = null;
            try
            {
                this.Connect();
#if DEBUG_DATABASE
                Debug.WriteLine(string.Format("ExecuteScalar {0}", cmd));
#endif
                using (DbCommand command = new SQLiteCommand(cmd, this.sqliteConnection))
                {
                    result = command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing SQL \"" + cmd + "\"\n" + ex.Message);
            }
            return result;
        }

        public override DataSet QueryDataSet(string query)
        {
            Debug.Assert(this.DbFlavor == DbFlavor.Sqlite);

            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            try
            {
                this.Connect();

                DataSet dataSet = new DataSet();

                using (DbDataAdapter da = new SQLiteDataAdapter(query, this.sqliteConnection))
                {
                    da.Fill(dataSet, "Results");
                }

                if (dataSet.Tables.Contains("Results"))
                {
                    return dataSet;
                }

            }
            catch (Exception)
            {
                throw; // useful for setting breakpoints.
            }
            return null;
        }

        public override void ExecuteNonQuery(string cmd)
        {
            Debug.Assert(this.DbFlavor == DbFlavor.Sqlite);
            if (cmd == null || cmd.Trim().Length == 0)
            {
                return;
            }

            this.AppendLog(cmd);
            try
            {
                this.Connect();
#if DEBUG_DATABASE
                Debug.WriteLine(string.Format("ExecuteNonQuery {0}", cmd));
#endif
                using (DbCommand command = new SQLiteCommand(cmd, this.sqliteConnection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception)
            {
                throw; // useful for setting breakpoints.
            }
        }

        public override IDataReader ExecuteReader(string cmd)
        {
            Debug.Assert(this.DbFlavor == DbFlavor.Sqlite);

            this.AppendLog(cmd);

            try
            {
                this.Connect();
#if DEBUG_DATABASE
                Debug.WriteLine(string.Format("ExecuteReader {0}", cmd));
#endif
                using (DbCommand command = new SQLiteCommand(cmd, this.sqliteConnection))
                {
                    return command.ExecuteReader();
                }
            }
            catch (Exception)
            {
                throw; // useful for setting breakpoints.
            }
        }


        public override void Backup(string backupPath)
        {
            this.BackupPath = backupPath;
            File.Copy(this.DatabasePath, backupPath);
        }


        public override void CreateOrUpdateTable(TableMapping mapping)
        {
            if (!this.TableExists(mapping.TableName))
            {
                // this is the easy case, we need to create the table
                string createTable = GetCreateTableScript(mapping);
                this.ExecuteNonQuery(createTable);
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                StringBuilder log = new StringBuilder();

                using (var transaction = this.sqliteConnection.BeginTransaction())
                {

                    // the hard part, figure out if table needs to be altered...                
                    TableMapping actual = this.LoadTableMetadata(mapping.TableName);

                    // Lastly see if any types or maxlengths need to be changed.
                    // Unfortunately SqlLite does not support ALTER a column!!
                    // See https://www.sqlite.org/lang_altertable.html

                    bool newTable = false;
                    foreach (ColumnMapping c in actual.Columns)
                    {
                        ColumnMapping ac = mapping.FindColumn(c.ColumnName);
                        if (ac != null)
                        {
                            if (c.MaxLength != ac.MaxLength || c.SqlType != ac.SqlType || c.Precision != ac.Precision || c.Scale != ac.Scale ||
                                c.AllowNulls != ac.AllowNulls)
                            {
                                // bummer, so the only way to do this is to copy all the data over to a new table!
                                newTable = true;
                                break;
                            }
                        }
                    }
                    List<ColumnMapping> newColumns = new List<ColumnMapping>();
                    List<ColumnMapping> renames = new List<ColumnMapping>();
                    // See if any new columns need to be added.
                    foreach (ColumnMapping c in mapping.Columns)
                    {
                        ColumnMapping ac = actual.FindColumn(c.ColumnName);
                        if (ac == null)
                        {
                            if (!string.IsNullOrEmpty(c.OldColumnName))
                            {
                                ac = actual.FindColumn(c.OldColumnName);
                                if (ac != null)
                                {
                                    // this column needs to be renamed, ALTER TABLE doesn't allow renames, so the most efficient way to do it
                                    // is add the new column, do an UPDATE to copy all the values over, then DROP the old column.
                                    if (c.IsPrimaryKey || ac.IsPrimaryKey)
                                    {
                                        newTable = true; // then we need to create a new table!
                                    }
                                    // we deliberately do NOT copy the AllowNulls because we can't set that yet.
                                    ColumnMapping clone = new ColumnMapping()
                                    {
                                        ColumnName = c.ColumnName,
                                        OldColumnName = c.OldColumnName,
                                        MaxLength = c.MaxLength,
                                        Precision = c.Precision,
                                        Scale = c.Scale,
                                        SqlType = c.SqlType,
                                        AllowNulls = true // because we can't set this initially until we populate the column.
                                    };
                                    renames.Add(clone);
                                }
                                else
                                {
                                    newColumns.Add(c);
                                }
                            }
                            else
                            {
                                newColumns.Add(c);
                            }
                        }
                    }

                    // See if any columns need to be dropped.
                    foreach (ColumnMapping c in actual.Columns)
                    {
                        ColumnMapping ac = mapping.FindColumn(c.ColumnName);
                        if (ac == null)
                        {
                            // bummer, sqllite can't drop columns.
                            newTable = true;
                            break;
                        }
                    }

                    if (newTable)
                    {
                        // invent a new name for the temporary table
                        string originalTableName = mapping.TableName;
                        mapping.TableName = "NEW_" + originalTableName;
                        string createTable = GetCreateTableScript(mapping);
                        this.ExecuteNonQuery(createTable);

                        // copy the data across to this new table, taking any "renames" into account.
                        // INSERT INTO new_X SELECT ... FROM X
                        sb.Length = 0;
                        sb.Append(string.Format("INSERT INTO [{0}] (", mapping.TableName));

                        StringBuilder select = new StringBuilder();
                        select.Append("SELECT ");
                        bool first = true;

                        foreach (ColumnMapping c in actual.Columns)
                        {
                            ColumnMapping ac = mapping.FindColumn(c.ColumnName);
                            if (ac == null)
                            {
                                if (!string.IsNullOrEmpty(c.OldColumnName))
                                {
                                    // renaming a column
                                    ac = actual.FindColumn(c.OldColumnName);
                                    if (ac != null)
                                    {
                                        if (!first)
                                        {
                                            sb.Append(", ");
                                            select.Append(", ");
                                        }
                                        sb.Append("[" + c.ColumnName + "]");
                                        select.Append("[" + c.OldColumnName + "]");
                                        first = false;
                                    }
                                    else
                                    {
                                        // already been renamed, now we are dropping it.
                                    }
                                }
                                else
                                {
                                    // dropping this column
                                }
                            }
                            else
                            {
                                // copy as is.
                                if (!first)
                                {
                                    sb.Append(", ");
                                    select.Append(", ");
                                }
                                sb.Append("[" + c.ColumnName + "]");
                                select.Append("[" + c.ColumnName + "]");
                                first = false;
                            }
                        }

                        if (first)
                        {
                            throw new Exception("Invalid table definition, no columns found in the old table");
                        }

                        sb.Append(") ");
                        select.Append(string.Format(" FROM [{0}]", originalTableName));
                        sb.Append(select.ToString());
                        this.ExecuteNonQuery(sb.ToString());

                        // drop the old table, now that we've copied all the data into the new one
                        this.ExecuteNonQuery(string.Format("DROP TABLE [{0}]", originalTableName));

                        // rename new table to original table name.
                        this.ExecuteNonQuery(string.Format("ALTER TABLE [{0}] RENAME TO [{1}]", mapping.TableName, originalTableName));

                        mapping.TableName = originalTableName;
                    }
                    else
                    {
                        if (newColumns.Count > 0)
                        {
                            // create the new column that was added.
                            foreach (ColumnMapping c in newColumns)
                            {
                                sb.Append(string.Format("ALTER TABLE [{0}] ADD ", mapping.TableName));
                                c.GetPartialSqlDefinition(sb);
                                string cmd = sb.ToString();
                                log.AppendLine(cmd);
                                this.ExecuteScalar(cmd);
                                sb.Length = 0;
                            }
                        }

                        if (renames.Count > 0)
                        {
                            // create the new column that was added.
                            foreach (ColumnMapping c in renames)
                            {
                                sb.Append(string.Format("ALTER TABLE [{0}] ADD ", mapping.TableName));
                                c.GetPartialSqlDefinition(sb);
                                string cmd = sb.ToString();
                                log.AppendLine(cmd);
                                this.ExecuteScalar(cmd);
                                sb.Length = 0;
                            }

                            // now copy the data across to the new column that was added.
                            foreach (ColumnMapping c in renames)
                            {
                                if (sb.Length > 0)
                                {
                                    sb.AppendLine(",");
                                }
                                sb.Append(string.Format("[{0}] = [{1}]", c.ColumnName, c.OldColumnName));
                                actual.Columns.Add(c);
                            }

                            string update = string.Format("UPDATE [{0}] SET ", mapping.TableName) + sb.ToString();
                            log.AppendLine(update);
                            this.ExecuteScalar(update);
                            sb.Length = 0;
                        }

                        // See if any columns need to be dropped.
                        foreach (ColumnMapping c in actual.Columns)
                        {
                            ColumnMapping ac = mapping.FindColumn(c.ColumnName);
                            if (ac == null)
                            {
                                string drop = string.Format("ALTER TABLE [{0}]  DROP  COLUMN [{1}] ", mapping.TableName, c.ColumnName);
                                log.AppendLine(drop);
                                this.ExecuteScalar(drop);
                            }
                        }

                    }

                    // Ok, commit the transaction!
                    transaction.Commit();
                }


                if (log.Length > 0)
                {
                    this.AppendLog(log.ToString());
                }
            }
        }

    }
}
