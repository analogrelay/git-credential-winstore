using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Git.Credential.WinStore
{
    internal static class NativeMethods
    {
        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
        public static extern bool CredWrite(ref CREDENTIAL userCredential, UInt32 flags);

        [DllImport("advapi32.dll", EntryPoint="CredReadW", CharSet=CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
        public static extern bool CredDelete(string target, CRED_TYPE type, int flags);

        [DllImport("advapi32.dll")]
        public static extern void CredFree(IntPtr credentialPtr);

        public enum CRED_TYPE : int
        {
            GENERIC = 1,
            DOMAIN_PASSWORD = 2,
            DOMAIN_CERTIFICATE = 3,
            DOMAIN_VISIBLE_PASSWORD = 4,
            MAXIMUM = 5
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREDENTIAL
        {
            public int flags;
            public int type;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string targetName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME lastWritten;
            public int credentialBlobSize;
            public IntPtr credentialBlob;
            public int persist;
            public int attributeCount;
            public IntPtr credAttribute;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string targetAlias;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string userName;
        } 
    }
}
