# Build first
$mypath = (Split-Path -Parent $MyInvocation.MyCommand.Path)
$reporoot = (Split-Path -Parent $mypath)

&"$mypath\build.ps1" /p:Configuration=Release

Write-Host -ForegroundColor Green "Assembling Package files..."
# Make a place to build the package
$pkgDir = Join-Path $reporoot ".packaging"
if(!(Test-Path $pkgDir)) {
    mkdir $pkgDir | Out-Null
}
if(!(Test-Path "$pkgDir\tools")) {
    mkdir "$pkgDir\tools" | Out-Null
}

# Slurp the version out
$ver = (cat "$reporoot\git-credential-winstore\Properties\AssemblyInfo.cs") | where { $_ -match "^\[assembly: AssemblyVersion\(`"(.*)`"\)\]$" } | foreach { $matches[1] }

# Copy files in
cp "$reporoot\git-credential-winstore\bin\Release\git-credential-winstore.exe" "$pkgDir\tools"
cp "$mypath\ChocolateyInstall.ps1" "$pkgDir\tools"

cp "$mypath\Git-Credential-WinStore.nuspec" "$pkgDir\Git-Credential-WinStore.nuspec"

# Check for NuGet
Write-Host -ForegroundColor Green "Searching for nuget.exe..."
$nuget = $null
$nugetcmd = Get-Command "nuget" -ErrorAction SilentlyContinue
if(!$nugetcmd) {
    $nuget = (Join-Path $mypath "nuget.exe")
    if(!(Test-Path $nuget)) {
        throw "NuGet.exe was not found in your path. You should have it in your PATH, it's awesome! Go get it from https://nuget.codeplex.com/releases/ !"
    }
} else {
    $nuget = $nugetcmd.Definition
}

if(!(Test-Path "$reporoot\bin")) {
    mkdir "$reporoot\bin" | Out-Null
}

&$nuget pack "$pkgDir\Git-Credential-WinStore.nuspec" -NoPackageAnalysis -OutputDirectory "$reporoot\bin" -Version $ver