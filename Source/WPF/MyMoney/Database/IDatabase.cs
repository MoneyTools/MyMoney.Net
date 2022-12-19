using System.Data;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    /// <summary>
    /// Interface for talking to different types of money storage.
    /// </summary>
    public interface IDatabase
    {
        string Server { get; }
        string DatabasePath { get; }
        string UserId { get; }
        string Password { get; set; }
        string BackupPath { get; }
        bool SupportsUserLogin { get; }

        DbFlavor DbFlavor { get; }

        bool Exists { get; }
        void Create();
        MyMoney Load(IStatusService status);
        void Save(MyMoney money);
        void Backup(string path);
        bool UpgradeRequired { get; }
        void Upgrade();
        void Delete();

        string GetLog();
        DataSet QueryDataSet(string cmd);
        void Disconnect();
    }
}
