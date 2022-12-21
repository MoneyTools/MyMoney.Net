using Microsoft.Win32;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace Walkabout.Data
{
    /// <summary>
    /// This class wraps the lambdas that call out to the real SQL CE engine.
    /// </summary>
    public class SqlCeEngine : IDisposable
    {
        private readonly object engine;
        private readonly Action<object> upgrade;
        private readonly Action<object> create;
        private readonly Action<object> dispose;

        internal SqlCeEngine(object engine, Action<object> upgrade, Action<object> create, Action<object> dispose)
        {
            this.engine = engine;
            this.upgrade = upgrade;
            this.create = create;
            this.dispose = dispose;
        }

        public void CreateDatabase()
        {
            this.create(this.engine);
        }

        public void Dispose()
        {
            this.dispose(this.engine);
        }

        public void Upgrade()
        {
            this.upgrade(this.engine);
        }
    }

    /// <summary>
    /// This class wraps the lambdas that call out to the real SQL CE types, providing a lazy loaded dependency on SQL CE
    /// so that ClickOnce doesn't complain if SQL CE is missing.
    /// </summary>    
    internal class SqlCeFactory
    {
        private static readonly Assembly assembly;
        private static Func<string, object> createEngine;
        private static Action<object> upgrade;
        private static Action<object> createDatabase;
        private static Action<object> disposeEngine;
        private static Func<string, DbConnection> createConnection;
        private static Func<string, DbConnection, DbCommand> createCommand;
        private static Func<string, DbConnection, DbDataAdapter> createDataAdapter;

        static SqlCeFactory()
        {
            foreach (string supportedVersion in new string[] { "4.0.0.0" })
            {
                var name = new System.Reflection.AssemblyName("System.Data.SqlServerCe, Version=" + supportedVersion + ", Culture=neutral, PublicKeyToken=89845dcd8080cc91, processorArchitecture=MSIL");

                try
                {
                    // This wil throw if SQL CE is not installed.  THe code should have called IsSQLCEInstalled before calling into this path.
                    assembly = Assembly.Load(name.FullName);
                    break;
                }
                catch
                {
                }
            }

            if (assembly != null)
            {
                CreateEngineLambdas();
                CreateConnectionLambda();
                CreateCommandLambda();
                CreateDataAdapterLambda();
            }
        }

        private static void CheckError()
        {
            if (assembly == null)
            {
                throw new Exception(@"A supported version of SQL Server Compact Edition was not found.
Please install it from http://www.microsoft.com/download/en/details.aspx?id=17876");
            }
        }

        public static SqlCeEngine CreateSqlCeEngine(string constr)
        {
            CheckError();
            return new SqlCeEngine(createEngine(constr), upgrade, createDatabase, disposeEngine);
        }

        public static DbConnection CreateSqlCeConnection(string constr)
        {
            CheckError();
            return createConnection(constr);
        }

        public static DbCommand CreateSqlCeCommand(string commandString, DbConnection connection)
        {
            CheckError();
            return createCommand(commandString, connection);
        }

        public static DbDataAdapter CreateSqlCeDataAdapter(string queryString, DbConnection connection)
        {
            CheckError();
            return createDataAdapter(queryString, connection);
        }

        private static void CreateEngineLambdas()
        {
            // create lambda that constructs new SqlCeConnection and returns it as a DbConnection.
            Type conType = assembly.GetType("System.Data.SqlServerCe.SqlCeEngine");
            ConstructorInfo ci = conType.GetConstructor(new Type[] { typeof(string) });

            ParameterExpression parameter = Expression.Parameter(typeof(string), "connectionString");
            var exp = System.Linq.Expressions.Expression.New(ci, parameter);
            var converted = Expression.Convert(exp, typeof(object));

            Expression<Func<string, object>> lambda = Expression.Lambda<Func<string, object>>(converted, parameter);
            createEngine = lambda.Compile();

            CreateUpgradeLambda(conType);
            CreateDatabaseLambda(conType);
            CreateDisposeEngineLambda(conType);
        }

        private static void CreateUpgradeLambda(Type engineType)
        {
            //------------------------------------------
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression toEngine = Expression.Convert(instance, engineType);
            MethodInfo mi = engineType.GetMethod("Upgrade", BindingFlags.Instance | BindingFlags.Public, null, new Type[0], null);
            Expression call = Expression.Call(toEngine, mi);
            Expression<Action<object>> lambda = Expression.Lambda<Action<object>>(call, instance);
            upgrade = lambda.Compile();
        }

        private static void CreateDatabaseLambda(Type engineType)
        {
            //------------------------------------------
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression toEngine = Expression.Convert(instance, engineType);
            MethodInfo mi = engineType.GetMethod("CreateDatabase");
            Expression call = Expression.Call(toEngine, mi);
            Expression<Action<object>> lambda = Expression.Lambda<Action<object>>(call, instance);
            createDatabase = lambda.Compile();
        }

        private static void CreateDisposeEngineLambda(Type engineType)
        {
            //------------------------------------------
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression toEngine = Expression.Convert(instance, engineType);
            MethodInfo mi = engineType.GetMethod("Dispose");
            Expression call = Expression.Call(toEngine, mi);
            Expression<Action<object>> lambda = Expression.Lambda<Action<object>>(call, instance);
            disposeEngine = lambda.Compile();
        }

        private static void CreateConnectionLambda()
        {
            // create lambda that constructs new SqlCeConnection and returns it as a DbConnection.
            Type conType = assembly.GetType("System.Data.SqlServerCe.SqlCeConnection");
            ConstructorInfo ci = conType.GetConstructor(new Type[] { typeof(string) });

            ParameterExpression parameter = Expression.Parameter(typeof(string), "connectionString");
            var exp = System.Linq.Expressions.Expression.New(ci, parameter);
            var converted = Expression.Convert(exp, typeof(DbConnection));

            Expression<Func<string, DbConnection>> lambda = Expression.Lambda<Func<string, DbConnection>>(converted, parameter);
            createConnection = lambda.Compile();
        }

        private static void CreateCommandLambda()
        {
            // create lambda that constructs new SqlCeCommand and returns DbCommand.
            Type cmdType = assembly.GetType("System.Data.SqlServerCe.SqlCeCommand");
            Type conType = assembly.GetType("System.Data.SqlServerCe.SqlCeConnection");

            ConstructorInfo ci = cmdType.GetConstructor(new Type[] { typeof(string), conType });

            ParameterExpression parameter1 = Expression.Parameter(typeof(string), "commandString");
            ParameterExpression parameter2 = Expression.Parameter(typeof(DbConnection), "connection");
            var toSqlCeConnection = Expression.Convert(parameter2, conType);
            var exp = System.Linq.Expressions.Expression.New(ci, parameter1, toSqlCeConnection);

            var converted = Expression.Convert(exp, typeof(DbCommand));

            Expression<Func<string, DbConnection, DbCommand>> lambda = Expression.Lambda<Func<string, DbConnection, DbCommand>>(converted, parameter1, parameter2);
            createCommand = lambda.Compile();
        }

        private static void CreateDataAdapterLambda()
        {
            // create lambda that constructs new SqlCeCommand and returns DbCommand.
            Type adapterType = assembly.GetType("System.Data.SqlServerCe.SqlCeDataAdapter");
            Type conType = assembly.GetType("System.Data.SqlServerCe.SqlCeConnection");

            ConstructorInfo ci = adapterType.GetConstructor(new Type[] { typeof(string), conType });

            ParameterExpression parameter1 = Expression.Parameter(typeof(string), "query");
            ParameterExpression parameter2 = Expression.Parameter(typeof(DbConnection), "connection");
            var toSqlCeConnection = Expression.Convert(parameter2, conType);
            var exp = System.Linq.Expressions.Expression.New(ci, parameter1, toSqlCeConnection);

            var converted = Expression.Convert(exp, typeof(DbDataAdapter));

            Expression<Func<string, DbConnection, DbDataAdapter>> lambda = Expression.Lambda<Func<string, DbConnection, DbDataAdapter>>(converted, parameter1, parameter2);
            createDataAdapter = lambda.Compile();
        }

    }


    public class SqlCeDatabase : SqlServerDatabase
    {
        public SqlCeDatabase()
        {

        }

        /// <summary>
        /// Return true if SQL CE is installed.
        /// </summary>
        public static bool IsSqlCEInstalled
        {
            get
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Microsoft SQL Server Compact Edition"))
                {
                    Version supported = new Version(4, 0, 0); // minimum supported version.
                    if (key != null)
                    {
                        foreach (string versionName in key.GetSubKeyNames())
                        {
                            using (RegistryKey vkey = key.OpenSubKey(versionName))
                            {
                                string fullVersion = (string)vkey.GetValue("Version");
                                if (!string.IsNullOrEmpty(fullVersion))
                                {
                                    Version v = Version.Parse(fullVersion);
                                    if (v >= supported)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    return false;
                }
            }
        }

        public static string OfficialSqlCeFileExtension = ".myMoney.sdf";

        private DbConnection sqlCEConnection;

        public override bool SupportsBatchUpdate { get { return false; } }

        protected override string GetConnectionString(bool includeDatabase)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = this.DatabasePath;
            //if (includeDatabase)
            //{
            //    cstr += "Initial Catalog=" + this.name;
            //}
            //if (this.integratedSecurity)
            //{
            //    cstr += ";Integrated Security=SSPI";
            //}
            //else
            //{
            //    if (!string.IsNullOrEmpty(userId))
            //    {
            //        cstr += ";User Id=" + userId;
            //    }
            if (!string.IsNullOrEmpty(this.Password))
            {
                builder.Password = this.Password;
            }
            //    cstr += ";Workstation Id=" + Environment.MachineName;
            //}

            // cstr += ";connection timeout=20";

            return builder.ConnectionString;
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
                using (SqlCeEngine en = SqlCeFactory.CreateSqlCeEngine(connectionString))
                {
                    en.CreateDatabase();
                }
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

        public static SqlCeDatabase Restore(string backup, string databaseFile, string password)
        {
            string fullBackupPath = Path.GetFullPath(backup);
            string fullDatabasePath = Path.GetFullPath(databaseFile);

            var result = new SqlCeDatabase()
            {
                DatabasePath = fullBackupPath,
                Password = password
            };

            result.Connect(); // make sure we can connect to it.
            result.Disconnect();

            // Ok, then we're good to copy it.
            File.Copy(fullBackupPath, fullDatabasePath, true);

            return new SqlCeDatabase()
            {
                DatabasePath = fullDatabasePath,
                Password = password,
                BackupPath = fullBackupPath
            };
        }

        public override DbFlavor DbFlavor
        {
            get { return Data.DbFlavor.SqlCE; }
        }

        public override bool UpgradeRequired
        {
            get
            {
                try
                {
                    this.Connect();
                    return false;
                }
                catch (Exception e)
                {
                    return e.GetType().Name.Contains("SqlCeInvalidDatabaseFormatException");
                }
            }
        }

        public override void Upgrade()
        {
            string constr = this.GetConnectionString(true);
            using (SqlCeEngine en = SqlCeFactory.CreateSqlCeEngine(constr))
            {
                en.Upgrade();
            }
            this.sqlCEConnection.Open();
        }

        public override DbConnection Connect()
        {
            if (this.sqlCEConnection == null || this.sqlCEConnection.State != ConnectionState.Open)
            {
                if (!IsSqlCEInstalled)
                {
                    throw new MoneyException(@"<Paragraph xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>SQL Compact Edition is not installed, please download it from 
                        <Hyperlink NavigateUri='http://www.microsoft.com/download/en/details.aspx?id=17876'>Microsoft Download Center</Hyperlink> 
                        and install the version matching your operating system platform (Select <Bold>SSCERuntime_x64-ENU.exe</Bold> if you are running 
                        a 64 bit version of windows, otherwise <Bold>SSCERuntime_x86-ENU.exe</Bold>).  Then after it is installed restart the Money application.</Paragraph>");
                }

                string constr = this.GetConnectionString(true);
                this.sqlCEConnection = SqlCeFactory.CreateSqlCeConnection(constr);
                this.sqlCEConnection.Open();

            }
            return this.sqlCEConnection;
        }

        public override void Disconnect()
        {
            if (this.sqlCEConnection != null && this.sqlCEConnection.State == ConnectionState.Open)
            {
                try
                {
                    using (this.sqlCEConnection)
                    {
                        this.sqlCEConnection.Close();
                    }
                }
                catch { }
            }
        }

        public override bool Exists
        {
            get
            {
                return File.Exists(this.DatabasePath);
            }
        }

        public override object ExecuteScalar(string cmd)
        {
            Debug.Assert(this.DbFlavor == DbFlavor.SqlCE);

            if (cmd == null || cmd.Trim().Length == 0)
            {
                return null;
            }

            this.AppendLog(cmd);

            object result = null;
            try
            {
                this.Connect();
                using (DbCommand command = SqlCeFactory.CreateSqlCeCommand(cmd, this.sqlCEConnection))
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
            Debug.Assert(this.DbFlavor == DbFlavor.SqlCE);

            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            try
            {
                this.Connect();

                DataSet dataSet = new DataSet();

                using (DbDataAdapter da = SqlCeFactory.CreateSqlCeDataAdapter(query, this.sqlCEConnection))
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
            Debug.Assert(this.DbFlavor == DbFlavor.SqlCE);
            if (cmd == null || cmd.Trim().Length == 0)
            {
                return;
            }

            this.AppendLog(cmd);
            try
            {
                this.Connect();
                using (DbCommand command = SqlCeFactory.CreateSqlCeCommand(cmd, this.sqlCEConnection))
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
            Debug.Assert(this.DbFlavor == DbFlavor.SqlCE);

            this.AppendLog(cmd);

            try
            {
                this.Connect();
                using (DbCommand command = SqlCeFactory.CreateSqlCeCommand(cmd, this.sqlCEConnection))
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

    }
}
