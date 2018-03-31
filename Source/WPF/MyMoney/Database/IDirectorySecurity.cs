using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Walkabout.Utilities
{
    public interface IDirectorySecurity
    {
        /// <summary>
        /// Give the specified account write permission on the given directory.
        /// </summary>
        /// <param name="accountName">The account being added</param>
        /// <param name="path">The directory it needs access to</param>
        void AddWritePermission(string accountName, string path);
    }
}
