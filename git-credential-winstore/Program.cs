using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

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
            TryLaunchDebugger(ref args);

            IDictionary<string, object> installParameters = ReadInstallParameters(ref args);
            bool hasInstallParameter = args.Any(arg => arg == "-i" || arg == "-s");

            // Parse command
            Func<IDictionary<string, string>, IEnumerable<Tuple<string, string>>> command = null;
            string cmd;

            if (args.Length == 0 || hasInstallParameter)
            {
                string gitPath = installParameters["gitPath"] as string;
                bool silent = (bool) installParameters["silent"];

                if (silent)
                {
                    Console.Out.WriteLine("Silently Installing...");
                }

                InstallTheApp(gitPath, silent: silent);
                return;
            }

            cmd = args[0];

            IDictionary<string, string> parameters = ReadGitParameters();

            if (cmd == "debug")
            {
                cmd = parameters["cmd"];
                parameters.Remove("cmd");
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

        // Conditional methods can't return anything, so we use a ref arg... :S
        [Conditional("DEBUG")]
        private static void TryLaunchDebugger(ref string[] args)
        {
            if (args.Length > 0 && args[0] == "-d")
            {
                Console.Error.WriteLine("Launching debugger...");
                Debugger.Launch();
                args = args.Skip(1).ToArray();
            }
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
                // Find the first '='
                int equalsIndex = line.IndexOf('=');
                if (equalsIndex > -1)
                {
                    string key = line.Substring(0, equalsIndex);
                    string value = line.Substring(equalsIndex + 1);
                    values[key] = value;
                }
            }
            return values;
        }
        private static IDictionary<string, object> ReadInstallParameters(ref string[] args)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            int length = args.Length;

            string gitPath = String.Empty;
            bool silentMode = false;

            if (length > 0)
            {
                for (int i = 0; i < length; i++)
                {
                    switch (args[i])
                    {
                        case "-s":
                            silentMode = true;
                            break;
                        case "-i":
                            try
                            {
                                gitPath = args[++i];
                            }
                            catch (Exception)
                            {
                                gitPath = String.Empty;
                            }
                            break;
                    }
                }

            }

            values["gitPath"] = gitPath;
            values["silent"] = silentMode;

            return values;
        }

        private static void WriteUsage()
        {
            Console.Error.WriteLine("If you see this. git-credential-winstore is correctly installed!");
            Console.Error.WriteLine("This application is designed to be used by git as a credential helper and should not be invoked separately");
            Console.Error.WriteLine("See the following link for more info: http://www.manpagez.com/man/1/git-credential-cache/");
        }

        private static void InstallTheApp(string pathToGit, bool silent)
        {
            if(!silent)
            {
                if (MessageBox.Show("Do you want to install git-credential-winstore to prompt for passwords?",
                    "Installing git-credential-winstore", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return;
                }
            }

            // Look for git
            if (String.IsNullOrEmpty(pathToGit))
            {
                string[] paths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
                pathToGit = paths.Select(path => Path.Combine(path, "git.exe"))
                                 .Where(File.Exists).FirstOrDefault();
                if (String.IsNullOrEmpty(pathToGit))
                {
                    Console.WriteLine(@"Could not find Git in your PATH environment variable.");
                    Console.WriteLine(@"You can specify the exact path to git by running: ");
                    Console.WriteLine(@" git-credential-winstore -i C:\Path\To\Git.exe");
                    Console.WriteLine(@"Press ENTER to exit.");
                    Console.ReadLine();
                    return;
                }
            }

            var target = new DirectoryInfo(Environment.ExpandEnvironmentVariables(@"%AppData%\GitCredStore"));
            if (!target.Exists)
            {
                target.Create();
            }

            var dest = new FileInfo(Environment.ExpandEnvironmentVariables(@"%AppData%\GitCredStore\git-credential-winstore.exe"));
            if (dest.Exists)
            {
                dest.Delete();
            }
            File.Copy(Assembly.GetExecutingAssembly().Location, dest.FullName, true);

            Process.Start(pathToGit, string.Format("config --global credential.helper \"!'{0}'\"", dest.FullName));
        }

        static IEnumerable<Tuple<string, string>> GetCommand(IDictionary<string, string> args)
        {
            // Build the URL
            Uri url = ExtractUrl(args);
            if (url == null)
            {
                yield break;
            }

            string userName = args.GetOrDefault("username", null);
            string password = null;
            
            IntPtr credPtr = IntPtr.Zero;
            try
            {
                // Check for a credential
                string target = GetTargetName(url);
                if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE.GENERIC, 0, out credPtr))
                {
                    // Don't have a credential for this user. Are we on XP? If so, sorry no dice.
                    if (OnXP())
                    {
                        // Users will get a Git prompt for user name and password. We'll still store them.
                        yield break;
                    }
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
                            pszMessageText = "Enter your credentials for: " + GetHost(url)
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

        private static bool OnXP()
        {
            // I know, version detection a Bad Thing(TM). But "feature detection" is supposed to be done
            // via checking DLL exports, which is gross in C#...
            return Environment.OSVersion.Version.Major == 5;
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
            // Manually build a string (tried a UriBuilder, but Git gives us credentials and port numbers so it's difficult)
            string scheme = args.GetOrDefault("protocol", "https");
            string host = args.GetOrDefault("host", "no-host.git");
            string path = args.GetOrDefault("path", "/");

            string candidateUrl = String.Format("{0}://{1}{2}", scheme, host, path);
            Uri url;
            if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out url))
            {
                Console.Error.WriteLine("Failed to parse url: {0}", candidateUrl);
                return null;
            }
            return url;
        }

        private static string GetTargetName(Uri url)
        {
            return "git:" + GetHost(url);
        }

        private static string GetHost(Uri url)
        {
            return url.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
        }
    }
}
