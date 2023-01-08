using System.Security.Principal;
using Walkabout.Setup;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public class SecurityService : IDirectorySecurity
    {
        #region IDirectorySecurity

        public void AddWritePermission(IdentityReference accountId, string path)
        {
            DirectorySetup.AddWritePermission(accountId, path);
        }

        #endregion 
    }
}
