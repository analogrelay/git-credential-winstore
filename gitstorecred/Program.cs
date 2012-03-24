using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace gitstorecred
{
    class Program
    {
        static void Main(string[] args)
        {
            // Parse our args

            // If the username was specified, pack it
            Tuple<IntPtr, uint> inBuf = Tuple.Create<IntPtr, uint>(IntPtr.Zero, 0);
            //if(!String.IsNullOrEmpty(options.User)) {
            //    inBuf = PackUser(options.User) ?? inBuf;
            //}

            NativeMethods.CREDUI_INFO info = new NativeMethods.CREDUI_INFO()
            {
                hbmBanner = IntPtr.Zero,
                hwndParent = IntPtr.Zero,
                pszCaptionText = "Git Credentials",
                pszMessageText = "Enter your credentials for ..."
            };
            info.cbSize = Marshal.SizeOf(info);

            IntPtr outBuf;
            uint outBufSize;
            bool save = true;
            IntPtr authPackage = IntPtr.Zero;
            NativeMethods.CredUIReturnCodes ret = NativeMethods.CredUIPromptForWindowsCredentials(
                ref info,
                authError: 0,
                authPackage: ref authPackage,
                InAuthBuffer: inBuf.Item1,
                InAuthBufferSize: inBuf.Item2,
                refOutAuthBuffer: out outBuf,
                refOutAuthBufferSize: out outBufSize,
                fSave: ref save,
                flags: NativeMethods.PromptForWindowsCredentialsFlags.CREDUIWIN_GENERIC |
                       NativeMethods.PromptForWindowsCredentialsFlags.CREDUIWIN_CHECKBOX);
            if (ret != NativeMethods.CredUIReturnCodes.NO_ERROR)
            {
                Console.Error.WriteLine("Error Prompting for Credentials: " + ret.ToString());
                Environment.Exit((int)ret);
                return;
            }

            // Unpack the credential and write the password to stdout
            int userNameSize = 255;
            int domainNameSize = 255;
            int passwordSize = 255;
            StringBuilder userName = new StringBuilder(userNameSize);
            StringBuilder domainName = new StringBuilder(domainNameSize);
            StringBuilder password = new StringBuilder(passwordSize);
            if (!NativeMethods.CredUnPackAuthenticationBuffer(
                dwFlags: 0x00,
                pAuthBuffer: outBuf,
                cbAuthBuffer: outBufSize,
                pszUserName: userName,
                pcchUserName: ref userNameSize,
                pszDomainName: domainName,
                pcchDomainName: ref domainNameSize,
                pszPassword: password,
                pcchPassword: ref passwordSize))
            {
                Console.Error.WriteLine("Error unpacking credentials");
                return;
            }
        }

        private static Tuple<IntPtr, uint> PackUser(string username)
        {
            uint size = 0;
            IntPtr packed = IntPtr.Zero;
            if (!NativeMethods.CredPackAuthenticationBuffer(
                dwFlags: 0x00,
                pszUserName: username,
                pszPassword: String.Empty,
                pPackedCredentials: ref packed,
                pcbPackedCredentials: ref size))
            {
                Console.Error.WriteLine("Error packing credentials!");
                return null;
            }
            return Tuple.Create(packed, size);
        }
    }
}
