# Build the code

$slnName = "git-credential-winstore.sln"
$mypath = (Split-Path -Parent $MyInvocation.MyCommand.Path)
$reporoot = (Split-Path -Parent $mypath)
$sln = Join-Path $reporoot $slnName
if(!(Test-Path $sln)) {
    throw "Configuration error. Solution file not found: $sln."
}
$packagesDir = Join-Path $reporoot "packages"

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

# Get NuGet Version of built-in nuget
$ver = nuget help | where { $_ -match "^NuGet Version: (.*)$" } | foreach { $matches[1] }
if(!$ver) {
    throw "Unknown version of NuGet.exe found!"
}
$ver = New-Object System.Version $ver
if($ver -lt ([version]"2.7")) {
    throw "Incompatible version of NuGet.exe found. Update using 'nuget update -Self'. This project requires NuGet 2.7 or higher."
}

Write-Host "Using NuGet $ver found in $($nugetcmd.Definition)"

# Restore packages
Write-Host -ForegroundColor Green "Restoring NuGet Packages..."
&$nuget restore $sln -PackagesDirectory $packagesDir

# Build!
Write-Host -ForegroundColor Green "Building $slnName..."

$args = @($args)
$config = $args | where { $_.StartsWith("/p:Configuration") }
if(!$config) {
    $args += "/p:Configuration=Debug"
}
$platform = $args | where { $_.StartsWith("/p:Platform") }
if(!$platform) {
    $args += "/p:Platform=Any CPU"
}

msbuild $sln @args

if($config -and $config.EndsWith("Release"))
{
    if(!(Test-Path "$reporoot\bin")) {
        mkdir "$reporoot\bin" | Out-Null
    }

    cp "$reporoot\git-credential-winstore\bin\Release" "bin"
}