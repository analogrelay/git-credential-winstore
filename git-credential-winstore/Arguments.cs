using System;

namespace Git.Credential.WinStore
{
    class Arguments
    {
        public string GitPath { get; set; }

        public bool SilentMode { get; set; }

        public string InstallPath { get; set; }

        public bool Help { get; set; }

        public bool HasInstallParameter { get { return !String.IsNullOrEmpty(GitPath) || !String.IsNullOrEmpty(InstallPath) || SilentMode; } }

        public static Arguments Parse(ref string[] args)
        {
            var arguments = new Arguments();
            var length = args.Length;

            if (length > 0)
            {
                for (int i = 0; i < length; i++)
                {
                    switch (args[i])
                    {
                        case "-h":
                        case "-?":
                            arguments.Help = true;
                            break;

                        case "-s":
                            arguments.SilentMode = true;
                            break;

                        case "-i":
                            if (args.Length > i + 1)
                            {
                                arguments.GitPath = args[++i];
                            }
                            else
                            {
                                throw new Exception("Expected a value after '-i' switch");
                            }
                            break;

                        case "-t":
                            if (args.Length > i + 1)
                            {
                                arguments.InstallPath = args[++i];
                            }
                            else
                            {
                                throw new Exception("Expected a value after '-t' switch");
                            }
                            break;
                    }
                }
            }

            return arguments;
        }
    }
}
