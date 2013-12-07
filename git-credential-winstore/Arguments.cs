using System;

namespace Git.Credential.WinStore
{
    class Arguments
    {

        private String _gitPath = String.Empty;
        public String GitPath
        {
            get { return _gitPath; }
            set { _gitPath = value; }
        }

        private bool _silentMode = false;
        public bool SilentMode
        {
            get { return _silentMode; }
            set { _silentMode = value; }
        }

        private String _installPath = String.Empty;
        public String InstallPath
        {
            get { return _installPath; }
            set { _installPath = value; }
        }

    }
}
