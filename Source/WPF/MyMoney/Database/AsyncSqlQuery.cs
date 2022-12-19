using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public class SqlQueryResultArgs : EventArgs
    {
        public IDataReader DataReader { get; set; }
        public Exception Error { get; set; }
    }

    public class AsyncSqlQuery
    {
        string connectionString;
        string query;
        bool cancelled;
        SqlCommand asyncCommand;
        EventHandlerCollection<SqlQueryResultArgs> handlers;

        internal AsyncSqlQuery()
        {
        }

        public event EventHandler<SqlQueryResultArgs> Completed
        {
            add
            {
                if (handlers == null)
                {
                    handlers = new EventHandlerCollection<SqlQueryResultArgs>();
                }
                handlers.AddHandler(value);
            }
            remove
            {
                if (handlers != null)
                {
                    handlers.RemoveHandler(value);
                }
            }
        }

        public void BeginRunQuery(string connectionString, string query)
        {
            cancelled = false;
            this.connectionString = connectionString;
            this.query = query;
            Stop();
            var thread = new Thread(new ThreadStart(RunQuery));
            thread.Start();
        }

        public void Stop()
        {
            cancelled = true;
            if (asyncCommand != null)
            {
                try
                {
                    asyncCommand.Cancel();
                }
                catch
                {
                }
                asyncCommand = null;
            }
        }

        private void OnCompleted(IDataReader reader, Exception error)
        {
            if (handlers != null && handlers.HasListeners)
            {
                handlers.RaiseEvent(this, new SqlQueryResultArgs() { DataReader = reader, Error = error });
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private void RunQuery()
        {
            SqlConnection con = null;
            SqlDataReader reader = null;
            Exception error = null;
            try
            {
                con = new SqlConnection(connectionString + ";Asynchronous Processing=true");
                con.Open();
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    asyncCommand = cmd;
                    IAsyncResult result = cmd.BeginExecuteReader();
                    reader = cmd.EndExecuteReader(result);
                }
            }
            catch (Exception ex)
            {
                if (!cancelled)
                {
                    error = ex;
                }
            }
            finally
            {
                asyncCommand = null;
                OnCompleted(reader, error);
                using (reader) // dispose the reader
                {
                }
                if (con != null && con.State != System.Data.ConnectionState.Closed)
                {
                    con.Close();
                }
                using (con) // dispose the connection
                {
                }
            }
        }

    }
}
