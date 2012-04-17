using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Git.Credential.WinStore
{
    class Program
    {
        private static Dictionary<string, Func<IDictionary<string, string>, IEnumerable<Tuple<string, string>>>> _commands = new Dictionary<string, Func<IDictionary<string, string>, IEnumerable<Tuple<string, string>>>>(StringComparer.OrdinalIgnoreCase)
        {
            { "get", GetCommand },
            { "store", StoreCommand },
            { "erase", EraseCommand }
        };

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "-d")
            {
                Console.Error.WriteLine("Launching debugger...");
                Debugger.Launch();
                args = args.Skip(1).ToArray();
            }

            // Read arguments
            IDictionary<string, string> parameters = ReadGitParameters();

            // Parse command
            Func<IDictionary<string, string>, IEnumerable<Tuple<string, string>>> command = null;
            string cmd;
            if (args.Length == 0)
            {
                // Debugging mode, cmd is a parameter
                cmd = parameters.GetOrDefault("cmd", "get");
            }
            else
            {
                cmd = args[0];
            }

            if (!_commands.TryGetValue(cmd, out command))
            {
                WriteUsage();
                return;
            }
            IDictionary<string, string> response = command(parameters).ToDictionary(
                t => t.Item1,
                t => t.Item2);
            WriteGitParameters(response);
        }

        private static void WriteGitParameters(IDictionary<string, string> response)
        {
            foreach (var pair in response)
            {
                Console.Write("{0}={1}\n", pair.Key, pair.Value);
            }
        }

        private static IDictionary<string, string> ReadGitParameters()
        {
            string line;
            Dictionary<string, string> values = new Dictionary<string, string>();
            while (!String.IsNullOrWhiteSpace((line = Console.ReadLine())))
            {
                string[] splitted = line.Split('=');
                values[splitted[0]] = splitted[1];
            }
            return values;
        }

        private static void WriteUsage()
        {
            Console.Error.WriteLine("This application is designed to be used by git as a credential helper. See here the following like for more info: http://www.manpagez.com/man/1/git-credential-cache/");
        }

        static IEnumerable<Tuple<string, string>> GetCommand(IDictionary<string, string> args)
        {
            // Build the URL
            Uri url = ExtractUrl(args);
            string userName = args.GetOrDefault("username", null);
            string password = null;
            
            IntPtr credPtr = IntPtr.Zero;
            try
            {
                // Check for a credential
                string target = GetTargetName(url);
                if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE.GENERIC, 0, out credPtr))
                {
                    // Don't have a credential for this user.
                    credPtr = IntPtr.Zero;

                    // If we have a username, pack an input authentication buffer
                    Tuple<int, IntPtr> inputBuffer = null;;
                    IntPtr outputBuffer = IntPtr.Zero;
                    int outputBufferSize = 0;
                    try
                    {
                        inputBuffer = PackUserNameBuffer(userName);
                        if (inputBuffer == null) { yield break; }

                        // Setup UI
                        NativeMethods.CREDUI_INFO ui = new NativeMethods.CREDUI_INFO()
                        {
                            pszCaptionText = "Git Credentials",
                            pszMessageText = "Enter your credentials for: " + url.AbsoluteUri
                        };
                        ui.cbSize = Marshal.SizeOf(ui);

                        // Prompt!
                        int authPackage = 0;
                        bool save = false;
                        var ret = NativeMethods.CredUIPromptForWindowsCredentials(
                            uiInfo: ref ui,
                            authError: 0,
                            authPackage: ref authPackage,
                            InAuthBuffer: inputBuffer.Item2,
                            InAuthBufferSize: inputBuffer.Item1,
                            refOutAuthBuffer: out outputBuffer,
                            refOutAuthBufferSize: out outputBufferSize,
                            fSave: ref save,
                            flags: NativeMethods.PromptForWindowsCredentialsFlags.CREDUIWIN_GENERIC);
                        if (ret != NativeMethods.CredUIReturnCodes.NO_ERROR)
                        {
                            Console.Error.WriteLine("Error prompting for credentials: " + ret.ToString());
                            yield break;
                        }
                    }
                    finally
                    {
                        if (inputBuffer != null && inputBuffer.Item2 != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(inputBuffer.Item2);
                        }
                    }

                    try
                    {
                        // Unpack
                        if (!UnPackAuthBuffer(outputBuffer, outputBufferSize, out userName, out password))
                        {
                            yield break;
                        }
                    }
                    finally
                    {
                        if (outputBuffer != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(outputBuffer);
                        }
                    }
                }
                else
                {
                    // Decode the credential
                    NativeMethods.CREDENTIAL cred = (NativeMethods.CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(NativeMethods.CREDENTIAL));
                    userName = cred.userName;
                    password = Marshal.PtrToStringBSTR(cred.credentialBlob);
                }
                    
                yield return Tuple.Create("username", userName);
                yield return Tuple.Create("password", password);
            }
            finally
            {
                if (credPtr != IntPtr.Zero)
                {
                    NativeMethods.CredFree(credPtr);
                }
            }
        }

        private static bool UnPackAuthBuffer(IntPtr buffer, int size, out string userName, out string password)
        {
            userName = String.Empty;
            password = String.Empty;

            StringBuilder userNameBuffer = new StringBuilder(255);
            StringBuilder passwordBuffer = new StringBuilder(255);
            StringBuilder domainBuffer = new StringBuilder(255);
            int userNameSize = 255;
            int passwordSize = 255;
            int domainSize = 255;
            if (!NativeMethods.CredUnPackAuthenticationBuffer(
                dwFlags: 0,
                pAuthBuffer: buffer,
                cbAuthBuffer: size,
                pszUserName: userNameBuffer,
                pcchMaxUserName: ref userNameSize,
                pszDomainName: domainBuffer,
                pcchMaxDomainame: ref domainSize,
                pszPassword: passwordBuffer,
                pcchMaxPassword: ref passwordSize))
            {
                Console.Error.WriteLine("Unable to unpack credential: " + GetLastErrorMessage());
                return false;
            }
            userName = userNameBuffer.ToString();
            password = passwordBuffer.ToString();
            return true;
        }

        private static Tuple<int, IntPtr> PackUserNameBuffer(string userName)
        {
            if (String.IsNullOrWhiteSpace(userName))
            {
                return Tuple.Create(0, IntPtr.Zero);
            }
            IntPtr buf = IntPtr.Zero;
            int size = 0;
            
            // First, calculate size. (buf == IntPtr.Zero)
            var result = NativeMethods.CredPackAuthenticationBuffer(
                dwFlags: 4, // CRED_PACK_GENERIC_CREDENTIALS
                pszUserName: userName,
                pszPassword: String.Empty,
                pPackedCredentials: buf,
                pcbPackedCredentials: ref size);
            Debug.Assert(!result);
            if (Marshal.GetLastWin32Error() != 122)
            {
                Console.Error.WriteLine("Unable to calculate size of packed authentication buffer: " + GetLastErrorMessage());
                return null;
            }

            buf = Marshal.AllocHGlobal(size);
            if (!NativeMethods.CredPackAuthenticationBuffer(
                dwFlags: 4, // CRED_PACK_GENERIC_CREDENTIALS
                pszUserName: userName,
                pszPassword: String.Empty,
                pPackedCredentials: buf,
                pcbPackedCredentials: ref size))
            {
                Console.Error.WriteLine("Unable to pack incoming username: " + GetLastErrorMessage());
                return null;
            }
            return Tuple.Create(size, buf);
        }

        static IEnumerable<Tuple<string, string>> StoreCommand(IDictionary<string, string> args)
        {
            // Build the URL
            Uri url = ExtractUrl(args);
            string userName = args.GetOrDefault("username", String.Empty);
            string password = args.GetOrDefault("password", String.Empty);

            bool abort = false;
            if(abort |= String.IsNullOrEmpty(userName)) {
                Console.Error.WriteLine("username parameter must be provided");
            }
            if(abort |= String.IsNullOrEmpty(password)) {
                Console.Error.WriteLine("password parameter must be provided");
            }
            if (!abort)
            {
                string target = GetTargetName(url);
                IntPtr passwordPtr = Marshal.StringToBSTR(password);
                NativeMethods.CREDENTIAL cred = new NativeMethods.CREDENTIAL()
                {
                    type = 0x01, // Generic
                    targetName = target,
                    credentialBlob = Marshal.StringToCoTaskMemUni(password),
                    persist = 0x03, // Enterprise (roaming)
                    attributeCount = 0,
                    userName = userName
                };
                cred.credentialBlobSize = Encoding.Unicode.GetByteCount(password);
                if (!NativeMethods.CredWrite(ref cred, 0))
                {
                    Console.Error.WriteLine(
                        "Failed to write credential: " +
                        GetLastErrorMessage());
                }   
            }
            return Enumerable.Empty<Tuple<string, string>>();
        }

        static IEnumerable<Tuple<string, string>> EraseCommand(IDictionary<string, string> args)
        {
            Uri url = ExtractUrl(args);
            if (!NativeMethods.CredDelete(GetTargetName(url), NativeMethods.CRED_TYPE.GENERIC, 0))
            {
                Console.Error.WriteLine(
                    "Failed to erase credential: " +
                    GetLastErrorMessage());
            }
            yield break;
        }

        private static string GetLastErrorMessage()
        {
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        private static Uri ExtractUrl(IDictionary<string, string> args)
        {
            Uri url = new UriBuilder()
            {
                Scheme = args.GetOrDefault("protocol", "https"),
                Host = args.GetOrDefault("host", "no-host.git"),
                Path = args.GetOrDefault("path", "/")
            }.Uri;
            return url;
        }

        private static string GetTargetName(Uri url)
        {
            return "git:" + url.AbsoluteUri;
        }
    }
}
