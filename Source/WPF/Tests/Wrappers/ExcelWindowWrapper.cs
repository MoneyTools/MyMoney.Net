using System;
using System.Threading;
using System.Windows.Automation;
using Walkabout.Tests.Interop;

namespace Walkabout.Tests.Wrappers
{
    internal class ExcelWindowWrapper : DialogWrapper
    {
        private ExcelWindowWrapper(AutomationElement e) : base(e)
        {
        }

        public override void Close()
        {
            this.window.ClickButtonByName("Close");
        }

        public static ExcelWindowWrapper FindExcelWindow(string[] names, int retries, bool throwIfNotFound)
        {
            for (int i = 0; i < retries; i++)
            {
                for (var n = 0; n < names.Length; n++)
                {

                    AutomationElement e = Win32.FindDesktopWindow(names[n]);
                    if (e != null)
                    {
                        var result = new ExcelWindowWrapper(e);
                        result.WaitForInputIdle(500);
                        return result;
                    }

                    Thread.Sleep(500);
                }
            }

            if (throwIfNotFound)
            {
                throw new Exception("Excel window not found for name " + names);
            }

            return null;
        }
    }
}
