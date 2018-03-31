using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.ComponentModel;

namespace Walkabout.Utilities
{
    public enum CredentialType
    {
        None = 0,
        Generic = 1, // CRED_TYPE_GENERIC,
        DomainPassword = 2, // CRED_TYPE_DOMAIN_PASSWORD,
        DomainCertificate = 3, // CRED_TYPE_DOMAIN_CERTIFICATE,
        DomainVisiblePassword = 4, // CRED_TYPE_DOMAIN_VISIBLE_PASSWORD
        GenericCertificate = 5, // CRED_TYPE_GENERIC_CERTIFICATE
        DomainExtended = 6, // CRED_TYPE_DOMAIN_EXTENDED
    }

    public enum CredentialFlags
    {
        PromptNow = 2, // CRED_FLAGS_PROMPT_NOW
        UserNameTarget = 4 // CRED_FLAGS_USERNAME_TARGET
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CREDENTIAL_ATTRIBUTE
    {
        string Keyword;
        int Flags;
        int ValueSize;
        IntPtr Value; // LPBYTE 
    };

    public enum CredentialPersistence
    {
        Session = 1, // CRED_PERSIST_SESSION
        LocalComputer = 2, // CRED_PERSIST_LOCAL_MACHINE
        Enterprise = 3, // CRED_PERSIST_ENTERPRISE
    }

    public class Credential : IDisposable
    {
        SecureString password;
        string userName;
        string targetName;
        string targetAlias;
        string description;
        CredentialType type;
        CredentialPersistence persist;
        DateTime lastWriteTime;

        public Credential(string targetName, CredentialType type)
        {
            if (null == targetName)
            {
                throw new ArgumentNullException("targetName");
            }
            this.targetName = targetName;
            this.type = type;
        }

        public string TargetName
        {
            get { return targetName; }
            set { targetName = value; }
        }

        public CredentialType CredentialType
        {
            get { return (CredentialType)this.type; }
            set { this.type = value; }
        }

        public string UserName
        {
            get { return this.userName; }
            set { this.userName = value; }
        }

        public string TargetAlias
        {
            get { return this.targetAlias; }
            set { this.targetAlias = value; }
        }

        public CredentialPersistence Persistence
        {
            get { return this.persist; }
            set { this.persist = value; }
        }

        public SecureString Password
        {
            get { return this.password; }
            set { this.password = value; }
        }

        public string Description
        {

            get { return this.description; }
            set { this.description = value; }
        }

        public DateTime LastWriteTime
        {
            get { return lastWriteTime; }
            set { this.lastWriteTime = value; }
        }

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] CREDENTIAL userCredential, [In] int flags);

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredReadW", CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string targetName, int type, int flags, out IntPtr credential);

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string targetName, int type, int flags);

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredFree", CharSet = CharSet.Unicode)]
        private static extern void CredFree([In] IntPtr cred);

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            GC.SuppressFinalize(this);
            password = null;
            TargetName = null;
            UserName = null;
            TargetAlias = null;
            Description = null;
        }

        ~Credential()
        {
            Dispose(false);
        }

        private const int ERROR_NO_SUCH_LOGON_SESSION = 1312;
        private const int ERROR_BAD_USERNAME = 2202;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_INVALID_FLAGS = 1004;
        private const int ERROR_NOT_FOUND = 1168;

        [StructLayout(LayoutKind.Sequential)]
        private class CREDENTIAL
        {
            public UInt32 Flags;
            public UInt32 Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public long LastWritten;
            public UInt32 CredentialBlobSize;
            public IntPtr CredentialBlob;
            public UInt32 Persist;
            public UInt32 AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }


        internal void Save()
        {
            LastWriteTime = DateTime.Now;

            CREDENTIAL data = new CREDENTIAL();
            data.CredentialBlobSize = (uint)this.password.Length * sizeof(char); // in bytes

            IntPtr bstr = Marshal.SecureStringToBSTR(this.Password);
            data.CredentialBlob = bstr;
            data.Type = (uint)this.type;
            data.Persist = (uint)this.persist;
            data.AttributeCount = 0;
            data.Attributes = IntPtr.Zero;
            data.LastWritten = this.lastWriteTime.ToFileTime();

            if (!string.IsNullOrEmpty(this.targetName))
            {
                data.TargetName = Marshal.StringToCoTaskMemUni(this.targetName);
            }
            if (!string.IsNullOrEmpty(this.userName))
            {
                data.UserName = Marshal.StringToCoTaskMemUni(this.userName);
            }
            if (!string.IsNullOrEmpty(this.targetAlias))
            {
                data.TargetAlias = Marshal.StringToCoTaskMemUni(this.targetAlias);
            }
            if (!string.IsNullOrEmpty(this.description))
            {
                data.Comment = Marshal.StringToCoTaskMemUni(this.description);
            }


            try
            {
                if (!CredWrite(data, 0))
                {
                    int result = Marshal.GetLastWin32Error();
                    string msg = "Internal Error";

                    switch (result)
                    {
                        case ERROR_NO_SUCH_LOGON_SESSION:
                            msg = "The logon session does not exist or there is no credential set associated with this logon session. Network logon sessions do not have an associated credential set";
                            break;
                        case ERROR_BAD_USERNAME:
                            msg = "The UserName member of the passed in Credential structure is not valid. For a description of valid user name syntax, see the definition of that member.";
                            break;
                        case ERROR_INVALID_PARAMETER:
                            msg = "Certain fields cannot be changed in an existing credential. This error is returned if a field does not match the value in a protected field of the existing credential";
                            break;
                        case ERROR_INVALID_FLAGS:
                            msg = "A value that is not valid was specified for the Flags parameter";
                            break;
                    }

                    throw new Win32Exception(result, msg);
                }
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
                data.CredentialBlob = IntPtr.Zero; // remember this is freed now.

                // free the memory we allocated above.
                if (data.TargetName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(data.TargetName);
                }
                if (data.UserName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(data.UserName);
                }
                if (data.TargetAlias != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(data.TargetAlias);
                }
                if (data.Comment != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(data.Comment);
                }
            }
        }

        public void Load()
        {
            IntPtr block = IntPtr.Zero;
            if (!CredRead(this.TargetName, (int)this.CredentialType, 0, out block))
            {
                int result = Marshal.GetLastWin32Error();
                string msg = "Internal Error";
                switch (result)
                {
                    case ERROR_NOT_FOUND:
                        msg = "No credential exists with the specified TargetName.";
                        break;

                    case ERROR_NO_SUCH_LOGON_SESSION:
                        msg = "The logon session does not exist or there is no credential set associated with this logon session. Network logon sessions do not have an associated credential set";
                        break;

                }
                throw new Win32Exception(result, msg);
            }

            CREDENTIAL data = new CREDENTIAL();
            Marshal.PtrToStructure(block, data);

            this.targetName = GetNativeString(data.TargetName);
            this.userName = GetNativeString(data.UserName);
            this.targetAlias = GetNativeString(data.TargetAlias);
            this.description = GetNativeString(data.Comment);
            this.persist = (CredentialPersistence)data.Persist;
            this.lastWriteTime = DateTime.FromFileTime(data.LastWritten);

            int len = (int)data.CredentialBlobSize / sizeof(char);
            this.password = Credential.ToSecureString(Marshal.PtrToStringUni(data.CredentialBlob, len));

            // free the memory CredRead allocated.
            CredFree(block);
        }

        private string GetNativeString(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                return Marshal.PtrToStringUni(ptr);
            }
            return null;
        }

        public static void Delete(string targetName, CredentialType type)
        {
            if (null == targetName)
            {
                throw new ArgumentNullException("targetName");
            }

            if (!CredDelete(targetName, (int)type, 0))
            {
                int result = Marshal.GetLastWin32Error();
                string msg = "Internal Error";

                switch (result)
                {
                    case ERROR_NOT_FOUND:
                        msg = "There is no credential with the specified TargetName";
                        break;
                    case ERROR_NO_SUCH_LOGON_SESSION:
                        msg = "The logon session does not exist or there is no credential set associated with this logon session. Network logon sessions do not have an associated credential set";
                        break;
                }

                throw new Win32Exception(result, msg);
            }
        }

        public static SecureString ToSecureString(String s)
        {
            SecureString ss = new SecureString();
            for (int i = 0, n = s.Length; i < n; i++)
            {
                ss.AppendChar(s[i]);
            }
            return ss;
        }


        public static string SecureStringToString(SecureString secureString)
        {
            IntPtr bstr = IntPtr.Zero;

            try
            {
                bstr = Marshal.SecureStringToBSTR(secureString);
                return Marshal.PtrToStringBSTR(bstr);
            }
            finally
            {
                if (bstr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstr);
                }
            }
        }

    }
}

