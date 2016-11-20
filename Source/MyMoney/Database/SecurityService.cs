using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Setup;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public class SecurityService : IDirectorySecurity
    {
        #region IDirectorySecurity

        public void AddWritePermission(string accountName, string path)
        {
            DirectorySetup.AddWritePermission(accountName, path);
        }

        #endregion 
    }
}
