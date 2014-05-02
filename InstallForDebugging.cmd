@setlocal
@echo off
echo This command will install the credential helper from your local build in DEBUG mode.
echo You will get a DEBUG prompt for EVERY GIT CREDENTIAL REQUEST. DO NOT CONTINUE UNLESS YOU KNOW HOW TO REVERT IT!!
set /p continue=Continue? [y/N]: 
if /i "%continue%"=="y" goto install
echo Cancelled. Nothing has been changed.
exit 0

:install
git config --global credential.helper "!'%~dp0git-credential-winstore\bin\Debug\git-credential-winstore.exe' -d"
echo Installed.