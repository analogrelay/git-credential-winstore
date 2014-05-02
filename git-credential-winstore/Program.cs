using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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

            // Check debugging environment variables
            string flags = Environment.GetEnvironmentVariable("GIT_CRED_STORE_FLAGS");
            if (!String.IsNullOrEmpty(flags))
            {
                HashSet<string> splat = new HashSet<string>(flags.Split(','), StringComparer.OrdinalIgnoreCase);
                if (splat.Contains("trace"))
                {
                    // DO NOT set useErrorStream to false. We don't want to pollute stdout with tracing
                    // Because Git uses that pipe to communicate with us.
                    Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));
                }
                if (splat.Contains("debug"))
                {
                    Debugger.Launch();
                }
            }

            var arguments = Arguments.Parse(ref args);

            if (arguments.Help)
            {
                WriteUsage();
                return;
            }

            if (args.Length == 0 || arguments.HasInstallParameter)
            {
                Trace.TraceInformation("Entering Install Mode");
                if (arguments.SilentMode)
                {
                    Console.Out.WriteLine("Silently Installing...");
                }

                InstallTheApp(arguments.GitPath, silent: arguments.SilentMode, installPath: arguments.InstallPath);
                return;
            }

            // Parse command.
            var cmd = args[0];

            var parameters = ReadGitParameters();

            if (cmd == "debug")
            {
                cmd = parameters["cmd"];
                parameters.Remove("cmd");
            }

            Func<IDictionary<string, string>, IEnumerable<Tuple<string, string>>> command = null;

            if (!_commands.TryGetValue(cmd, out command))
            {
                WriteUsage();
                return;
            }
            Trace.TraceInformation("Executing command: '{0}'", cmd);

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
                Console.Error.WriteLine("Launched with args: {0}", String.Join(",", args));
                Debugger.Launch();
                args = args.Skip(1).ToArray();
            }
        }

        private static void WriteGitParameters(IDictionary<string, string> response)
        {
            foreach (var pair in response)
            {
                TraceParameter("To Git", pair.Key, pair.Value);
                Console.Write("{0}={1}\n", pair.Key, pair.Value);
            }
        }

        private static IDictionary<string, string> ReadGitParameters()
        {
            var values = new Dictionary<string, string>();

            string line;
            while (!String.IsNullOrWhiteSpace((line = Console.ReadLine())))
            {
                var pair = line.Split(new[] { '=' }, 2);

                if (pair.Length == 2)
                {
                    values[pair[0]] = pair[1];
                    TraceParameter("From Git", pair[0], pair[1]);
                }
            }

            return values;
        }

        private static void TraceParameter(string prefix, string key, string value)
        {
            if (Trace.Listeners.Count > 0)
            {
                string traceValue = value;
                if (String.Equals(key, "password", StringComparison.OrdinalIgnoreCase))
                {
                    traceValue = "****";
                }
                Trace.TraceInformation("{2}: {0} = {1}", key, traceValue, prefix);
            }
        }

        private static void WriteUsage()
        {
            Console.Error.WriteLine("Git Credential Storage tool for Windows");
            Console.Error.WriteLine(" Brought to you by the Git Credential WinStore contributors");
            Console.Error.WriteLine(" https://gitcredentialstore.codeplex.com/");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: ");
            Console.Error.WriteLine("  git-credential-winstore.exe [-s] [-i <path>] [-t <path>]");
            Console.Error.WriteLine("  git-credential-winstore.exe -h");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  -s          Install silently (with no prompts or dialogs)");
            Console.Error.WriteLine("  -i <path>   Specifies the path to 'git.exe'");
            Console.Error.WriteLine("  -t <path>   Specifies the path in which to install this helper");
            Console.Error.WriteLine("  -h or -?    Display this help message");
        }

        private static void InstallTheApp(string pathToGit, bool silent, string installPath)
        {
            if (!silent)
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
            }
            Trace.TraceInformation("Using Git Path: {0}", pathToGit);

            if (String.IsNullOrEmpty(pathToGit) || !File.Exists(pathToGit))
            {
                Console.Error.WriteLine(@"Could not find Git!");
                Console.Error.WriteLine(@"Ensure that 'git.exe' is in a path listed in your PATH environment variable. Or specify the exact path to git using the '-i' parameter:");
                Console.Error.WriteLine(@" git-credential-winstore -i C:\Path\To\Git.exe");
                return;
            }

            if (String.IsNullOrEmpty(installPath))
            {
                installPath = @"%AppData%\GitCredStore";
            }

            DirectoryInfo target;
            try
            {
                target = new DirectoryInfo(Environment.ExpandEnvironmentVariables(installPath));
            }
            catch (Exception)
            {
                Console.Error.WriteLine(@"It looks like the value ""{0}"" is not a valid path.", installPath);
                Console.Error.WriteLine(@"Please check the -t argument and try again.");
                return;
            }

            if (!target.Exists)
            {
                target.Create();
            }

            var dest = new FileInfo(Environment.ExpandEnvironmentVariables(String.Concat(installPath, @"\git-credential-winstore.exe")));
            if (dest.Exists)
            {
                Trace.TraceInformation("Found existing installation. Deleting");
                dest.Delete();
            }

            File.Copy(Assembly.GetExecutingAssembly().Location, dest.FullName, true);

            string args = String.Format("config --global credential.helper \"!'{0}'\"", dest.FullName);
            Trace.TraceInformation("Execing: git {0}", args);
            Process.Start(pathToGit, String.Format("config --global credential.helper \"!'{0}'\"", dest.FullName));
        }

        static IEnumerable<Tuple<string, string>> GetCommand(IDictionary<string, string> args)
        {
            // Build the URL
            var url = ExtractUrl(args);
            if (url == null)
            {
                yield break;
            }

            string userName = args.GetOrDefault("username", null);
            string password = null;
            Trace.TraceInformation("Looking up credential for '{0}' on {1}", userName, url);

            var credPtr = IntPtr.Zero;

            try
            {
                // Check for a credential
                var target = GetTargetName(url);
                Trace.TraceInformation("Credential Name: {0}", target);

                if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE.GENERIC, 0, out credPtr))
                {
                    Trace.TraceInformation("No credential found.");
                    // Don't have a credential for this user. Are we on XP? If so, sorry no dice.
                    if (OnXP())
                    {
                        Trace.TraceInformation("Sorry XP user, we don't support you :(");
                        // Users will get a Git prompt for user name and password. We'll still store them.
                        yield break;
                    }
                    Trace.TraceInformation("Prompting for creds.");

                    credPtr = IntPtr.Zero;

                    // If we have a username, pack an input authentication buffer
                    Tuple<int, IntPtr> inputBuffer = null; ;
                    IntPtr outputBuffer = IntPtr.Zero;
                    int outputBufferSize = 0;
                    try
                    {
                        inputBuffer = PackUserNameBuffer(userName);
                        if (inputBuffer == null)
                        {
                            yield break;
                        }

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
                    Trace.TraceInformation("Found a credential!");

                    // Decode the credential
                    NativeMethods.CREDENTIAL cred = (NativeMethods.CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(NativeMethods.CREDENTIAL));
                    userName = cred.userName;
                    password = Marshal.PtrToStringUni(cred.credentialBlob, cred.credentialBlobSize / 2); // blob size is in bytes but PtrToStringUni wants count of Unicode characters.
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

            var buf = IntPtr.Zero;
            var size = 0;

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
                Console.Error.WriteLine("Unable to calculate size of packed authentication buffer: {0}", GetLastErrorMessage());
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
                Console.Error.WriteLine("Unable to pack incoming username: {0}", GetLastErrorMessage());
                return null;
            }

            return Tuple.Create(size, buf);
        }

        static IEnumerable<Tuple<string, string>> StoreCommand(IDictionary<string, string> args)
        {
            // Build the URL
            var url = ExtractUrl(args);
            var userName = args.GetOrDefault("username", String.Empty);
            var password = args.GetOrDefault("password", String.Empty);

            var abort = false;
            if (abort |= String.IsNullOrEmpty(userName))
            {
                Console.Error.WriteLine("username parameter must be provided");
            }

            if (abort |= String.IsNullOrEmpty(password))
            {
                Console.Error.WriteLine("password parameter must be provided");
            }

            if (!abort)
            {
                var target = GetTargetName(url);
                Trace.TraceInformation("Storing credentials for '{0}' on {1} in {2}", userName, url, target);

                var cred = new NativeMethods.CREDENTIAL()
                {
                    type = 0x01, // Generic
                    targetName = target,
                    credentialBlob = Marshal.StringToCoTaskMemUni(password),
                    credentialBlobSize = Encoding.Unicode.GetByteCount(password),
                    persist = 0x03, // Enterprise (roaming)
                    attributeCount = 0,
                    userName = userName
                };

                if (!NativeMethods.CredWrite(ref cred, 0))
                {
                    Console.Error.WriteLine("Failed to write credential: {0}", GetLastErrorMessage());
                }
            }

            return Enumerable.Empty<Tuple<string, string>>();
        }

        static IEnumerable<Tuple<string, string>> EraseCommand(IDictionary<string, string> args)
        {
            var url = ExtractUrl(args);

            Trace.TraceInformation("Erasing credentials for '{0}'", url);
            if (!NativeMethods.CredDelete(GetTargetName(url), NativeMethods.CRED_TYPE.GENERIC, 0))
            {
                var errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 1168) // ERROR_NOT_FOUND ==> The credential doesn't exist, so no need to print the failure.
                {
                    Console.Error.WriteLine("Failed to erase credential: {0}", GetErrorMessage(errorCode));
                }
                else
                {
                    Trace.TraceInformation("No credentials to erase!");
                }
            }

            yield break;
        }

        private static string GetLastErrorMessage()
        {
            return GetErrorMessage(Marshal.GetLastWin32Error());
        }

        private static string GetErrorMessage(int lastError)
        {
            return new Win32Exception(lastError).Message;
        }

        private static Uri ExtractUrl(IDictionary<string, string> args)
        {
            // Manually build a string (tried a UriBuilder, but Git gives us credentials and port numbers so it's difficult)
            var scheme = args.GetOrDefault("protocol", "https");
            var host = args.GetOrDefault("host", "no-host.git");
            var path = args.GetOrDefault("path", String.Empty);

            var candidateUrl = String.Format("{0}://{1}/{2}", scheme, host, path);

            Uri url = null;
            if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out url))
            {
                Console.Error.WriteLine("Failed to parse url: {0}", candidateUrl);
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
