# Windows Credential Store for Git
This application is a small helper app designed to follow the git credentials API as defined by the [Git Documentation](https://github.com/gitster/git-htmldocs/blob/master/technical/api-credentials.txt).

## Installation

1. Download the [git-credential-winstore.exe](http://blob.andrewnurse.net/gitcredentialwinstore/git-credential-winstore.exe) application
2. Run it! If you have GIT in your PATH, it should just work. If you don't, run 'git-credential-winstore -i C:\Path\To\Git.exe'.

## FAQs

### Why doesn't it work?
Make sure you're running the latest version of msysgit. The credential API is fairly new. I've tested this on version 1.7.10.

### Why doesn't it install?
Make sure you're running with GIT on your PATH (perhaps by running from the GIT bash shell)

### Where are you storing my credentials?
This app just uses the existing Windows Credential Store to hold your credentials. You can see the stored credentials by going to Control Panel > User Accounts > Credential Manager and choosing "Windows Credentials". The entries starting "git:" are from git-credential-winstore.

### I have another question?
That's not a question.

### But I actually have another question...
Ok, you can email me at [andrew@andrewnurse.net](mailto:andrew@andrewnurse.net). Or post an issue in the Issues section.

## Special Thanks
* [Paul Betts](http://paulbetts.org/) for creating our handy-dandy installer.
* [Matt Wrock](https://github.com/mwrock) for adding a silent option to the installer.
* [mattn](https://github.com/mattn) for adding support for Windows XP.
* [Marc Brooks](https://github.com/IDisposable) for some readme tweaks.

## License
This code is Copyright Andrew Nurse and other contributors 2012. You are hereby granted a license to use the software and code under the terms of the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0.html)
