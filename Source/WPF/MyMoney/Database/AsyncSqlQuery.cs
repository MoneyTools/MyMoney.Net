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
        private string connectionString;
        private string query;
        private bool cancelled;
        private SqlCommand asyncCommand;
        private EventHandlerCollection<SqlQueryResultArgs> handlers;

        internal AsyncSqlQuery()
        {
        }

        public event EventHandler<SqlQueryResultArgs> Completed
        {
            add
            {
                if (this.handlers == null)
                {
                    this.handlers = new EventHandlerCollection<SqlQueryResultArgs>();
                }
                this.handlers.AddHandler(value);
            }
            remove
            {
                if (this.handlers != null)
                {
                    this.handlers.RemoveHandler(value);
                }
            }
        }

        public void BeginRunQuery(string connectionString, string query)
        {
            this.cancelled = false;
            this.connectionString = connectionString;
            this.query = query;
            this.Stop();
            var thread = new Thread(new ThreadStart(this.RunQuery));
            thread.Start();
        }

        public void Stop()
        {
            this.cancelled = true;
            if (this.asyncCommand != null)
            {
                try
                {
                    this.asyncCommand.Cancel();
                }
                catch
                {
                }
                this.asyncCommand = null;
            }
        }

        private void OnCompleted(IDataReader reader, Exception error)
        {
            if (this.handlers != null && this.handlers.HasListeners)
            {
                this.handlers.RaiseEvent(this, new SqlQueryResultArgs() { DataReader = reader, Error = error });
            }
        }

        private void RunQuery()
        {
            SqlConnection con = null;
            SqlDataReader reader = null;
            Exception error = null;
            try
            {
                con = new SqlConnection(this.connectionString + ";Asynchronous Processing=true");
                con.Open();
                using (SqlCommand cmd = new SqlCommand(this.query, con))
                {
                    this.asyncCommand = cmd;
                    IAsyncResult result = cmd.BeginExecuteReader();
                    reader = cmd.EndExecuteReader(result);
                }
            }
            catch (Exception ex)
            {
                if (!this.cancelled)
                {
                    error = ex;
                }
            }
            finally
            {
                this.asyncCommand = null;
                this.OnCompleted(reader, error);
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
