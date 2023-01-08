using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;

namespace Walkabout.Setup
{
    public static class DirectorySetup
    {
        public static void AddWritePermission(IdentityReference accountId, string path)
        {
            // NOTE this is all windows specific code.

            bool userHasControl = false;

            WindowsIdentity id = WindowsIdentity.GetCurrent();
            SecurityIdentifier sid = new SecurityIdentifier(id.User.Value);
            HashSet<string> groups = new HashSet<string>();

            IdentityReference rid = sid.Translate(typeof(System.Security.Principal.NTAccount));
            groups.Add(rid.Value);
            foreach (var gid in id.Groups)
            {
                try
                {
                    rid = gid.Translate(typeof(System.Security.Principal.NTAccount));
                    groups.Add(rid.Value);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            groups.Add(accountId.Value);

            string dir = path;
            // mkae sure folder exists (and user can create it!)
            Directory.CreateDirectory(dir);

            var security = new FileSecurity(path, AccessControlSections.Access);
            foreach (AuthorizationRule rule in security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)))
            {
                FileSystemAccessRule fsRule = rule as FileSystemAccessRule;
                if (fsRule != null && fsRule.AccessControlType == AccessControlType.Allow && groups.Contains(fsRule.IdentityReference.Value))
                {
                    if ((fsRule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl ||
                        (fsRule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify)
                    {
                        if (fsRule.IdentityReference == accountId)
                        {
                            // then network service is already good to go
                            return;
                        }

                        // current user has permission to give NETWORK SERVICE full control.
                        userHasControl = true;
                    }
                }
            }

            if (!userHasControl)
            {
                // then we need to elevate to admin...
                if (MessageBox.Show("SQL Server (\"NT AUTHORITY\\NETWORK SERVICE\" account) does not have permission to write to this directory, " +
                    "so please give this account full permission to this folder using Windows Explorer Security tab and then click Ok.",
                    "Missing Account Permission", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                {
                    // now make sure we're good.
                    // AddWritePermission(accountName, path);
                    return;
                }

                throw new OperationCanceledException("User canceled the operation");
            }

            var accessRuleFullControl = new FileSystemAccessRule(accountId, FileSystemRights.Modify, AccessControlType.Allow);
            security.AddAccessRule(accessRuleFullControl);
        }
    }
}
