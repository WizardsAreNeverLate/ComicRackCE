param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Rebuild,
    [switch]$Test
)

$ErrorActionPreference = "Stop"
$solution = Join-Path $PSScriptRoot "ComicRack.sln"

# dotnet build cannot compile this repo's binary .resx resources, so use Visual Studio's MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) {
    Write-Error "vswhere.exe was not found at $vswhere. Install Visual Studio with the .NET desktop workload."
    exit 1
}
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (!$msbuild) {
    Write-Error "MSBuild was not found. Install Visual Studio with the .NET desktop workload."
    exit 1
}
Write-Output "MSBuild: $msbuild"

# Restore from nuget.org only, so a machine-wide NuGet config with unreachable sources cannot fail the build
$nugetConfig = Join-Path ([System.IO.Path]::GetTempPath()) "ComicRackCE.nuget.config"
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfig -Encoding UTF8

Write-Output "Restoring packages..."
& $msbuild $solution -t:Restore "-p:RestoreConfigFile=$nugetConfig" -nologo -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Package restore failed."
    exit 1
}

$target = if ($Rebuild) { "Rebuild" } else { "Build" }
Write-Output "Building ComicRack.sln ($Configuration, $target)..."
& $msbuild $solution "-t:$target" "-p:Configuration=$Configuration" "-p:Platform=Any CPU" -m -nologo -v:minimal "-clp:ErrorsOnly;Summary"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

$exe = Join-Path $PSScriptRoot "ComicRack\bin\$Configuration\net48\ComicRack.exe"
if (!(Test-Path $exe)) {
    Write-Error "The build reported success, but $exe does not exist."
    exit 1
}
Write-Output "Built $exe"

if ($Test) {
    $tests = Join-Path $PSScriptRoot "ComicRack.Plugins.Tests\bin\$Configuration\net48\ComicRack.Plugins.Tests.exe"
    Write-Output "Running $tests"
    & $tests
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed."
        exit 1
    }
}
