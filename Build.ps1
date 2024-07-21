param
(
    [String]$CommitID,
    [String]$Configuration = "Release",
    [Switch]$Clean,
    [Switch]$Help,
    [Switch]$Test,
    [Switch]$UseFullCommitID,
    [String]$Version
)

$work = $pwd

$csprojPaths  = @("Process.Extensions\Process.Extensions.csproj", "Process.Extensions.Tests\Process.Extensions.Tests.csproj")
$vcxprojPaths = @("Process.Extensions.Tests.Client\Process.Extensions.Tests.Client.vcxproj", "Process.Extensions.Tests.Client.DllExport\Process.Extensions.Tests.Client.DllExport.vcxproj")
$buildPaths   = @("Process.Extensions\bin\")

$patchVersion = ".github\workflows\Patch-Version.ps1"
$vswhere      = "Tools\vswhere.exe"
$dependencies = @($patchVersion, $vswhere)

if ($Help)
{
    echo "Process.Extensions Build Script"
    echo ""
    echo "Usage:"
    echo "-CommitID [id] - set the commit ID to use from GitHub for the version number."
    echo "-Configuration [name] - build Process.Extensions with a specific configuration."
    echo "-Clean - cleans the solution before building Process.Extensions."
    echo "-Help - display help."
    echo "-Test - run tests after building Process.Extensions."
    echo "-UseFullCommitID - use the full 40 character commit ID for the version number."
    echo "-Version [major].[minor].[revision] - set the version number for this build of Process.Extensions."
    exit
}

# Check for dependencies.
foreach ($dependency in $dependencies)
{
    $resolved = [System.IO.Path]::Combine($work, $dependency)

    if (![System.IO.File]::Exists($resolved))
    {
        echo "Failed to locate build dependency: ${dependency}"
        exit -1
    }
}

# Check if the .NET SDK is installed.
if (!(Get-Command -Name dotnet -ErrorAction SilentlyContinue))
{
    echo ".NET SDK is required to build Process.Extensions."
    echo "You can install the required .NET SDK for Windows from here: https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
    exit -1
}

$vs      = & "${vswhere}" -nologo -latest -prerelease -property installationPath
$msbuild = [System.IO.Path]::Combine($vs, "MSBuild\Current\Bin\MSBuild.exe")

if (![System.IO.File]::Exists($msbuild))
{
    echo "Failed to locate MSBuild."
    echo "Ensure that you have Visual Studio installed with the ""Desktop development with C++"" workload."
    exit -1
}

function PatchVersionInformation([String]$commitID, [Boolean]$useFullCommitID, [String]$version)
{
    # Patch the version number for all projects.
    if (![System.String]::IsNullOrEmpty($version))
    {
        foreach ($project in [System.IO.Directory]::EnumerateFiles($work, "*.csproj", [System.IO.SearchOption]::AllDirectories))
        {
            & "${patchVersion}" -CommitID $commitID -ProjectPath "${project}" -Version $version
        }
    }
    else
    {
        PatchVersionInformation "" $false "1.0.0"
    }
}

foreach ($csprojPath in $csprojPaths)
{
    if ($Clean)
    {
        dotnet clean "${csprojPath}"
        echo ""
    }

    # Patch version number before building.
    PatchVersionInformation $CommitID $UseFullCommitID $Version

    dotnet build "${csprojPath}" /p:Configuration="${Configuration}"
    echo ""

    # Restore default version number.
    PatchVersionInformation "" $false "1.0.0"
}

foreach ($vcxprojPath in $vcxprojPaths)
{
    if ($Clean)
    {
        & "${msbuild}" "${vcxprojPath}" /t:Clean
        echo ""
    }
    
    # Build test projects for both platforms to run tests native and on WoW64.
    & "${msbuild}" "${vcxprojPath}" /p:Configuration="${Configuration}" /p:Platform="Win32"
    echo ""
    & "${msbuild}" "${vcxprojPath}" /p:Configuration="${Configuration}" /p:Platform="x64"
    echo ""
}

if (!$Test)
{
    exit
}

$testProgramDir = [System.IO.Path]::Combine($work, "Process.Extensions.Tests\bin\${Configuration}\net8.0")
$testProgram    = [System.IO.Path]::Combine($testProgramDir, "Process.Extensions.Tests.exe")

if (![System.IO.File]::Exists($testProgram))
{
    echo "Failed to locate test program..."
    echo "It may have failed to build and cannot be tested."
    exit -1
}

$process = Start-Process -FilePath "${testProgram}" -WorkingDirectory "${testProgramDir}" -NoNewWindow -PassThru -Wait
$exitCode = $process.ExitCode

echo ""

if ($exitCode -eq 0)
{
    echo "Tests completed successfully."
    exit 0
}

echo "Tests failed with exit code: ${exitCode}"
exit $exitCode