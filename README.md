# Windows Credential Store for Git
This application is a small helper app designed to follow the git credentials API as defined by the [Git Documentation](https://github.com/gitster/git-htmldocs/blob/master/technical/api-credentials.txt).
## Installation

1. Download the git-credential-winstore.exe file from the downloads section
2. Run it (make sure you run it from a command prompt where git is in your path)

## FAQs

### Why doesn't it work?
Make sure you're running the latest version of msysgit. The credential API is fairly new. I've tested this on version 1.7.10.

### Where are you storing my credentials?
This app just uses the existing Windows Credential Store to hold your credentials. You can see the stored credentials by going to Control Panel > User Accounts > Credential Manager and choosing "Windows Credentials". The entries starting "git:" are from git-credential-winstore.

### I have another question?
That's not a question.

### But I actually have another question...
Ok, you can email me at [andrew@andrewnurse.net](mailto:andrew@andrewnurse.net).

## License
This code is Copyright Andrew Nurse 2012. You are hereby granted a license to use the software and code under the terms of the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0.html)