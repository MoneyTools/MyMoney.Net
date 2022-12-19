using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;
using System.IO;

namespace Walkabout.Tests.Wrappers
{
    class ExcelWindowWrapper : DialogWrapper
    {
        private ExcelWindowWrapper(AutomationElement e) : base(e)
        {
        }

        public override void Close()
        {
            window.ClickButtonByName("Close");
        }

        public static ExcelWindowWrapper FindExcelWindow(string name, int retries, bool throwIfNotFound)
        {
            for (int i = 0; i < retries; i++)
            {
                AutomationElement e = Win32.FindDesktopWindow(name);
                if (e != null)
                {
                    var result = new ExcelWindowWrapper(e);
                    result.WaitForInputIdle(500);
                    return result;
                }

                Thread.Sleep(1000);
            }

            if (throwIfNotFound)
            {
                throw new Exception("Excel window not found for name " + name);
            }

            return null;
        }
    }
}
