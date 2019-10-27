using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Walkabout.Utilities;
using Walkabout.Controls;
using Walkabout.Configuration;
using System.Globalization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
using Walkabout.Dialogs;

namespace Walkabout.Data
{
    public enum DbFlavor
    {
        None,
        SqlServer,
        SqlCE,
        Sqlite,
        Xml,
        BinaryXml
    }

    public enum ConnectMode
    {
        Create,
        Connect,
        Restore
    }

    public static class DatabaseSecurity
    {
        public static string LoadDatabasePassword(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return "";
            }
            string name = "MyMoney=" + ("" + databaseName).Trim().ToLowerInvariant();
            using (Credential credential = new Credential(name, CredentialType.Generic))
            {
                credential.UserName = Environment.GetEnvironmentVariable("USERNAME");
                credential.Persistence = CredentialPersistence.LocalComputer;
                credential.Load();
                return Credential.SecureStringToString(credential.Password);
            }
        }

        public static void SaveDatabasePassword(string databaseName, string password)
        {
            string name = "MyMoney=" + ("" + databaseName).Trim().ToLowerInvariant();
            using (Credential credential = new Credential(name, CredentialType.Generic))
            {
                try
                {
                    credential.UserName = Environment.GetEnvironmentVariable("USERNAME");
                    credential.Persistence = CredentialPersistence.LocalComputer;
                    credential.Password = Credential.ToSecureString(password);
                    credential.Save();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("### Credential Error: {0}", ex.Message);
                }
            }
        }

    }

    /// <summary>
    /// This class provides an abstraction over the SQL database.
    /// </summary>
    public class SqlServerDatabase : IDatabase
    {
        #region Fields
        private System.Collections.Generic.List<string> tableNames = new System.Collections.Generic.List<string>();
        private string path;
        private StringBuilder log = new StringBuilder();

        private IStatusService status;

        //private string connectionString = "data source=localhost;initial catalog=mymoney;integrated security=SSPI;user id=sa;";
        private string server;
        private string backupPath;
        private string userId;
        private string password;

        #endregion 

        // this is used to give SQL Server access to the database directory.
        public IDirectorySecurity SecurityService { get; set; }

        public string DatabasePath
        {
            get { return path; }
            set { path = value; }
        }

        public string Server
        {
            get { return server; }
            set { server = value; }
        }

        public string UserId
        {
            get { return userId; }
            set { userId = value; }
        }

        public string Password
        {
            get { return password; }
            set
            {
                if (password != null && password != value)
                {
                    OnPasswordChanged(password, value);
                }
                password = value;
            }
        }

        public virtual void OnPasswordChanged(string oldPassword, string newPassword)
        {
            // todo: implement changing password on SQL user account....
            throw new Exception("Sorry ALTER LOGIN is not supported yet, you could try using File/SaveAs instead...");
        }

        public string BackupPath
        {
            get { return backupPath; }
            set { this.backupPath = value; }
        }

        public string DatabaseName
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.DatabasePath);
            }
        }

        public virtual bool SupportsBatchUpdate { get { return true; } }

        public virtual bool Exists
        {
            get
            {
                if (string.IsNullOrEmpty(server))
                {
                    return false;
                }
                try
                {
                    using (SqlConnection con = new SqlConnection(GetConnectionString(false)))
                    {
                        con.Open();
                        SqlCommand cmd = new SqlCommand("select name from sysdatabases", con);
                        IDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string name = reader.GetString(0);
                            if (string.Compare(name, this.DatabaseName, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                return true;
                            }
                        }
                        con.Close();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    // nope!
                }
                return false;
            }
        }

        public virtual string GetDatabaseFullPath()
        {
            if (string.IsNullOrEmpty(server))
            {
                return null;
            }
            try
            {
                using (SqlConnection con = new SqlConnection(GetConnectionString(false)))
                {
                    con.Open();
                    SqlCommand cmd = new SqlCommand("select name, filename from sysdatabases", con);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string name = reader.GetString(0);
                        if (string.Compare(name, this.DatabaseName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            return reader.GetString(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                // nope!
            }
            return null;
        }

        public static string GetConnectionString(string server, string database, string userid, string password)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = server;

            if (!string.IsNullOrEmpty(database))
            {
                builder.InitialCatalog = database;
            }
            if (string.IsNullOrEmpty(userid))
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = userid;

                if (!string.IsNullOrEmpty(password))
                {
                    builder.Password = password;
                }
                builder.WorkstationID = Environment.MachineName;
            }
            builder.ConnectTimeout = 20;

            return builder.ConnectionString;
        }

        protected virtual string GetConnectionString(bool includeDatabase)
        {
            return GetConnectionString(this.server, includeDatabase ? this.DatabaseName : null, this.userId, this.password);
        }

        /// <summary>
        /// SQL Express runs as "NETWORK SERVICE", and so the directory we are trying to create the database in needs to
        /// give this service permission.
        /// </summary>
        public void AclDatabasePath(string path)
        {
            var networkService = "NT AUTHORITY\\NETWORK SERVICE";
            SecurityService.AddWritePermission(networkService, path);
        }

        public virtual void Create()
        {
            Disconnect();
            string conStr = GetConnectionString(false);
            using (SqlConnection con = new SqlConnection(conStr))
            {
                con.Open();

                if (!Exists)
                {
                    string createCommand = null;
                    string dir = Path.GetDirectoryName(this.DatabasePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        AclDatabasePath(dir);
                        string logPhysicalPath = Path.Combine(Path.GetDirectoryName(this.DatabasePath), Path.GetFileNameWithoutExtension(this.DatabasePath) + ".ldf");
                        createCommand = string.Format(@"Create Database {0} ON PRIMARY (NAME = Data, FILENAME = '{1}') LOG ON (NAME = Log, FILENAME = '{2}')",
                                                        this.DatabaseName, this.DatabasePath, logPhysicalPath);
                    }
                    else
                    {
                        string.Format(@"Create Database {0}", this.DatabaseName);
                    }

                    using (SqlCommand cmd2 = new SqlCommand(createCommand, con))
                    {
                        cmd2.ExecuteNonQuery();
                    }
                }

                if (con != null && con.State != ConnectionState.Closed)
                {
                    con.Close();
                }
            }
        }

        public virtual DbFlavor DbFlavor
        {
            get { return DbFlavor.SqlServer; }
        }

        public virtual bool UpgradeRequired
        {
            get
            {
                try
                {
                    Connect();
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        public virtual void Upgrade()
        {
            // nothing to do, yet...
        }

        /// <summary>
        /// Do whatever it takes on SqlServer to query the metadata and see what tables need to be created and/or altered.
        /// </summary>
        public void LazyCreateTables()
        {
            foreach (Type t in this.GetType().Assembly.GetTypes())
            {
                object[] attrs = t.GetCustomAttributes(typeof(TableMapping), false);
                if (attrs != null && attrs.Length > 0)
                {
                    Debug.Assert(attrs.Length == 1, "A class must have one and only one TableMapping attribute");
                    TableMapping mapping = attrs[0] as TableMapping;
                    mapping.ObjectType = t;
                    tableNames.Add(mapping.TableName);
                    CreateOrUpdateTable(mapping);
                }
            }
        }

        internal static string GetCreateTableScript(TableMapping mapping)
        {
            /* for example:
             create table OnlineAccounts (
                [Id] int PRIMARY KEY,
                [Name] nvarchar(80) NOT NULL,
                [Institution] nvarchar(80),
                [OFX] nvarchar(255),
                [FID] char(10),
                [UserId] char(20),
                [Password] char(20),
                [BankId] char(10),
                [BranchId] char(10),
                [OfxVersion] char(10)
            )
             */
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("create table [{0}] (", mapping.TableName));
            bool first = true;
            foreach (ColumnMapping column in mapping.Columns)
            {
                // generate SQL...
                if (!first)
                {
                    sb.AppendLine(",");
                }
                column.GetSqlDefinition(sb);

                first = false;
            }
            sb.AppendLine();
            sb.AppendLine(")");
            return sb.ToString();
        }

        public virtual void CreateOrUpdateTable(TableMapping mapping)
        {
            if (!this.TableExists(mapping.TableName))
            {
                // this is the easy case, we need to create the table
                string createTable = GetCreateTableScript(mapping);
                this.ExecuteNonQuery(createTable);
            }
            else
            {
                StringBuilder log = new StringBuilder();

                // the hard part, figure out if table needs to be altered...                
                TableMapping actual = LoadTableMetadata(mapping.TableName);
                List<ColumnMapping> renames = new List<ColumnMapping>();
                StringBuilder sb = new StringBuilder();
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
                                    throw new NotImplementedException("Cannot rename the primary key automatically, sorry");
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
                        }


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
                    sb.Length = 0;
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

                // Lastly see if any types or maxlengths need to be changed.
                foreach (ColumnMapping c in actual.Columns)
                {
                    ColumnMapping ac = mapping.FindColumn(c.ColumnName);
                    if (ac != null)
                    {
                        if (c.MaxLength != ac.MaxLength || c.SqlType != ac.SqlType || c.Precision != ac.Precision || c.Scale != ac.Scale ||
                            c.AllowNulls != ac.AllowNulls)
                        {
                            if (sb.Length > 0)
                            {
                                sb.AppendLine(",");
                            }
                            sb.Append(string.Format("ALTER TABLE [{0}]  ALTER COLUMN ", mapping.TableName));
                            ac.GetSqlDefinition(sb);

                            string alter = sb.ToString();
                            log.AppendLine(alter);
                            this.ExecuteScalar(alter);
                            sb.Length = 0;
                        }
                    }
                }

                if (log.Length > 0)
                {
                    this.AppendLog(log.ToString());
                }
            }
        }

        public TableMapping LoadTableMetadata(string tableName)
        {
            TableMapping table = new TableMapping();
            table.TableName = tableName;

            List<ColumnMapping> columns = this.GetTableSchema(tableName);

            table.Columns = columns;
            return table;
        }

        #region IDatabase

        int progressMax;
        int progressLastReport;
        int progressValue;

        private void IncrementProgress(string name)
        {
            progressValue++;
            if ((100 * ((double)progressValue - (double)progressLastReport) / (double)progressMax) > 1.0 && status != null)
            {
                status.ShowProgress("Loading database:", 0, progressMax, progressValue);
                progressLastReport = progressValue;
            }
        }


        public MyMoney Load(IStatusService status)
        {
            this.status = status;

            log.Clear();

            this.LazyCreateTables();
            MyMoney money = new MyMoney();
            money.BeginUpdate(this);
            try
            {
                // Must be done in the right order so all object references can be resolved properly

                if (status != null)
                {
                    progressMax = 0;
                    progressValue = 0;
                    progressLastReport = 0;
                    foreach (string name in tableNames)
                    {
                        progressMax += CountTableRows(name);
                    }
                    status.ShowProgress("Loading database:", 0, progressMax, progressValue);
                }
                this.ReadOnlineAccounts(money.OnlineAccounts, money);
                this.ReadCategories(money.Categories, money);  // Must populate the Categories before the Account, since the Account now have 2 fields pointing to Categories
                this.ReadAccounts(money.Accounts, money);
                this.ReadPayees(money.Payees, money);
                this.ReadAliases(money.Payees, money);

                this.ReadSecurities(money.Securities, money);
                this.ReadStockSplits(money.StockSplits, money);
                this.ReadCurrencies(money.Currencies, money);
                this.ReadTransactions(money.Transactions, money);


                // Loan and Building are loaded last since they use the Transactions for looking up totals of expenses per Buildings
                this.ReadLoanPayments(money.LoanPayments, money);
                this.ReadRentBuildings(money.Buildings, money);

            }
            finally
            {
                money.FlushUpdates();
                money.EndUpdate();
            }
            if (status != null)
            {
                status.ShowProgress(string.Empty, 0, 0, 0);
            };
            return money;
        }

        SqlTransaction transaction;

        public void Save(MyMoney money)
        {
            log.Clear();

            using (var tran = this.Connect().BeginTransaction())
            {
                transaction = tran as SqlTransaction;
                try
                {
                    UpdateOnlineAccounts(money.OnlineAccounts);
                    UpdateAccounts(money.Accounts);
                    UpdatePayees(money.Payees);
                    UpdateAliases(money.Aliases);
                    UpdateCategories(money.Categories);
                    UpdateCurrencies(money.Currencies);
                    UpdateTransactions(money.Transactions);
                    UpdateSecurities(money.Securities);
                    UpdateStockSplits(money.StockSplits);
                    UpdateBuildings(money.Buildings);
                    UpdateLoanPayments(money.LoanPayments);

                    tran.Commit();
                }
                finally
                {
                    transaction = null;
                }
            }
        }
        #endregion


        public void UpdateBuildings(RentBuildings buildings)
        {
            UpdateRentUnits(buildings.Units);
            UpdateRentBuildings(buildings);
        }

        public static bool IsSqlExpressInstalled
        {
            get
            {
                try
                {
                    // got this code from this article:
                    // http://msdn.microsoft.com/en-us/library/bb264562(SQL.90).aspx
                    using (RegistryKey Key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Microsoft SQL Server\\", false))
                    {
                        if (Key == null) return false;
                        string[] strNames;
                        strNames = Key.GetSubKeyNames();

                        //If we cannot find a SQL Server registry key, we don't have SQL Server Express installed
                        if (strNames.Length == 0) return false;

                        foreach (string s in strNames)
                        {
                            if (s.StartsWith("MSSQL") && s.EndsWith("SQLEXPRESS"))
                            {
                                using (RegistryKey subKey = Key.OpenSubKey(s, false))
                                {
                                    using (RegistryKey setup = subKey.OpenSubKey("Setup", false))
                                    {
                                        if (setup != null)
                                        {
                                            string edition = (string)setup.GetValue("Edition");
                                            if (edition == "Express Edition")
                                            {
                                                //If there is at least one instance of SQL Server Express installed, return true
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // must not be there then.
                }
                return false;
            }
        }


        public static bool IsSqlLocalDbInstalled
        {
            get
            {
                try
                {
                    // got this code from this article:
                    // http://msdn.microsoft.com/en-us/library/bb264562(SQL.90).aspx
                    using (RegistryKey Key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Microsoft SQL Server\\", false))
                    {
                        if (Key == null) return false;
                        string[] strNames;
                        strNames = Key.GetSubKeyNames();

                        //If we cannot find a SQL Server registry key, we don't have SQL Server Express installed
                        if (strNames.Length == 0) return false;

                        foreach (string s in strNames)
                        {
                            if (s.StartsWith("MSSQL") && s.EndsWith("LOCALDB"))
                            {
                                using (RegistryKey subKey = Key.OpenSubKey(s, false))
                                {
                                    using (RegistryKey setup = subKey.OpenSubKey("Setup", false))
                                    {
                                        if (setup != null)
                                        {
                                            string edition = (string)setup.GetValue("Edition");
                                            if (edition == "Express Edition")
                                            {
                                                //If there is at least one instance of SQL Server Express installed, return true
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // must not be there then.
                }
                return false;
            }
        }

        /// <summary>
        /// Create SqlConnection so we can start executing commands.
        /// </summary>
        public virtual System.Data.Common.DbConnection Connect()
        {
            return ConnectSqlServer();
        }

        public virtual void Disconnect()
        {
            if (this.sqlConnection != null)
            {
                using (this.sqlConnection)
                {
                    try
                    {
                        if (this.sqlConnection.State != ConnectionState.Closed)
                        {
                            this.sqlConnection.Close();
                        }
                    }
                    catch
                    {
                    }
                }

            }
            this.sqlConnection = null;
        }

        private SqlConnection sqlConnection;

        public SqlConnection ConnectSqlServer()
        {
            if (this.sqlConnection == null || this.sqlConnection.State != ConnectionState.Open)
            {
                if (this.sqlConnection != null)
                {
                    this.sqlConnection.Dispose();
                }
                this.sqlConnection = new SqlConnection(GetConnectionString(true));
                this.sqlConnection.Open();
            }
            return this.sqlConnection;
        }

        public virtual object ExecuteScalar(string cmd)
        {
            if (cmd == null || cmd.Trim().Length == 0) return null;

            this.log.AppendLine(cmd);

            object result = null;
            try
            {
                using (SqlCommand command = new SqlCommand(cmd, ConnectSqlServer(), transaction))
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


        public virtual string GetLog()
        {
            return log.ToString();
        }

        public virtual DataSet QueryDataSet(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            try
            {
                using (DataSet dataSet = new DataSet())
                {
                    using (SqlDataAdapter da = new SqlDataAdapter(query, ConnectSqlServer()))
                    {
                        da.Fill(dataSet, "Results");
                    }
                    if (dataSet.Tables.Contains("Results"))
                    {
                        return dataSet;
                    }
                }

            }
            catch (Exception)
            {
                throw; // useful for setting breakpoints.
            }
            return null;
        }

        public virtual void ExecuteNonQuery(string cmd)
        {
            if (cmd == null || cmd.Trim().Length == 0) return;
            this.log.AppendLine(cmd);
            try
            {
                using (SqlCommand command = new SqlCommand(cmd, ConnectSqlServer(), transaction))
                {
                    command.CommandTimeout = 30;
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception)
            {
                throw; // useful for setting breakpoints.
            }
        }

        public virtual IDataReader ExecuteReader(string cmd)
        {

            this.log.AppendLine(cmd);

            try
            {
                using (SqlCommand command = new SqlCommand(cmd, ConnectSqlServer(), transaction))
                {
                    return command.ExecuteReader();
                }
            }
            catch (Exception)
            {
                throw; // useful for setting breakpoints.
            }
        }


        public virtual void Delete()
        {
            Disconnect();
            string conStr = GetConnectionString(false);
            using (SqlConnection con = new SqlConnection(conStr))
            {
                con.Open();

                if (this.Exists)
                {
                    using (SqlCommand cmd2 = new SqlCommand("DROP DATABASE " + this.DatabaseName, con))
                    {
                        cmd2.ExecuteNonQuery();
                    }
                }

                if (con != null && con.State != ConnectionState.Closed)
                {
                    con.Close();
                }
            }
        }

        public void AddLogin(string userName, string password)
        {
            ExecuteScalar("sp_addLogin '" + userName + "','" + password + "'");
            ExecuteScalar("sp_addsrvrolemember '" + userName + "','sysadmin'");


            // Have to turn on mixed mode.
            // http://www.eggheadcafe.com/articles/20040703.asp

            using (RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL.1\MSSQLServer", true))
            {
                if (reg != null)
                {
                    object value = reg.GetValue("LoginMode");
                    if ((int)value != 2)
                    {
                        // Must be set to 2 for mixed mode to work.
                        reg.SetValue("LoginMode", 2);

                        // TODO - we need to remove any UI from the DataBase model lower layer
                        MessageBox.Show("Please restart your SQL Service in order for mixed mode logins to work", "Mixed Mode Enabled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        static string DBString(string s)
        {
            if (s == null) return null;
            return s.Replace("'", "''");
        }

        static string TwoDigit(int i)
        {
            if (i < 10) return "0" + i;
            return i.ToString();
        }

        static string DBDateTime(DateTime dt)
        {
            if (dt == DateTime.MinValue)
                return "NULL";
            else
                return "'" + dt.Year + "-" + TwoDigit(dt.Month) + "-" + TwoDigit(dt.Day) + " " + TwoDigit(dt.Hour) + ":" + TwoDigit(dt.Minute) + ":" + TwoDigit(dt.Second) + "'";
        }

        static string DBNullableDateTime(DateTime? ndt)
        {
            if (!ndt.HasValue)
            {
                return "NULL";
            }
            else
            {
                DateTime dt = ndt.Value;
                if (dt == DateTime.MinValue)
                    return "NULL";
                else
                    return "'" + dt.Year + "-" + TwoDigit(dt.Month) + "-" + TwoDigit(dt.Day) + " " + TwoDigit(dt.Hour) + ":" + TwoDigit(dt.Minute) + ":" + TwoDigit(dt.Second) + "'";
            }
        }

        static string DBGuid(SqlGuid guid)
        {
            if (guid.IsNull)
                return "NULL";
            return "'" + guid.ToString() + "'";
        }

        static string DBDecimal(decimal value)
        {
            return "'" + value.ToString("G", CultureInfo.InvariantCulture) + "'";
        }

        // return 0 if the column is null
        internal static int ReadInt32(IDataReader reader, int i)
        {
            if (reader.IsDBNull(i)) return 0;
            return reader.GetInt32(i);
        }

        // return 0 if the column is null
        internal static decimal ReadDbDecimal(IDataReader reader, int i)
        {
            if (reader.IsDBNull(i)) return 0;
            return reader.GetDecimal(i);
        }

        internal static string ReadDbString(IDataReader reader, int i)
        {
            if (reader.IsDBNull(i)) return null;
            string s = reader.GetString(i);
            if (s != null) s = s.Trim();
            return s;
        }

        #region TABLE ACCCOUNTS
        public void ReadAccounts(Accounts accts, MyMoney money)
        {
            IDataReader reader = ExecuteReader("SELECT Id,AccountId,Name,Type,Description,OnlineAccount,OpeningBalance,LastSync,LastBalance,SyncGuid,Flags,Currency,WebSite,ReconcileWarning,CategoryIdForPrincipal,CategoryIdForInterest,OfxAccountId FROM Accounts");
            accts.BeginUpdate(false);
            accts.Clear();
            while (reader.Read())
            {
                IncrementProgress("Accounts");
                int id = reader.GetInt32(0);
                Account a = accts.AddAccount(id);
                a.AccountId = ReadDbString(reader, 1);
                a.Name = ReadDbString(reader, 2);
                a.Type = (AccountType)reader.GetInt32(3);
                a.Description = ReadDbString(reader, 4);
                id = reader.GetInt32(5);
                a.OnlineAccount = money.OnlineAccounts.FindOnlineAccountAt(id);
                a.OpeningBalance = reader.GetDecimal(6);

                if (!reader.IsDBNull(7))
                    a.LastSync = reader.SafeGetDateTime(7);
                if (!reader.IsDBNull(8))
                    a.LastBalance = reader.SafeGetDateTime(8);
                if (!reader.IsDBNull(9))
                    a.SyncGuid = new SqlGuid(reader.GetGuid(9));
                if (!reader.IsDBNull(10))
                    a.Flags = (AccountFlags)reader.GetInt32(10);
                a.Currency = ReadDbString(reader, 11);
                a.WebSite = ReadDbString(reader, 12);
                a.ReconcileWarning = ReadInt32(reader, 13);

                a.CategoryForPrincipal = money.Categories.FindCategoryById(ReadInt32(reader, 14));
                a.CategoryForInterest = money.Categories.FindCategoryById(ReadInt32(reader, 15));

                a.OfxAccountId = ReadDbString(reader, 16);
                a.OnUpdated();
            }
            accts.EndUpdate();
            accts.FireChangeEvent(accts, accts, null, ChangeType.Reloaded);
            reader.Close();
        }

        public void UpdateAccounts(Accounts accounts)
        {
            if (accounts.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Account a in accounts)
            {
                if (a.IsChanged)
                {
                    sb.AppendLine("-- updating account: " + a.Name);
                    sb.Append("UPDATE Accounts SET ");
                    sb.Append(String.Format("AccountId='{0}'", DBString(a.AccountId)));
                    sb.Append(String.Format(",OfxAccountId='{0}'", DBString(a.OfxAccountId)));
                    sb.Append(String.Format(",Name='{0}'", DBString(a.Name)));
                    sb.Append(String.Format(",Type={0}", ((int)a.Type).ToString()));
                    sb.Append(String.Format(",Description='{0}'", DBString(a.Description)));
                    sb.Append(String.Format(",OnlineAccount={0}", a.OnlineAccount != null ? a.OnlineAccount.Id.ToString() : "-1"));
                    sb.Append(String.Format(",OpeningBalance={0}", a.OpeningBalance.ToString()));
                    sb.Append(String.Format(",LastSync={0}", DBDateTime(a.LastSync)));
                    sb.Append(String.Format(",LastBalance={0}", DBDateTime(a.LastBalance)));
                    sb.Append(String.Format(",SyncGuid={0}", DBGuid(a.SyncGuid)));
                    sb.Append(String.Format(",Flags={0}", ((int)a.Flags).ToString()));
                    sb.Append(String.Format(",Currency='{0}'", DBString(a.Currency)));
                    sb.Append(String.Format(",WebSite='{0}'", DBString(a.WebSite)));
                    sb.Append(String.Format(",ReconcileWarning={0}", a.ReconcileWarning));
                    sb.Append(String.Format(",CategoryIdForPrincipal='{0}'", a.CategoryForPrincipal == null ? "-1" : a.CategoryForPrincipal.Id.ToString()));
                    sb.Append(String.Format(",CategoryIdForInterest='{0}'", a.CategoryForInterest == null ? "-1" : a.CategoryForInterest.Id.ToString()));

                    sb.AppendLine(String.Format(" WHERE Id={0};", a.Id));
                }
                else if (a.IsInserted)
                {
                    sb.AppendLine("-- inserting account: " + a.Name);
                    sb.Append("INSERT INTO Accounts (Id,AccountId,OfxAccountId,Name,Type,Description,OnlineAccount,OpeningBalance,LastSync,LastBalance,SyncGuid,Flags,Currency,WebSite,ReconcileWarning,CategoryIdForPrincipal,CategoryIdForInterest) VALUES (");
                    sb.Append(String.Format("{0}", a.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(a.AccountId)));
                    sb.Append(String.Format(",'{0}'", DBString(a.OfxAccountId)));
                    sb.Append(String.Format(",'{0}'", DBString(a.Name)));
                    sb.Append(String.Format(",{0}", ((int)a.Type).ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(a.Description)));
                    sb.Append(String.Format(",{0}", a.OnlineAccount != null ? a.OnlineAccount.Id.ToString() : "-1"));
                    sb.Append(String.Format(",{0}", a.OpeningBalance.ToString()));
                    sb.Append(String.Format(",{0}", DBDateTime(a.LastSync)));
                    sb.Append(String.Format(",{0}", DBDateTime(a.LastBalance)));
                    sb.Append(String.Format(",{0}", DBGuid(a.SyncGuid)));
                    sb.Append(String.Format(",{0}", ((int)a.Flags).ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(a.Currency)));
                    sb.Append(String.Format(",'{0}'", DBString(a.WebSite)));
                    sb.Append(String.Format(",{0}", a.ReconcileWarning));
                    sb.Append(String.Format(",'{0}'", a.CategoryForPrincipal == null ? "-1" : a.CategoryForPrincipal.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", a.CategoryForInterest == null ? "-1" : a.CategoryForInterest.Id.ToString()));
                    sb.AppendLine(");");
                }
                else if (a.IsDeleted)
                {
                    sb.AppendLine("-- deleting account: " + a.Name);
                    sb.AppendLine(string.Format("DELETE FROM Accounts WHERE Id={0};", a.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (Account a in accounts)
            {
                a.OnUpdated();
            }

            accounts.RemoveDeleted();
        }
        #endregion

        private int CountTableRows(string tableName)
        {
            using (IDataReader reader = ExecuteReader("SELECT Count(*) as Count FROM " + tableName))
            {
                while (reader.Read())
                {
                    return reader.GetInt32(0);
                }
                reader.Close();
            }
            return 0;
        }

        #region TABLE ONLINE ACCCOUNTS

        public void ReadOnlineAccounts(OnlineAccounts onlineAccounts, MyMoney money)
        {
            onlineAccounts.Clear();
            IDataReader reader = ExecuteReader("SELECT Id,Name,Institution,OFX,FID,UserId,Password,BankId,BranchId,BrokerId,OfxVersion,LogoUrl,AppId,AppVersion,ClientUid,UserCred1,UserCred2,AuthToken,AccessKey,UserKey,UserKeyExpireDate FROM OnlineAccounts");
            onlineAccounts.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("OnlineAccounts");
                int id = reader.GetInt32(0);
                OnlineAccount i = onlineAccounts.AddOnlineAccount(id);
                i.Name = ReadDbString(reader, 1);
                i.Institution = ReadDbString(reader, 2);
                i.Ofx = ReadDbString(reader, 3);
                i.FID = ReadDbString(reader, 4);
                i.UserId = ReadDbString(reader, 5);
                i.Password = ReadDbString(reader, 6);
                i.BankId = ReadDbString(reader, 7);
                i.BranchId = ReadDbString(reader, 8);
                i.BrokerId = ReadDbString(reader, 9);
                i.OfxVersion = ReadDbString(reader, 10);
                i.LogoUrl = ReadDbString(reader, 11);
                i.AppId = ReadDbString(reader, 12);
                i.AppVersion = ReadDbString(reader, 13);
                i.ClientUid = ReadDbString(reader, 14);
                i.UserCred1 = ReadDbString(reader, 15);
                i.UserCred2 = ReadDbString(reader, 16);
                i.AuthToken = ReadDbString(reader, 17);
                i.AccessKey = ReadDbString(reader, 18);
                i.UserKey = ReadDbString(reader, 19);
                if (!reader.IsDBNull(20))
                    i.UserKeyExpireDate = reader.SafeGetDateTime(20);

                i.OnUpdated();
            }
            onlineAccounts.EndUpdate();
            onlineAccounts.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            reader.Close();
        }

        public void UpdateOnlineAccounts(OnlineAccounts accounts)
        {
            if (accounts.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (OnlineAccount i in accounts)
            {
                if (i.IsChanged)
                {
                    sb.AppendLine("-- updating online account: " + i.Name);
                    sb.Append("UPDATE OnlineAccounts SET ");
                    sb.Append(String.Format("Name='{0}'", DBString(i.Name)));
                    sb.Append(String.Format(",Institution='{0}'", DBString(i.Institution)));
                    sb.Append(String.Format(",OFX='{0}'", DBString(i.Ofx)));
                    sb.Append(String.Format(",FID='{0}'", DBString(i.FID)));
                    sb.Append(String.Format(",UserId='{0}'", DBString(i.UserId)));
                    sb.Append(String.Format(",Password='{0}'", DBString(i.Password)));
                    sb.Append(String.Format(",BankId='{0}'", DBString(i.BankId)));
                    sb.Append(String.Format(",BranchId='{0}'", DBString(i.BranchId)));
                    sb.Append(String.Format(",BrokerId='{0}'", DBString(i.BrokerId)));
                    sb.Append(String.Format(",OfxVersion='{0}'", DBString(i.OfxVersion)));
                    sb.Append(String.Format(",LogoUrl='{0}'", DBString(i.LogoUrl)));
                    sb.Append(String.Format(",AppId='{0}'", DBString(i.AppId)));
                    sb.Append(String.Format(",AppVersion='{0}'", DBString(i.AppVersion)));
                    sb.Append(String.Format(",ClientUid='{0}'", DBString(i.ClientUid)));
                    sb.Append(String.Format(",UserCred1='{0}'", DBString(i.UserCred1)));
                    sb.Append(String.Format(",UserCred2='{0}'", DBString(i.UserCred2)));
                    sb.Append(String.Format(",AuthToken='{0}'", DBString(i.AuthToken)));
                    sb.Append(String.Format(",AccessKey='{0}'", DBString(i.AccessKey)));
                    sb.Append(String.Format(",UserKey='{0}'", DBString(i.UserKey)));
                    sb.Append(String.Format(",UserKeyExpireDate={0}", DBNullableDateTime(i.UserKeyExpireDate)));
                    sb.AppendLine(String.Format(" WHERE Id={0};", i.Id));
                }
                else if (i.IsInserted)
                {
                    sb.AppendLine("-- inserting online account: " + i.Name);
                    sb.Append("INSERT INTO OnlineAccounts (Id,Name,Institution,OFX,FID,UserId,Password,BankId,BranchId,BrokerId,OfxVersion,LogoUrl,AppId,AppVersion,ClientUid,UserCred1,UserCred2,AuthToken,AccessKey,UserKey,UserKeyExpireDate) VALUES (");
                    sb.Append(String.Format("{0}", i.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(i.Name)));
                    sb.Append(String.Format(",'{0}'", DBString(i.Institution)));
                    sb.Append(String.Format(",'{0}'", DBString(i.Ofx)));
                    sb.Append(String.Format(",'{0}'", DBString(i.FID)));
                    sb.Append(String.Format(",'{0}'", DBString(i.UserId)));
                    sb.Append(String.Format(",'{0}'", DBString(i.Password)));
                    sb.Append(String.Format(",'{0}'", DBString(i.BankId)));
                    sb.Append(String.Format(",'{0}'", DBString(i.BranchId)));
                    sb.Append(String.Format(",'{0}'", DBString(i.BrokerId)));
                    sb.Append(String.Format(",'{0}'", DBString(i.OfxVersion)));
                    sb.Append(String.Format(",'{0}'", DBString(i.LogoUrl)));
                    sb.Append(String.Format(",'{0}'", DBString(i.AppId)));
                    sb.Append(String.Format(",'{0}'", DBString(i.AppVersion)));
                    sb.Append(String.Format(",'{0}'", DBString(i.ClientUid)));
                    sb.Append(String.Format(",'{0}'", DBString(i.UserCred1)));
                    sb.Append(String.Format(",'{0}'", DBString(i.UserCred2)));
                    sb.Append(String.Format(",'{0}'", DBString(i.AuthToken)));
                    sb.Append(String.Format(",'{0}'", DBString(i.AccessKey)));
                    sb.Append(String.Format(",'{0}'", DBString(i.UserKey)));
                    sb.Append(String.Format(",{0}", DBNullableDateTime(i.UserKeyExpireDate)));
                    sb.AppendLine(");");
                }
                else if (i.IsDeleted)
                {
                    sb.AppendLine("-- deleting online account: " + i.Name);
                    sb.AppendLine(string.Format("DELETE FROM OnlineAccounts WHERE Id={0};", i.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (OnlineAccount i in accounts)
            {
                i.OnUpdated();
            }

            accounts.RemoveDeleted();
        }
        #endregion


        #region TABLE PAYEES and ALIAS
        public void ReadPayees(Payees payees, MyMoney money)
        {
            payees.Clear();
            IDataReader reader = ExecuteReader("SELECT Id,Name FROM Payees");
            payees.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("Payees");
                int id = reader.GetInt32(0);
                Payee p = payees.AddPayee(id);
                p.Name = ReadDbString(reader, 1);
                p.OnUpdated();
            }
            payees.EndUpdate();
            payees.FireChangeEvent(payees, payees, null, ChangeType.Reloaded);
            reader.Close();
        }

        public void ReadAliases(Payees payees, MyMoney money)
        {
            Aliases aliases = money.Aliases;
            IDataReader reader = ExecuteReader("SELECT Id,Pattern,Payee,Flags FROM Aliases");
            payees.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("Aliases");
                int id = reader.GetInt32(0);
                Alias a = aliases.AddAlias(id);
                string pattern = ReadDbString(reader, 1);
                a.Pattern = pattern;
                int payeeId = reader.GetInt32(2);
                Payee p = payees.FindPayeeAt(payeeId);
                Debug.Assert(p != null);
                a.Payee = p;
                try
                {
                    a.AliasType = (AliasType)reader.GetInt32(3);
                }
                catch
                {
                    // don't blow up if bad alias reg-ex got saved to DB.
                }
                a.OnUpdated();
            }
            payees.EndUpdate();
            payees.FireChangeEvent(payees, payees, null, ChangeType.Reloaded);
            reader.Close();
        }




        public void UpdatePayees(Payees payees)
        {
            if (payees.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Payee p in payees)
            {
                if (p.IsChanged)
                {
                    sb.AppendLine("-- updating payee: " + p.Name);
                    sb.Append("UPDATE Payees SET ");
                    sb.Append(String.Format("Name='{0}'", DBString(p.Name)));
                    sb.AppendLine(String.Format(" WHERE Id={0};", p.Id));
                }
                else if (p.IsInserted)
                {
                    sb.AppendLine("-- inserting payee: " + p.Name);
                    sb.Append("INSERT INTO Payees (Id, Name) VALUES (");
                    sb.Append(String.Format("{0}", p.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(p.Name)));
                    sb.AppendLine(");");
                }
                else if (p.IsDeleted)
                {
                    sb.AppendLine("-- deleting payee: " + p.Name);
                    sb.AppendLine(string.Format("DELETE FROM Payees WHERE Id={0};", p.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }

            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }
            foreach (Payee p in payees)
            {
                if (p.IsDeleted)
                {
                    p.Parent.RemoveChild(p);
                }
                else
                {
                    p.OnUpdated();
                }
            }
            payees.RemoveDeleted();
        }

        public void UpdateAliases(Aliases aliases)
        {
            if (aliases.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Alias a in aliases)
            {
                if (a.IsChanged)
                {
                    sb.AppendLine("-- updating alias: " + a.Pattern);
                    sb.Append("UPDATE Aliases SET ");
                    sb.Append(String.Format("Pattern='{0}'", DBString(a.Pattern)));
                    sb.Append(String.Format(",Payee='{0}'", a.Payee.Id));
                    sb.Append(String.Format(",Flags='{0}'", ((int)a.AliasType).ToString()));
                    sb.AppendLine(String.Format(" WHERE Id={0};", a.Id));
                }
                else if (a.IsInserted)
                {
                    sb.AppendLine("-- inserting alias: " + a.Pattern);
                    sb.Append("INSERT INTO Aliases (Id, Pattern, Payee, Flags) VALUES (");
                    sb.Append(String.Format("{0}", a.Id));
                    sb.Append(String.Format(",'{0}'", DBString(a.Pattern)));
                    sb.Append(String.Format(",{0}", a.Payee.Id));
                    sb.Append(String.Format(",{0}", ((int)a.AliasType).ToString()));
                    sb.AppendLine(");");
                }
                else if (a.IsDeleted)
                {
                    sb.AppendLine("-- deleting alias: " + a.Pattern);
                    sb.AppendLine(string.Format("DELETE FROM Aliases WHERE Id='{0}';", a.Id));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }

            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }
            foreach (Alias a in aliases)
            {
                a.OnUpdated();
            }

            aliases.RemoveDeleted();
        }

        #endregion



        #region TABLE RENT BUILDING

        public void ReadRentBuildings(RentBuildings collection, MyMoney money)
        {
            this.ReadRentUnits(collection.Units, money);

            collection.Clear();

            IDataReader reader = ExecuteReader("SELECT Id,Name,Address,PurchasedDate,PurchasedPrice,LandValue,EstimatedValue," +
                "CategoryForIncome, CategoryForTaxes, CategoryForInterest, CategoryForRepairs, CategoryForMaintenance,CategoryForManagement,ownershipName1,ownershipName2,OwnershipPercentage1,OwnershipPercentage2,Note FROM RentBuildings");
            collection.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("RentBuildings");
                RentBuilding r = new RentBuilding(collection);
                r.Id = reader.GetInt32(0);
                r.Name = ReadDbString(reader, 1);
                r.Address = ReadDbString(reader, 2);
                r.PurchasedDate = reader.SafeGetDateTime(3);
                r.PurchasedPrice = reader.GetDecimal(4);
                r.LandValue = reader.GetDecimal(5);
                r.EstimatedValue = reader.GetDecimal(6);
                r.CategoryForIncome = ReadInt32(reader, 7);
                r.CategoryForTaxes = ReadInt32(reader, 8);
                r.CategoryForInterest = ReadInt32(reader, 9);
                r.CategoryForRepairs = ReadInt32(reader, 10);
                r.CategoryForMaintenance = ReadInt32(reader, 11);
                r.CategoryForManagement = ReadInt32(reader, 12);

                r.OwnershipName1 = ReadDbString(reader, 13);
                r.OwnershipName2 = ReadDbString(reader, 14);
                r.OwnershipPercentage1 = ReadDbDecimal(reader, 15);
                r.OwnershipPercentage2 = ReadDbDecimal(reader, 16);

                r.Note = ReadDbString(reader, 17);

                foreach (var unit in money.Buildings.Units.GetList().Where(x => x.Building == r.Id).OrderBy(x => x.Id))
                {
                    r.Units.Add(unit);
                }

                collection.AddRentBuilding(r);
                r.OnUpdated();
            }
            collection.EndUpdate();
            reader.Close();
        }

        public void UpdateRentBuildings(RentBuildings buildings)
        {
            if (buildings.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (RentBuilding p in buildings)
            {
                if (p.IsChanged)
                {
                    sb.AppendLine("-- updating RentalBuildings : " + p.Name);
                    sb.Append("UPDATE RentBuildings SET ");
                    sb.Append(String.Format("Name='{0}'", DBString(p.Name)));
                    sb.Append(String.Format(", Address='{0}'", DBString(p.Address)));
                    sb.Append(String.Format(", PurchasedPrice='{0}'", p.PurchasedPrice));
                    sb.Append(String.Format(", LandValue='{0}'", p.LandValue));
                    sb.Append(String.Format(", EstimatedValue='{0}'", p.EstimatedValue));
                    sb.Append(String.Format(", CategoryForIncome='{0}'", p.CategoryForIncome));
                    sb.Append(String.Format(", CategoryForTaxes='{0}'", p.CategoryForTaxes));
                    sb.Append(String.Format(", CategoryForInterest='{0}'", p.CategoryForInterest));
                    sb.Append(String.Format(", CategoryForRepairs='{0}'", p.CategoryForRepairs));
                    sb.Append(String.Format(", CategoryForMaintenance='{0}'", p.CategoryForMaintenance));
                    sb.Append(String.Format(", CategoryForManagement='{0}'", p.CategoryForManagement));

                    sb.Append(String.Format(", OwnershipName1='{0}'", DBString(p.OwnershipName1)));
                    sb.Append(String.Format(", OwnershipName2='{0}'", DBString(p.OwnershipName2)));

                    sb.Append(String.Format(", OwnershipPercentage1='{0}'", p.OwnershipPercentage1));
                    sb.Append(String.Format(", OwnershipPercentage2='{0}'", p.OwnershipPercentage2));

                    sb.Append(String.Format(", Note='{0}'", DBString(p.Note)));
                    sb.AppendLine(String.Format(" WHERE Id={0};", p.Id));
                }
                else if (p.IsInserted)
                {
                    sb.AppendLine("-- inserting RentalBuildings : " + p.Name);
                    sb.Append(@"INSERT INTO RentBuildings (
                                Id,
                                Name,
                                Address,
                                PurchasedDate,
                                PurchasedPrice,
                                LandValue,
                                EstimatedValue,
                                CategoryForIncome,
                                CategoryForTaxes,
                                CategoryForInterest,
                                CategoryForRepairs,
                                CategoryForMaintenance,
                                CategoryForManagement,
                                OwnershipName1,
                                OwnershipName2,
                                OwnershipPercentage1,
                                OwnershipPercentage2,
                                Note) VALUES ("
                        );
                    sb.Append(String.Format("'{0}'", p.Id.ToString()));
                    sb.Append(String.Format(", '{0}'", DBString(p.Name)));
                    sb.Append(String.Format(", '{0}'", DBString(p.Address)));
                    sb.Append(String.Format(",  {0}", DBDateTime(p.PurchasedDate)));
                    sb.Append(String.Format(", '{0}'", p.PurchasedPrice));
                    sb.Append(String.Format(", '{0}'", p.LandValue));
                    sb.Append(String.Format(", '{0}'", p.EstimatedValue));
                    sb.Append(String.Format(", '{0}'", p.CategoryForIncome));
                    sb.Append(String.Format(", '{0}'", p.CategoryForTaxes));
                    sb.Append(String.Format(", '{0}'", p.CategoryForInterest));
                    sb.Append(String.Format(", '{0}'", p.CategoryForRepairs));
                    sb.Append(String.Format(", '{0}'", p.CategoryForMaintenance));
                    sb.Append(String.Format(", '{0}'", p.CategoryForManagement));
                    sb.Append(String.Format(", '{0}'", DBString(p.OwnershipName1)));
                    sb.Append(String.Format(", '{0}'", DBString(p.OwnershipName2)));
                    sb.Append(String.Format(", '{0}'", p.OwnershipPercentage1));
                    sb.Append(String.Format(", '{0}'", p.OwnershipPercentage2));
                    sb.Append(String.Format(", '{0}'", DBString(p.Note)));
                    sb.AppendLine(");");
                }
                else if (p.IsDeleted)
                {
                    sb.AppendLine("-- deleting RentalBuildings : " + p.Name);
                    sb.AppendLine(string.Format("DELETE FROM RentBuildings WHERE Id={0};", p.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }

            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }
            foreach (RentBuilding p in buildings)
            {
                p.OnUpdated();
            }
            buildings.RemoveDeleted();
        }


        #endregion


        #region TABLE UNITS

        public void ReadRentUnits(RentUnits collection, MyMoney money)
        {
            collection.Clear();
            IDataReader reader = ExecuteReader("SELECT Id,Building,Name,Renter,Note FROM RentUnits");
            collection.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("RentUnits");
                RentUnit r = new RentUnit(collection);
                r.Id = reader.GetInt32(0);
                r.Building = reader.GetInt32(1);
                r.Name = ReadDbString(reader, 2);
                r.Renter = ReadDbString(reader, 3);
                r.Note = ReadDbString(reader, 4);

                collection.AddRentUnit(r);
                r.OnUpdated();
            }
            collection.EndUpdate();
            reader.Close();
        }


        public void UpdateRentUnits(RentUnits units)
        {
            if (units.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (RentUnit x in units)
            {
                if (x.IsChanged)
                {
                    sb.AppendLine("-- updating RentUnits : " + x.Name);
                    sb.Append("UPDATE RentUnits SET ");
                    sb.Append(String.Format("Name='{0}'", DBString(x.Name)));
                    sb.Append(String.Format(", Renter='{0}'", DBString(x.Renter)));
                    sb.Append(String.Format(", Note='{0}'", DBString(x.Note)));
                    sb.AppendLine(String.Format(" WHERE Id={0} AND Building={1};", x.Id, x.Building));
                }
                else if (x.IsInserted)
                {
                    sb.AppendLine("-- inserting RentUnits : " + x.Name);
                    sb.Append("INSERT INTO RentUnits (Id, Building, Name, Renter, Note) VALUES (");
                    sb.Append(String.Format("{0}", x.Id.ToString()));
                    sb.Append(String.Format(", {0}", x.Building.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(x.Name)));
                    sb.Append(String.Format(",'{0}'", DBString(x.Renter)));
                    sb.Append(String.Format(",'{0}'", DBString(x.Note)));
                    sb.AppendLine(");");
                }
                else if (x.IsDeleted)
                {
                    sb.AppendLine("-- deleting RentUnits : " + x.Name);
                    sb.AppendLine(string.Format("DELETE FROM RentUnits WHERE Id={0} AND Building={1};", x.Id.ToString(), x.Building.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }

            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (RentUnit x in units)
            {
                x.OnUpdated();
            }
            units.RemoveDeleted();
        }


        #endregion


        #region TABLE LOANS

        public void ReadLoanPayments(LoanPayments collection, MyMoney money)
        {
            collection.Clear();
            IDataReader reader = ExecuteReader("SELECT Id, AccountId,Date,Principal,Interest,Memo FROM LoanPayments");
            collection.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("LoanPayments");
                LoanPayment x = new LoanPayment(collection);
                x.BatchMode = true;
                x.Id = reader.GetInt32(0);
                x.AccountId = reader.GetInt32(1);
                x.Date = reader.SafeGetDateTime(2);
                x.Principal = reader.GetDecimal(3);
                x.Interest = reader.GetDecimal(4);
                x.Memo = ReadDbString(reader, 5);
                x.BatchMode = false;
                collection.AddLoan(x);
                x.OnUpdated();
            }
            collection.EndUpdate();
            collection.FireChangeEvent(collection, collection, null, ChangeType.Reloaded);
            reader.Close();

            foreach (Account a in money.Accounts)
            {
                if (a.Type == AccountType.Loan)
                {
                    // This will have the side effect of updating the Account.Balance to the latest value
                    a.BatchMode = true;
                    LoanPaymentAggregation.GetLoanPayementsAggregation(money, a);
                    a.BatchMode = false;
                }
            }
        }

        public void UpdateLoanPayments(LoanPayments loans)
        {
            if (loans.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (LoanPayment i in loans)
            {
                if (i.IsChanged)
                {
                    sb.AppendLine("-- updating LoanPayments : " + i.Id);
                    sb.Append("UPDATE LoanPayments SET ");
                    sb.Append(String.Format("Date={0}", DBDateTime(i.Date)));
                    sb.Append(String.Format(",AccountId={0}", i.AccountId.ToString()));
                    sb.Append(String.Format(",Principal='{0}'", i.Principal.ToString()));
                    sb.Append(String.Format(",Interest='{0}'", i.Interest.ToString()));
                    sb.Append(String.Format(",Memo='{0}'", i.Memo));
                    sb.AppendLine(String.Format(" WHERE Id={0};", i.Id));
                }
                else if (i.IsInserted)
                {
                    sb.AppendLine("-- inserting LoanPayments : " + i.Id);
                    sb.Append("INSERT INTO LoanPayments (Id,AccountId,Date,Principal,Interest,Memo) VALUES (");
                    sb.Append(String.Format("{0}", i.Id.ToString()));
                    sb.Append(String.Format(",{0}", i.AccountId.ToString()));
                    sb.Append(String.Format(",{0}", DBDateTime(i.Date)));
                    sb.Append(String.Format(",{0}", i.Principal));
                    sb.Append(String.Format(",{0}", i.Interest));
                    sb.Append(String.Format(",'{0}'", DBString(i.Memo)));
                    sb.AppendLine(");");
                }
                else if (i.IsDeleted)
                {
                    sb.AppendLine("-- deleting LoanPayments : " + i.Id);
                    sb.AppendLine(string.Format("DELETE FROM LoanPayments WHERE Id={0};", i.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (LoanPayment i in loans)
            {
                i.OnUpdated();
            }

            loans.RemoveDeleted();
        }

        #endregion


        #region TABLE CATEGORIES

        public void ReadCategories(Categories categories, MyMoney money)
        {
            categories.Clear();
            IDataReader reader = ExecuteReader("SELECT [Id],[Name],[Description],[Type],[ParentId],[Budget],[Frequency],[Balance],[Color],[TaxRefNum] FROM Categories");
            categories.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("Categories");
                int id = reader.GetInt32(0);
                Category c = new Category(categories);
                c.Id = id;
                categories.AddCategory(c);
                c.Name = ReadDbString(reader, 1);
                c.Description = ReadDbString(reader, 2);
                if (!reader.IsDBNull(3))
                {
                    c.Type = (CategoryType)reader.GetInt32(3);
                }
                if (!reader.IsDBNull(4))
                {
                    c.ParentId = reader.GetInt32(4);
                }
                if (!reader.IsDBNull(5))
                {
                    c.Budget = reader.GetDecimal(5);
                }
                if (!reader.IsDBNull(6))
                {
                    c.BudgetRange = (CalendarRange)reader.GetInt32(6);
                }
                if (!reader.IsDBNull(7))
                {
                    c.Balance = reader.GetDecimal(7);
                }
                if (!reader.IsDBNull(8))
                {
                    c.Color = reader.GetString(8);
                }
                if (!reader.IsDBNull(9))
                {
                    c.TaxRefNum = reader.GetInt32(9);
                }
                c.OnUpdated();
                // one more fix up that will need to be saved (so must come after c.OnUpdated).
                if (c.Type == CategoryType.Reserved)
                {
                    c.Type = CategoryType.Expense;
                }
            }
            categories.EndUpdate();
            categories.FireChangeEvent(categories, categories, null, ChangeType.Reloaded);
            reader.Close();
        }

        public void UpdateCategories(Categories categories)
        {
            if (categories.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Category c in categories)
            {
                if (c.IsChanged)
                {
                    sb.AppendLine("-- udpating Categories : " + c.Name);
                    sb.Append("UPDATE Categories SET ");
                    sb.Append(String.Format("Name='{0}'", DBString(c.Name)));
                    sb.Append(String.Format(",Description='{0}'", DBString(c.Description)));
                    sb.Append(String.Format(",Type={0}", (int)c.Type));
                    sb.Append(String.Format(",ParentId={0}", c.ParentCategory != null ? c.ParentCategory.Id : -1));
                    sb.Append(String.Format(",Budget={0}", c.Budget));
                    sb.Append(String.Format(",Frequency={0}", (int)c.BudgetRange));
                    sb.Append(String.Format(",Balance={0}", c.Balance));
                    sb.Append(String.Format(",Color='{0}'", c.Color));
                    sb.Append(String.Format(",TaxRefNum='{0}'", c.TaxRefNum));

                    sb.AppendLine(String.Format(" WHERE Id={0};", c.Id));
                }
                else if (c.IsInserted)
                {
                    sb.AppendLine("-- inserting Categories : " + c.Name);
                    sb.Append("INSERT INTO Categories (Id,Name,Description,Type,ParentId,Budget,Frequency,Balance,Color,TaxRefNum) VALUES (");
                    sb.Append(String.Format("{0}", c.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(c.Name)));
                    sb.Append(String.Format(",'{0}'", DBString(c.Description)));
                    sb.Append(String.Format(",{0}", (int)c.Type));
                    sb.Append(String.Format(",{0}", c.ParentCategory != null ? c.ParentCategory.Id : -1));
                    sb.Append(String.Format(",{0}", c.Budget.ToString()));
                    sb.Append(String.Format(",{0}", (int)c.BudgetRange));
                    sb.Append(String.Format(",{0}", c.Balance));
                    sb.Append(String.Format(",'{0}'", c.Color));
                    sb.Append(String.Format(",'{0}'", c.TaxRefNum));
                    sb.AppendLine(");");
                }
                else if (c.IsDeleted)
                {
                    sb.AppendLine("-- deleting Categories : " + c.Name);
                    sb.AppendLine(string.Format("DELETE FROM Categories WHERE Id={0};", c.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (Category c in categories)
            {
                c.OnUpdated();
            }
            categories.RemoveDeleted();
        }

        #endregion

        #region CURRENCIES

        public void ReadCurrencies(Currencies currencies, MyMoney money)
        {
            currencies.Clear();
            IDataReader reader = ExecuteReader("SELECT Id,Symbol,Name,Ratio,LastRatio FROM Currencies");
            currencies.BeginUpdate(false);

            while (reader.Read())
            {
                IncrementProgress("Currencies");
                int id = reader.GetInt32(0);
                Currency s = currencies.AddCurrency(id);
                s.Symbol = ReadDbString(reader, 1);
                s.Name = ReadDbString(reader, 2);
                if (!reader.IsDBNull(3)) s.Ratio = reader.GetDecimal(3);
                if (!reader.IsDBNull(4)) s.LastRatio = reader.GetDecimal(4);
                s.OnUpdated();
            }
            currencies.EndUpdate();
            currencies.FireChangeEvent(currencies, currencies, null, ChangeType.Reloaded);
            reader.Close();
        }


        public void UpdateCurrencies(Currencies currencies)
        {
            if (currencies.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Currency s in currencies)
            {
                if (s.IsChanged)
                {
                    sb.AppendLine("-- inserting Currencies : " + s.Name);
                    sb.Append("UPDATE Currencies SET ");
                    sb.Append(String.Format("Symbol='{0}'", DBString(s.Symbol)));
                    sb.Append(String.Format(",Name='{0}'", DBString(s.Name)));
                    sb.Append(String.Format(",Ratio={0}", s.Ratio));
                    sb.Append(String.Format(",LastRatio={0}", s.LastRatio));
                    sb.AppendLine(String.Format(" WHERE Id={0};", s.Id));
                }
                else if (s.IsInserted)
                {
                    sb.AppendLine("-- updating Currencies : " + s.Name);
                    sb.Append("INSERT INTO Currencies VALUES (");
                    sb.Append(String.Format("{0}", s.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(s.Name)));
                    sb.Append(String.Format(",'{0}'", DBString(s.Symbol)));
                    sb.Append(String.Format(",{0}", s.Ratio));
                    sb.Append(String.Format(",{0}", s.LastRatio));
                    sb.AppendLine(");");
                }
                else if (s.IsDeleted)
                {
                    sb.AppendLine("-- deleting Currencies : " + s.Name);
                    sb.AppendLine(string.Format("DELETE FROM Currencies WHERE Id={0};", s.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate && sb.Length > 0)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (Currency s in currencies)
            {
                s.OnUpdated();
            }

            currencies.RemoveDeleted();
        }

        #endregion


        #region TABLE SECURITIES

        public void ReadSecurities(Securities securities, MyMoney money)
        {
            securities.Clear();
            IDataReader reader = ExecuteReader("SELECT Id,Name,Symbol,Price,LastPrice,CuspId,SecurityType,Taxable,PriceDate FROM Securities");
            securities.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("Securities");
                int id = reader.GetInt32(0);
                Security s = securities.AddSecurity(id);
                s.Name = ReadDbString(reader, 1);
                s.Symbol = ReadDbString(reader, 2);
                s.Price = reader.GetDecimal(3);
                if (!reader.IsDBNull(4)) s.LastPrice = reader.GetDecimal(4);
                s.CuspId = ReadDbString(reader, 5);
                if (!reader.IsDBNull(6)) s.SecurityType = (SecurityType)reader.GetInt32(6);
                if (!reader.IsDBNull(7)) s.Taxable = (YesNo)reader.GetByte(7);
                if (!reader.IsDBNull(8)) s.PriceDate = reader.SafeGetDateTime(8);
                s.OnUpdated();
            }
            securities.EndUpdate();
            securities.FireChangeEvent(securities, securities, null, ChangeType.Reloaded);
            reader.Close();
        }

        public void UpdateSecurities(Securities securities)
        {
            if (securities.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Security s in securities)
            {
                if (s.IsChanged)
                {
                    sb.AppendLine("-- updating Securities : " + s.Name);
                    sb.Append("UPDATE Securities SET ");
                    sb.Append(String.Format("Name='{0}'", DBString(s.Name)));
                    sb.Append(String.Format(",Symbol='{0}'", DBString(s.Symbol)));
                    sb.Append(String.Format(",Price={0}", s.Price));
                    sb.Append(String.Format(",LastPrice={0}", s.LastPrice));
                    sb.Append(String.Format(",CuspId='{0}'", s.CuspId));
                    sb.Append(String.Format(",SecurityType={0}", (int)s.SecurityType));
                    sb.Append(String.Format(",Taxable={0}", (byte)s.Taxable));
                    sb.Append(String.Format(",PriceDate={0}", DBDateTime(s.PriceDate)));
                    sb.AppendLine(String.Format(" WHERE Id={0};", s.Id));
                }
                else if (s.IsInserted)
                {
                    sb.AppendLine("-- inserting Securities : " + s.Name);
                    sb.Append("INSERT INTO Securities VALUES (");
                    sb.Append(String.Format("{0}", s.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(s.Name)));
                    sb.Append(String.Format(",'{0}'", DBString(s.Symbol)));
                    sb.Append(String.Format(",{0}", s.Price));
                    sb.Append(String.Format(",{0}", s.LastPrice));
                    sb.Append(String.Format(",'{0}'", s.CuspId));
                    sb.Append(String.Format(",{0}", (int)s.SecurityType));
                    sb.Append(String.Format(",{0}", (byte)s.Taxable));
                    sb.Append(String.Format(",{0}", DBDateTime(s.PriceDate)));
                    sb.AppendLine(");");
                }
                else if (s.IsDeleted)
                {
                    sb.AppendLine("-- deleting Securities : " + s.Name);
                    sb.AppendLine(string.Format("DELETE FROM Securities WHERE Id={0};", s.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (Security s in securities)
            {
                s.OnUpdated();
            }

            securities.RemoveDeleted();
        }

        #endregion


        #region TABLE STOCK SPLITS
        void ReadStockSplits(StockSplits splits, MyMoney money)
        {
            splits.Clear();
            IDataReader reader = ExecuteReader("SELECT Id,Date,Security,Numerator,Denominator FROM StockSplits");
            splits.BeginUpdate(false);
            while (reader.Read())
            {
                IncrementProgress("StockSplits");

                int sid = reader.GetInt32(2);
                long id = reader.GetInt64(0);
                StockSplit s = splits.AddStockSplit(id);
                s.Date = reader.SafeGetDateTime(1);
                s.Security = money.Securities.FindSecurityAt(sid);
                s.Numerator = reader.GetDecimal(3);
                s.Denominator = reader.GetDecimal(4);
                s.OnUpdated();
            }
            splits.EndUpdate();
            splits.FireChangeEvent(splits, splits, null, ChangeType.Reloaded);
            reader.Close();
        }


        public void UpdateStockSplits(StockSplits stockSplits)
        {
            if (stockSplits.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (StockSplit s in stockSplits)
            {
                if (s.IsChanged && s.Date != DateTime.MinValue)
                {
                    if (s.Security != null)
                    {
                        sb.AppendLine("-- updating StockSplits for : " + s.Security.Name);
                        sb.Append("UPDATE StockSplits SET ");
                        sb.Append(String.Format("Date={0}", DBDateTime(s.Date)));
                        sb.Append(String.Format(",Security={0}", s.Security.Id));
                        sb.Append(String.Format(",Numerator={0}", s.Numerator));
                        sb.Append(String.Format(",Denominator={0}", s.Denominator));
                        sb.AppendLine(String.Format(" WHERE Id={0};", s.Id));
                    }
                }
                else if (s.IsInserted && s.Date != DateTime.MinValue)
                {
                    if (s.Security != null)
                    {
                        sb.AppendLine("-- inserting StockSplits for : " + s.Security.Name);
                        sb.Append("INSERT INTO StockSplits VALUES (");
                        sb.Append(String.Format("{0}", s.Id.ToString()));
                        sb.Append(String.Format(",{0}", DBDateTime(s.Date)));
                        sb.Append(String.Format(",{0}", s.Security.Id));
                        sb.Append(String.Format(",{0}", s.Numerator));
                        sb.Append(String.Format(",{0}", s.Denominator));
                        sb.AppendLine(");");
                    }
                }
                else if (s.IsDeleted)
                {
                    sb.AppendLine("-- deleting StockSplits for : " + s.Security.Name);
                    sb.AppendLine(string.Format("DELETE FROM StockSplits WHERE Id={0};", s.Id.ToString()));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (StockSplit s in stockSplits)
            {
                s.OnUpdated();
            }

            stockSplits.RemoveDeleted();
        }
        #endregion


        #region TABLE CATEGORIES
        // Returns list of errors found in database.
        public ArrayList ReadTransactions(Transactions transactions, MyMoney money)
        {
            transactions.Clear();

            ArrayList errors = new ArrayList();

            IDataReader reader = ExecuteReader("SELECT Id,Number,Date,Amount,Account,Status,Memo,Payee,Category,FITID,SalesTax,Flags,ReconciledDate,BudgetBalanceDate,MergeDate,OriginalPayee FROM Transactions");
            transactions.BeginUpdate(false);

            while (reader.Read())
            {
                IncrementProgress("Transactions");
                long id = reader.GetInt64(0);
                Transaction t = transactions.AddTransaction(id);

                t.BatchMode = true;

                t.Number = ReadDbString(reader, 1);
                t.Date = reader.SafeGetDateTime(2);
                t.Amount = reader.GetDecimal(3);
                t.Account = money.Accounts.FindAccountAt(reader.GetInt32(4));
                t.Status = (TransactionStatus)reader.GetInt32(5);
                t.Memo = ReadDbString(reader, 6);
                t.Payee = money.Payees.FindPayeeAt(reader.GetInt32(7));
                t.Category = money.Categories.FindCategoryById(reader.GetInt32(8));
                t.FITID = ReadDbString(reader, 9);
                if (!reader.IsDBNull(10)) t.SalesTax = reader.GetDecimal(10);
                if (!reader.IsDBNull(11)) t.Flags = (TransactionFlags)reader.GetInt32(11);
                if (!reader.IsDBNull(12)) t.ReconciledDate = reader.SafeGetDateTime(12);
                if (!reader.IsDBNull(13)) t.BudgetBalanceDate = reader.SafeGetDateTime(13);
                if (!reader.IsDBNull(14)) t.MergeDate = reader.SafeGetDateTime(14);
                if (!reader.IsDBNull(15)) t.OriginalPayee = reader.GetString(15);

                t.BatchMode = false;


                t.OnUpdated();
            }

            reader.Close();
            // Load the splits.
            reader = ExecuteReader("SELECT [Id],[Transaction],[Amount],[Category],[Memo],[Transfer],[Payee],[Flags],[BudgetBalanceDate] FROM Splits ORDER BY [Transaction],[Id]");
            while (reader.Read())
            {
                IncrementProgress("Splits");
                int id = reader.GetInt32(0);
                long transactionid = reader.GetInt64(1);
                Transaction t = transactions.FindTransactionById(transactionid);
                Split s = null;
                if (t == null)
                {
                    // Yikes! -- this just needs to be deleted then.
                    Trace.WriteLine("Dangling split: " + id + "," + transactionid);
                    continue;
                }
                else
                {
                    if (!t.IsSplit)
                    {
                        t.Splits = new Splits(t, t);
                    }
                    s = t.Splits.AddSplit(id);
                }
                s.BatchMode = true;
                t.Splits.BeginUpdate(false);
                s.Amount = reader.GetDecimal(2);
                s.Category = money.Categories.FindCategoryById(reader.GetInt32(3));
                s.Memo = ReadDbString(reader, 4);
                long tid = reader.GetInt64(5);
                if (tid != -1)
                {
                    Transaction u = transactions.FindTransactionById(tid);
                    if (u == null)
                    {
                        errors.Add(new DataError(transactionid, id, "Other side of split transfer not found"));
                    }
                    else
                    {
                        if (u.Transfer != null && (u.Transfer.Transaction != t || u.Transfer.Split != s))
                        {
                            errors.Add(new DataError(transactionid, id, "Duplicate transfer found"));
                        }
                        s.Transfer = new Transfer(tid, t, s, u);
                    }
                }
                if (!reader.IsDBNull(6))
                {
                    int pid = reader.GetInt32(6);
                    s.Payee = money.Payees.FindPayeeAt(pid);
                }


                if (!reader.IsDBNull(7)) s.Flags = (SplitFlags)reader.GetInt32(7);
                if (!reader.IsDBNull(8)) s.BudgetBalanceDate = reader.SafeGetDateTime(8);

                t.Splits.EndUpdate();

                s.BatchMode = false;
                s.OnUpdated();
                if (t != null) t.OnUpdated();
            }

            reader.Close();
            // now we can resolve the transfers
            reader = ExecuteReader("SELECT Id,Transfer,TransferSplit FROM Transactions WHERE NOT Transfer = -1 ");
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                Transaction t = transactions.FindTransactionById(id);
                Debug.Assert(t != null); // since we just loaded it above.
                long tid = reader.GetInt64(1);
                Transaction u = transactions.FindTransactionById(tid);
                if (u == null)
                {
                    errors.Add(new DataError(id, "Transaction is marked as a transfer, but other side of transfer was not found"));
                }
                if (t != null && u != null)
                {
                    int sid = reader.GetInt32(2);
                    if (sid == -1)
                    {
                        if (u.Transfer != null)
                        {
                            if (u.Transfer.Transaction != t)
                            {
                                // already have a transfer for this transaction!
                                errors.Add(new DataError(id, string.Format("Already have a transfer for this transaction, so transfer {0} is a duplicate of transfer {1}", id, u.Transfer.Id)));
                            }
                        }
                        t.Transfer = new Transfer(id, t, u);
                    }
                    else
                    {
                        Split s = u.FindSplit(sid);
                        if (s == null)
                        {
                            errors.Add(new DataError(id, sid, "Transaction contains a split marked as a transfer, but other side of transfer was not found"));
                        }
                        else
                        {
                            if (t.Transfer != null)
                            {
                                // already have a transfer for this split!
                                errors.Add(new DataError(id, string.Format("Already have a transfer for this split, so {0} is a duplicate of {1}", id, t.Transfer.Id)));
                            }
                            t.Transfer = new Transfer(id, t, u, s);
                        }
                    }
                    t.OnUpdated();
                }
            }

            reader.Close();

            ReadInvestments(transactions, money);

            // recompute state of Payee objects 
            foreach (Transaction t in transactions)
            {
                t.BatchMode = true;

                Payee p = t.Payee;
                if (p != null)
                {

                    // setup initial counts
                    if (t.Category == null && t.Transfer == null && !t.IsSplit)
                    {
                        p.UncategorizedTransactions++;
                        p.OnUpdated();
                    }
                    if ((t.Flags & TransactionFlags.Unaccepted) != 0)
                    {
                        p.UnacceptedTransactions++;
                        p.OnUpdated();
                    }
                }

                t.BatchMode = false;
            }

            transactions.EndUpdate();

            transactions.FireChangeEvent(transactions, transactions, null, ChangeType.Reloaded);
            return errors;
        }

        public void UpdateTransactions(Transactions transactions)
        {
            if (transactions.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            foreach (Transaction t in transactions)
            {
                if (t.IsChanged)
                {
                    sb.AppendLine("-- updating Transaction : " + t.Number);
                    sb.Append("UPDATE Transactions SET ");
                    sb.Append(String.Format("Number='{0}'", DBString(t.Number)));
                    sb.Append(String.Format(",Account={0}", t.Account.Id));
                    sb.Append(String.Format(",Date={0}", DBDateTime(t.Date)));
                    sb.Append(String.Format(",Amount={0}", DBDecimal(t.Amount)));
                    sb.Append(String.Format(",Status={0}", ((int)t.Status).ToString()));
                    sb.Append(String.Format(",Memo='{0}'", DBString(t.Memo)));
                    sb.Append(String.Format(",Payee={0}", t.Payee != null ? t.Payee.Id.ToString() : "-1"));
                    sb.Append(String.Format(",Category={0}", t.Category != null ? t.Category.Id.ToString() : "-1"));
                    sb.Append(String.Format(",Transfer={0}", t.Transfer != null && t.Transfer.Transaction != null ? t.Transfer.Transaction.Id.ToString() : "-1"));
                    sb.Append(String.Format(",TransferSplit={0}", t.Transfer != null && t.Transfer.Split != null ? t.Transfer.Split.Id.ToString() : "-1"));
                    sb.Append(String.Format(",FITID='{0}'", DBString(t.FITID)));
                    sb.Append(String.Format(",SalesTax={0}", DBDecimal(t.SalesTax)));
                    sb.Append(String.Format(",Flags={0}", (int)t.Flags));
                    sb.Append(String.Format(",ReconciledDate={0}", DBNullableDateTime(t.ReconciledDate)));
                    sb.Append(String.Format(",BudgetBalanceDate={0}", DBNullableDateTime(t.BudgetBalanceDate)));
                    sb.Append(String.Format(",MergeDate={0}", DBNullableDateTime(t.MergeDate)));
                    sb.Append(String.Format(",OriginalPayee='{0}'", DBString(t.OriginalPayee)));
                    sb.AppendLine(String.Format(" WHERE Id={0};", t.Id));
                }
                else if (t.IsInserted)
                {
                    sb.AppendLine("-- inserting Transaction : " + t.Number);
                    sb.Append("INSERT INTO Transactions ([Id],[Number],[Account],[Date],[Amount],[Status],[Memo],[Payee],[Category],[Transfer],[TransferSplit],[FITID],[SalesTax],[Flags],[ReconciledDate],[BudgetBalanceDate],[MergeDate],[OriginalPayee]) VALUES (");
                    sb.Append(String.Format("{0}", t.Id.ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(t.Number)));
                    sb.Append(String.Format(",{0}", t.Account.Id));
                    sb.Append(String.Format(",{0}", DBDateTime(t.Date)));
                    sb.Append(String.Format(",{0}", DBDecimal(t.Amount)));
                    sb.Append(String.Format(",{0}", ((int)t.Status).ToString()));
                    sb.Append(String.Format(",'{0}'", DBString(t.Memo)));
                    sb.Append(String.Format(",{0}", t.Payee != null ? t.Payee.Id.ToString() : "-1"));
                    sb.Append(String.Format(",{0}", t.Category != null ? t.Category.Id.ToString() : "-1"));
                    sb.Append(String.Format(",{0}", t.Transfer != null && t.Transfer.Transaction != null ? t.Transfer.Transaction.Id.ToString() : "-1"));
                    sb.Append(String.Format(",{0}", t.Transfer != null && t.Transfer.Split != null ? t.Transfer.Split.Id.ToString() : "-1"));
                    sb.Append(String.Format(",'{0}'", DBString(t.FITID)));
                    sb.Append(String.Format(",{0}", DBDecimal(t.SalesTax)));
                    sb.Append(String.Format(",{0}", (int)t.Flags));
                    sb.Append(String.Format(",{0}", DBNullableDateTime(t.ReconciledDate)));
                    sb.Append(String.Format(",{0}", DBNullableDateTime(t.BudgetBalanceDate)));
                    sb.Append(String.Format(",{0}", DBNullableDateTime(t.MergeDate)));
                    sb.Append(String.Format(",'{0}'", DBString(t.OriginalPayee)));
                    sb.AppendLine(");");
                }
                else if (t.IsDeleted)
                {
                    sb.AppendLine("-- deleting Transaction : " + t.Number);
                    sb.AppendLine(string.Format("DELETE FROM Transactions WHERE Id={0};", t.Id.ToString()));
                }

                if (t.Splits != null)
                {
                    UpdateSplits(t.Splits);
                }

                if (t.Investment != null)
                {
                    UpdateInvestment(t.Investment);
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (Transaction t in transactions)
            {
                t.OnUpdated();
            }
            transactions.RemoveDeleted();
        }

        public void UpdateSplits(Splits splits)
        {
            StringBuilder sb = new StringBuilder();

            foreach (Split s in splits)
            {
                if (s.IsChanged)
                {
                    sb.AppendLine("-- updating Split : " + s.Id);
                    sb.Append("UPDATE [Splits] SET ");
                    sb.Append(String.Format("[Amount]={0}", s.Amount));
                    sb.Append(String.Format(",[Category]={0}", s.Category != null ? s.Category.Id.ToString() : "-1"));
                    sb.Append(String.Format(",[Memo]='{0}'", DBString(s.Memo)));
                    sb.Append(String.Format(",[Transfer]={0}", s.Transfer != null && s.Transfer.Transaction != null ? s.Transfer.Transaction.Id.ToString() : "-1"));
                    sb.Append(String.Format(",[Payee]={0}", s.Payee != null ? s.Payee.Id.ToString() : "-1"));
                    sb.Append(String.Format(",Flags={0}", (int)s.Flags));
                    sb.Append(String.Format(",BudgetBalanceDate={0}", DBNullableDateTime(s.BudgetBalanceDate)));
                    sb.AppendLine(String.Format(" WHERE [Id]={0} AND [Transaction]={1};", s.Id, s.Transaction.Id));
                }
                else if (s.IsInserted)
                {
                    sb.AppendLine("-- inserting Split : " + s.Id);
                    sb.Append("INSERT INTO [Splits] ([Id],[Transaction],[Amount],[Category],[Memo],[Transfer],[Payee],[Flags],[BudgetBalanceDate]) VALUES (");
                    sb.Append(String.Format("{0}", s.Id));
                    sb.Append(String.Format(",{0}", s.Transaction.Id.ToString()));
                    sb.Append(String.Format(",{0}", s.Amount));
                    sb.Append(String.Format(",{0}", s.Category != null ? s.Category.Id.ToString() : "-1"));
                    sb.Append(String.Format(",'{0}'", DBString(s.Memo)));
                    sb.Append(String.Format(",{0}", s.Transfer != null && s.Transfer.Transaction != null ? s.Transfer.Transaction.Id.ToString() : "-1"));
                    sb.Append(String.Format(",{0}", s.Payee != null ? s.Payee.Id.ToString() : "-1"));
                    sb.Append(String.Format(",{0}", (int)s.Flags));
                    sb.Append(String.Format(",{0}", DBNullableDateTime(s.BudgetBalanceDate)));
                    sb.AppendLine(");");
                }
                else if (s.IsDeleted)
                {
                    sb.AppendLine("-- deleting Split : " + s.Id);
                    sb.AppendLine(string.Format("DELETE FROM [Splits] WHERE [Id]={0} AND [Transaction]={1};", s.Id, s.Transaction.Id));
                }

                if (!this.SupportsBatchUpdate)
                {
                    ExecuteScalar(sb.ToString());
                    sb.Length = 0;
                }
            }
            if (this.SupportsBatchUpdate)
            {
                ExecuteScalar(sb.ToString());
            }

            foreach (Split s in splits)
            {
                s.OnUpdated();
            }
            splits.RemoveDeleted();
        }

        #endregion


        #region TABLE INVESTMENTS

        private IDataReader ReadInvestments(Transactions transactions, MyMoney money)
        {
            // Load investment transaction details.
            IDataReader reader = ExecuteReader("SELECT Id,Security,UnitPrice,Units,Commission,InvestmentType,TradeType,TaxExempt,Withholding,MarkUpDown,Taxes,Fees,[Load]  FROM Investments");
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                Transaction t = transactions.FindTransactionById(id);
                if (t == null)
                {
                    Debug.Assert(false, "Investment " + id + " is orphaned!");
                }
                else
                {
                    Investment i = t.GetOrCreateInvestment();
                    Debug.Assert(i != null); // should be associated with investment account.
                    i.Security = money.Securities.FindSecurityAt(reader.GetInt32(1));
                    i.UnitPrice = reader.GetDecimal(2);
                    i.Units = reader.GetDecimal(3);
                    i.Commission = reader.GetDecimal(4);
                    i.Type = (InvestmentType)reader.GetInt32(5);
                    if (!reader.IsDBNull(6)) i.TradeType = (InvestmentTradeType)reader.GetInt32(6);
                    if (!reader.IsDBNull(7)) i.TaxExempt = reader.GetBoolean(7);
                    if (!reader.IsDBNull(8)) i.Withholding = reader.GetDecimal(8);
                    if (!reader.IsDBNull(9)) i.MarkUpDown = reader.GetDecimal(9);
                    if (!reader.IsDBNull(10)) i.Taxes = reader.GetDecimal(10);
                    if (!reader.IsDBNull(11)) i.Fees = reader.GetDecimal(11);
                    if (!reader.IsDBNull(12)) i.Load = reader.GetDecimal(12);

                    i.OnUpdated();
                    t.OnUpdated();
                }
            }
            reader.Close();
            return reader;
        }

        public void UpdateInvestment(Investment i)
        {
            if (i == null) return;
            StringBuilder sb = new StringBuilder();
            if (i.IsChanged)
            {
                sb.AppendLine("-- updating Investment : " + i.Id);
                sb.Append("UPDATE Investments SET ");
                sb.Append(String.Format("Security={0}", i.Security == null ? -1 : i.Security.Id));
                sb.Append(String.Format(",UnitPrice={0}", DBDecimal(i.UnitPrice)));
                sb.Append(String.Format(",Units={0}", DBDecimal(i.Units)));
                sb.Append(String.Format(",Commission={0}", DBDecimal(i.Commission)));
                sb.Append(String.Format(",InvestmentType='{0}'", (int)i.Type));
                sb.Append(String.Format(",TradeType='{0}'", (int)i.TradeType));
                sb.Append(String.Format(",TaxExempt='{0}'", i.TaxExempt ? 1 : 0));
                sb.Append(String.Format(",Withholding={0}", DBDecimal(i.Withholding)));
                sb.Append(String.Format(",MarkUpDown={0}", DBDecimal(i.MarkUpDown))); ;
                sb.Append(String.Format(",Taxes={0}", DBDecimal(i.Taxes))); ;
                sb.Append(String.Format(",Fees={0}", DBDecimal(i.Fees))); ;
                sb.Append(String.Format(",[Load]={0}", DBDecimal(i.Load)));

                sb.AppendLine(String.Format(" WHERE Id={0};", i.Id));
            }
            else if (i.IsInserted)
            {
                sb.AppendLine("-- inserting Investment : " + i.Id);
                sb.Append("INSERT INTO Investments (Id, Security, UnitPrice, Units, Commission, InvestmentType, TradeType, TaxExempt, Withholding, MarkUpDown, Taxes, Fees, [Load]) VALUES (");
                sb.Append(String.Format("'{0}'", i.Id.ToString()));
                sb.Append(String.Format(",'{0}'", i.Security == null ? -1 : i.Security.Id));
                sb.Append(String.Format(",{0}", DBDecimal(i.UnitPrice)));
                sb.Append(String.Format(",{0}", DBDecimal(i.Units)));
                sb.Append(String.Format(",{0}", DBDecimal(i.Commission)));
                sb.Append(String.Format(",'{0}'", (int)i.Type));

                sb.Append(String.Format(",'{0}'", (int)i.TradeType));
                sb.Append(String.Format(",'{0}'", i.TaxExempt ? 1 : 0));
                sb.Append(String.Format(",{0}", DBDecimal(i.Withholding)));
                sb.Append(String.Format(",{0}", DBDecimal(i.MarkUpDown)));
                sb.Append(String.Format(",{0}", DBDecimal(i.Taxes)));
                sb.Append(String.Format(",{0}", DBDecimal(i.Fees)));
                sb.Append(String.Format(",{0}", DBDecimal(i.Load)));

                sb.AppendLine(");");
            }
            else if (i.IsDeleted)
            {
                sb.AppendLine("-- deleting Investment : " + i.Id);
                sb.AppendLine(string.Format("DELETE FROM Investments WHERE Id={0};", i.Id.ToString()));
            }

            ExecuteScalar(sb.ToString());
            i.OnUpdated();
        }

        #endregion

        public virtual void Backup(string backupPath)
        {
            this.backupPath = backupPath;

            //CheckBackupJob(backupPath);
            //ExecuteNonQuery("EXEC sp_start_job @job_name = 'myBackupJob'");

            if (string.IsNullOrEmpty(this.DatabaseName))
            {
                throw new ApplicationException("You have not specified a database name yet");
            }
            ExecuteNonQuery("alter database [" + this.DatabaseName + "] set recovery simple");
            ExecuteNonQuery("checkpoint");
            ExecuteNonQuery("alter database [" + this.DatabaseName + "] set recovery full");
            ExecuteNonQuery("backup database [" + this.DatabaseName + "] to disk = '" + backupPath + "' with init");
        }

        public static SqlServerDatabase Restore(string server, string databasePath, string userId, string password, string backupPath)
        {
            SqlServerDatabase database = new SqlServerDatabase()
            {
                DatabasePath = databasePath,
                Server = server,
                UserId = userId,
                Password = password,
                BackupPath = backupPath,
                SecurityService = new SecurityService()
            };
            database.Restore();
            return database;
        }

        public void Restore()
        {
            string cstr = GetConnectionString(false);
            SqlConnection con = new SqlConnection(cstr);
            using (con)
            {
                con.Open();
                try
                {
                    string dir = Path.GetDirectoryName(this.DatabasePath);
                    AclDatabasePath(dir);
                    string cmd = "RESTORE DATABASE [" + DatabaseName + "] FROM DISK = '" + backupPath + "' WITH REPLACE";
                    // Get the logical to physical file mapping.
                    using (SqlCommand command = new SqlCommand("RESTORE FILELISTONLY FROM DISK='" + backupPath + "'", con))
                    {
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            string logical = reader.GetString(0);
                            string physical = reader.GetString(1);
                            string ext = Path.GetExtension(physical);
                            physical = Path.Combine(Path.GetDirectoryName(this.DatabasePath), Path.GetFileNameWithoutExtension(this.DatabasePath) + ext);
                            cmd += ", MOVE '" + logical + "' TO '" + physical + "'";
                        }
                        reader.Close();

                        // Now construct new mapping so we don't clobber any existing database files.
                        using (var command2 = new SqlCommand(cmd, con))
                        {
                            command2.CommandTimeout = 60;
                            command2.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    throw; // useful for setting breakpoints.
                }
            }
        }


        //        void CheckBackupJob(string backupPath){
        //
        //            if (!backupPath.EndsWith("\\")) backupPath += "\\";
        //
        //            bool createBackup = false;
        //            bool hasBackup = false;
        //            SqlDataReader hr = null;
        //            try {
        //                SqlCommand hasBackupJob = new SqlCommand("EXEC sp_help_job NULL,'myBackupJob','STEPS'", this.Connection);
        //                hr = hasBackupJob.ExecuteReader();
        //                if (hr.Read()){
        //                    hasBackup = true;
        //                    string cmd = hr.GetString(hr.GetOrdinal("Command"));
        //                    string expected = "BACKUP DATABASE msdb TO DISK = '"+backupPath+@"'";
        //                    if (cmd.ToLower() != expected.ToLower()) {
        //                        // backup location has moved.
        //                        createBackup = true;
        //                    }
        //                }
        //                
        //            } catch (SqlException) {
        //                createBackup = true;
        //            } finally {
        //                if (hr != null) hr.Close();
        //            }
        //
        //            if (!createBackup){
        //                BackupResults result = GetBackupStatus();
        //                if (result.ExecutionStatus != ExecutionStatus.None) {
        //                    createBackup = true; 
        //                }
        //            }
        //            if (createBackup) {
        //                if (hasBackup) {
        //                    RemoveBackupJob();
        //                }
        //                SqlCommand cmd = new SqlCommand(CreateBackupJob(backupPath), this.Connection);
        //                cmd.ExecuteNonQuery();
        //            }
        //        }
        //
        //        void RemoveBackupJob(){
        //            SqlCommand removeBackupJob = new SqlCommand("EXEC sp_delete_job NULL,'myBackupJob'", this.Connection);
        //            removeBackupJob.ExecuteNonQuery();            
        //        }
        //        public BackupResults GetBackupStatus() {
        //            BackupResults results = new BackupResults();
        //            SqlDataReader hr = null;
        //            try {
        //                SqlCommand cmd = new SqlCommand("EXEC sp_help_job NULL,'myBackupJob','JOB'", this.Connection);
        //                hr = cmd.ExecuteReader();
        //                if (hr.Read()){                 
        //                    results.LastRun = hr.GetInt32(hr.GetOrdinal("last_run_date"));
        //                    results.LastTime = hr.GetInt32(hr.GetOrdinal("last_run_time"));
        //                    results.Status = (BackupStatus)hr.GetInt32(hr.GetOrdinal("last_run_outcome"));
        //                    results.CurrentStep = hr.GetString(hr.GetOrdinal("current_execution_step"));
        //                    results.RetryCount = hr.GetInt32(hr.GetOrdinal("current_retry_attempt"));
        //                    results.ExecutionStatus = (ExecutionStatus)hr.GetInt32(hr.GetOrdinal("current_execution_status"));
        //                } else {
        //                    results.Error = "No status";
        //                }      
        //                hr.Close();
        //                hr = null;
        //          
        //                cmd = new SqlCommand("EXEC sp_help_job NULL,'myBackupJob','TARGETS'", this.Connection);
        //                hr = cmd.ExecuteReader();
        //                if (hr.Read()){                 
        //                    results.LastRunDuration = hr.GetInt32(hr.GetOrdinal("last_run_duration"));
        //                    results.LastRunOutcome = (BackupStatus)hr.GetByte(hr.GetOrdinal("last_run_outcome"));
        //                    results.Error = this.ReadDbString(hr, hr.GetOrdinal("last_outcome_message"));
        //                }      
        //                hr.Close();
        //                hr = null;
        //                
        //            } catch (Exception e){
        //                results.Error = e.Message;
        //            } finally {
        //                if (hr != null) hr.Close();
        //            }
        //            return results;
        //        }


        internal void Attach()
        {
            string mdfPhysicalPath = Path.Combine(Path.GetDirectoryName(this.DatabasePath), this.DatabaseName + ".mdf");

            string logPhysicalPath = Path.Combine(Path.GetDirectoryName(this.DatabasePath), this.DatabaseName + ".ldf");

            if (File.Exists(mdfPhysicalPath))
            {

                string conStr = GetConnectionString(false);
                using (SqlConnection con = new SqlConnection(conStr))
                {
                    con.Open();

                    if (!Exists)
                    {
                        string attachCommand = null;
                        string dir = Path.GetDirectoryName(this.DatabasePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            AclDatabasePath(dir);
                            attachCommand = string.Format(@"CREATE DATABASE {0} ON (FILENAME = '{1}'), (FILENAME = '{2}') FOR ATTACH",
                                                            this.DatabaseName, mdfPhysicalPath, logPhysicalPath);
                        }

                        using (SqlCommand cmd2 = new SqlCommand(attachCommand, con))
                        {
                            cmd2.ExecuteNonQuery();
                        }
                    }

                    if (con != null && con.State != ConnectionState.Closed)
                    {
                        con.Close();
                    }
                }
            }

        }

        public virtual bool TableExists(string tableName)
        {
            object result = ExecuteScalar("select * from INFORMATION_SCHEMA.tables where table_name = '" + tableName + "'");
            return (result != null);
        }

        /// <summary>
        /// Get the schema of the given table
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>Returns the list of columns found</returns>
        public virtual List<ColumnMapping> GetTableSchema(string tableName)
        {
            var reader = ExecuteReader("select COLUMN_NAME,IS_NULLABLE,DATA_TYPE,CHARACTER_MAXIMUM_LENGTH from INFORMATION_SCHEMA.COLUMNS where table_name = '" + tableName + "'");

            List<ColumnMapping> columns = new List<ColumnMapping>();
            // ok, this is the hard part, we need to diff the table in SQL with the object Type and see if the table
            // needs to be altered.
            while (reader.Read())
            {
                ColumnMapping column = new ColumnMapping()
                {
                    ColumnName = SqlServerDatabase.ReadDbString(reader, 0),
                    AllowNulls = ReadYesNoAsBoolean(reader, 1),
                    SqlType = ReadSqlDataType(reader, 2),
                    MaxLength = SqlServerDatabase.ReadInt32(reader, 3)
                };
                columns.Add(column);
            }
            reader.Close();
            return columns;
        }

        internal static bool ReadYesNoAsBoolean(IDataReader reader, int i)
        {
            string value = SqlServerDatabase.ReadDbString(reader, i);
            if (value == "YES")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static Type ReadSqlDataType(IDataReader reader, int i)
        {
            string value = SqlServerDatabase.ReadDbString(reader, i);

            switch (value)
            {
                case "int":
                case "integer":
                case "numeric":
                    return typeof(SqlInt32);
                case "char":
                    return typeof(SqlAscii);
                case "nchar":
                case "nvarchar":
                    return typeof(SqlChars);
                case "money":
                    return typeof(SqlMoney);
                case "datetime":
                    return typeof(SqlDateTime);
                case "uniqueidentifier":
                    return typeof(SqlGuid);
                case "decimal":
                    return typeof(SqlDecimal);
                case "bigint":
                    return typeof(SqlInt64);
                case "smallint":
                    return typeof(SqlInt16);
                case "tinyint":
                    return typeof(SqlByte);
                case "float":
                    return typeof(SqlSingle);
                case "real":
                    return typeof(SqlDouble);
                case "bit":
                    return typeof(SqlBoolean);
                default:
                    throw new NotImplementedException(string.Format("SQL type '{0}' is not supported by the mapping engine", value));
            }
        }

        internal void AppendLog(string cmd)
        {
            this.log.AppendLine(cmd);
        }

    }

    // This is a fake SQL type so we can differentiate between "char" and "nchar"
    class SqlAscii
    {
    }

    public class BackupResults
    {
        public int LastRun;
        public int LastTime;
        public int LastRunDuration;
        public BackupStatus Status;
        public ExecutionStatus ExecutionStatus;
        public BackupStatus LastRunOutcome;
        public string CurrentStep;
        public int RetryCount;
        public string Error;
    }

    public enum BackupStatus
    {
        Failed = 0,
        Succeeded = 1,
        Canceled = 3,
        Unknown = 5
    }

    public enum ExecutionStatus
    {
        None,
        Executing,
        Waiting,
        Retrying,
        Idle,
        Suspending,
        Completing,
    }

    public class DataError
    {
        long id;
        int sid;
        string msg;

        public DataError(long id, string error)
        {
            this.id = id; this.msg = error;
        }
        public DataError(long id, int sid, string error)
        {
            this.id = id; this.sid = sid; this.msg = error;
        }
        public long Transaction { get { return this.id; } }
        public int Split { get { return this.sid; } }
        public string Message { get { return this.msg; } }

        public void Heal(MyMoney m)
        {
            // heal thyself!
        }

        public override string ToString()
        {
            return this.msg;
        }

    }
}
