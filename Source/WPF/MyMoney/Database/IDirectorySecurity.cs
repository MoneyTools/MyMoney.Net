using System.Security.Principal;

namespace Walkabout.Utilities
{
    public interface IDirectorySecurity
    {
        /// <summary>
        /// Give the specified account write permission on the given directory.
        /// </summary>
        /// <param name="accountId">The account being added</param>
        /// <param name="path">The directory it needs access to</param>
        void AddWritePermission(IdentityReference accountId, string path);
    }
}
