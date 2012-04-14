using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace gitstorecred
{
    class Program
    {
        static Regex GitRequestRegex = new Regex(@"^(?<req>(Username)|(Password)) for '(?<url>[^']*)':\s*$");

        static void Main(string[] args)
        {
            // Parse a line off standard in
            string line = Console.ReadLine();
            Match m = GitRequestRegex.Match(line, 0);
            string request = m.Groups["req"].Value;
            Uri url = (new Uri(m.Groups["url"].Value));

            // Extract the URL without user name to use as realm
            string realm = url.GetComponents(
                UriComponents.SchemeAndServer | 
                UriComponents.Port | 
                UriComponents.PathAndQuery, 
                UriFormat.Unescaped);

            // Try to get a credential

            bool save = true;
            int userNameSize = 255;
            int domainNameSize = 255;
            int passwordSize = 255;
            StringBuilder userName = new StringBuilder(userNameSize);
            StringBuilder domainName = new StringBuilder(domainNameSize);
            StringBuilder password = new StringBuilder(passwordSize);

            NativeMethods.CREDUI_FLAGS flags = 
                NativeMethods.CREDUI_FLAGS.EXCLUDE_CERTIFICATES |
                NativeMethods.CREDUI_FLAGS.GENERIC_CREDENTIALS |
                NativeMethods.CREDUI_FLAGS.SHOW_SAVE_CHECK_BOX;
            if(String.Equals("Password", request, StringComparison.OrdinalIgnoreCase) && !String.IsNullOrEmpty(url.UserInfo)) {
                userName.Append(url.UserInfo);
                flags |= NativeMethods.CREDUI_FLAGS.KEEP_USERNAME;
            }

            NativeMethods.CREDUI_INFO info = new NativeMethods.CREDUI_INFO() {
                pszCaptionText = "Git Credentials",
                pszMessageText = "Enter your credentials for " + realm,
            };
            info.cbSize = Marshal.SizeOf(info);

            NativeMethods.CredUIReturnCodes ret = NativeMethods.CredUIPromptForCredentials(
                ref info,
                targetName: "gitstorecred_" + realm,
                reserved1: IntPtr.Zero,
                iError: 0,
                userName: userName,
                maxUserName: 255,
                password: password,
                maxPassword: 255,
                pfSave: ref save,
                flags: flags);
            if (ret != NativeMethods.CredUIReturnCodes.NO_ERROR)
            {
                Console.Error.WriteLine("Error Prompting for Credentials: " + ret.ToString());
                Environment.Exit((int)ret);
                return;
            }

            if(String.Equals("Username", request, StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine(userName.ToString());
            } else if(String.Equals("Password", request, StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine(password.ToString());
            }
        }
    }
}
