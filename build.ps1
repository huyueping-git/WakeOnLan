#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild not found. Install Visual Studio 2022 with .NET desktop workload."
}

$nuget = Join-Path $root "tools\nuget.exe"
if (-not (Test-Path $nuget)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $nuget) | Out-Null
    Write-Host "Downloading nuget.exe ..."
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget
}

Write-Host "Restoring NuGet packages ..."
& $nuget restore (Join-Path $root "WakeOnLanClient.sln") -NonInteractive
if ($LASTEXITCODE -ne 0) {
    throw "NuGet restore failed."
}

$appProject = Join-Path $root "src\WakeOnLanClient\WakeOnLanClient.csproj"
Write-Host "Building application ($Configuration) ..."
& $msbuild $appProject /t:Rebuild /p:Configuration=$Configuration /p:Platform=AnyCPU /m
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed."
}

$setupProject = Join-Path $root "installer\WakeOnLanClient.Setup\WakeOnLanClient.Setup.wixproj"
Write-Host "Building MSI ($Configuration|x86) ..."
& $msbuild $setupProject /t:Rebuild /p:Configuration=$Configuration /p:Platform=x86 /restore /m
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed."
}

$msiPath = Join-Path $root "installer\WakeOnLanClient.Setup\bin\x86\$Configuration\WakeOnLanClient.msi"
if (-not (Test-Path $msiPath)) {
    $msiPath = Get-ChildItem -Path (Join-Path $root "installer\WakeOnLanClient.Setup\bin") -Filter "*.msi" -Recurse | Select-Object -First 1 -ExpandProperty FullName
}

Write-Host ("MSI output: " + $msiPath)
Write-Host "Done."
