using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.ComponentModel;

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
                Console.WriteLine("{0}={1}", pair.Key, pair.Value);
            }
            Console.WriteLine();
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
            Console.WriteLine("Figure it out yourself. But seriously, I just haven't written this yet. Sorry :(");
        }

        static IEnumerable<Tuple<string, string>> GetCommand(IDictionary<string, string> args)
        {
            // Build the URL
            Uri url = ExtractUrl(args);
            
            IntPtr credPtr = IntPtr.Zero;
            try
            {
                // Check for a credential
                string target = GetTargetName(url);
                if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE.GENERIC, 0, out credPtr))
                {
                    // Don't have a credential for this user.
                    yield break;
                }

                // Decode the credential
                NativeMethods.CREDENTIAL cred = (NativeMethods.CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(NativeMethods.CREDENTIAL));
                yield return Tuple.Create("username", cred.userName);
                yield return Tuple.Create("password", Marshal.PtrToStringBSTR(cred.credentialBlob));
            }
            finally
            {
                if (credPtr != IntPtr.Zero)
                {
                    NativeMethods.CredFree(credPtr);
                }
            }
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
