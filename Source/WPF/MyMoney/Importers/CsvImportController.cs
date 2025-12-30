using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Importers
{
    class CsvImportController
    {
        private DownloadControl control;
        private MyMoney myMoney;
        private string databaseDir;

        public CsvImportController(DownloadControl control, MyMoney money, string databaseDir)
        {
            this.myMoney = money;
            this.databaseDir = databaseDir;
            this.control = control;
        }

        public async Task<int> ImportCsv(string fileName)
        {
            int total = 0;
            try
            {
                DownloadData last = null;
                var entries = new ThreadSafeObservableCollection<DownloadData>();                
                this.control.DownloadEventTree.ItemsSource = entries;

                var csv = CsvDocument.Load(fileName);
                if (csv.Headers.Contains("Account Number"))
                {
                    CsvMap map = null;
                    var grouped = CsvTransactionImporter.GroupCsvByAccount(this.myMoney, csv);
                    foreach (var key in grouped.Keys)
                    {
                        var data = new DownloadData(null, key);
                        entries.Add(data);
                        var doc = grouped[key];
                        var result = this.ImportCsvForAccount(doc, key, data, map);
                        var count = result.Item1;
                        if (map == null)
                        {
                            map = result.Item2;
                        }
                        if (count > 0)
                        {
                            data.Message = $"Downloaded {count} new transactions";
                            await this.myMoney.Rebalance(key);
                            last = data;
                        }
                        data.Success = true;
                        total += count;
                    }
                }
                else
                {
                    string prompt = "Please select Account to import the CSV transactions into";
                    var acct = AccountHelper.PickAccount(this.myMoney, null, prompt);
                    var data = new DownloadData(null, acct);
                    entries.Add(data);
                    var result = this.ImportCsvForAccount(csv, acct, data);
                    var count = result.Item1;
                    if (count > 0 && acct != null)
                    {
                        data.Message = $"Downloaded {count} new transactions";
                        last = data;
                        await this.myMoney.Rebalance(acct);
                    }
                    total += count;
                }

                if (last != null)
                {
                    this.control.SelectEntry(last);
                }

            }
            catch (UserCanceledException)
            {
            }
            catch (Exception ex)
            {
                // this.log.Error("Import Error", ex);
                MessageBoxEx.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            return total;
        }

        private Tuple<int, CsvMap> ImportCsvForAccount(CsvDocument csv, Account acct, DownloadData data, CsvMap defaultMap = null)
        {
            int count = 0;
            CsvMap map = null;
            // load existing csv map if we have one.
            map = this.LoadMap(acct, defaultMap);
            var fields = acct.Type == AccountType.Brokerage || acct.Type == AccountType.Retirement ?
                CsvTransactionImporter.BrokerageAccountFields :
                CsvTransactionImporter.BankAccountFields;

            var importer = new CsvTransactionImporter(this.myMoney, acct, map, data, fields);
            count = importer.Import(csv);
            importer.Commit();
            map.Save();
            return new Tuple<int, CsvMap>(count, map);
        }

        private CsvMap LoadMap(Account a, CsvMap defaultMap)
        {
            CsvMap map = null;
            if (!string.IsNullOrEmpty(this.databaseDir))
            {
                var dir = Path.Combine(this.databaseDir, "CsvMaps");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var filename = Path.Combine(dir, a.Id + ".xml");
                map = CsvMap.Load(filename);
            }
            else
            {
                map = new CsvMap();
            }
            if (defaultMap != null && map.Fields == null)
            {
                map.CopyFrom(defaultMap);
            }
            return map;
        }

    }
}
