using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Windows;
using Walkabout.Controls;
using Walkabout.Utilities;

namespace Walkabout.Setup
{
    public static class DirectorySetup
    {
        public static void AddWritePermission(string accountName, string path)
        {
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

            groups.Add(accountName);

            string dir = path;
            Directory.CreateDirectory(dir);
            DirectorySecurity security = Directory.GetAccessControl(dir, AccessControlSections.Access);
            bool userHasControl = false;
            foreach (AccessRule rule in security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)))
            {
                FileSystemAccessRule fsRule = rule as FileSystemAccessRule;
                if (fsRule != null && fsRule.AccessControlType == AccessControlType.Allow && groups.Contains(fsRule.IdentityReference.Value))
                {
                    if ((fsRule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                    {
                        if (fsRule.IdentityReference.Value == accountName)
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
                if (MessageBoxEx.Show("SQL Server (NETWORK SERVICE account) does not have permission to write to this directory, " +
                    "and the current user does not have permission to give SQL server permission, so we need to elevate to administrator account." +
                    "  Are you ready to elevate as administrator?", "Need to Elevate", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                {
                    // here we have to do the work in a separate process that is elevated.
                    Walkabout.Admin.Program.AddPermissions(dir, accountName, FileSystemRights.FullControl);

                    // now make sure we're good.
                    AddWritePermission(accountName, path);
                    return;
                }

                throw new OperationCanceledException("User canceled the operation");
            }

            security = new DirectorySecurity();
            security.AddAccessRule(new FileSystemAccessRule(accountName, FileSystemRights.FullControl, AccessControlType.Allow));
            Directory.SetAccessControl(dir, security);
        }
    }
}
