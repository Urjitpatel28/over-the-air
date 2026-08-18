<#
.SYNOPSIS
    Builds OverTheAirSetup.exe (and the OverTheAir.msi inside it) from a Release build.

.DESCRIPTION
    Two passes:

        1. Package.wxs  -> OverTheAir.msi          the payload, no UI of its own
        2. Bundle.wxs   -> OverTheAirSetup.exe     the bootstrapper, with the .msi embedded

    Needs WiX, pinned below v6 (v6+ requires a paid EULA that CI cannot accept):

        dotnet tool install --global wix --version 5.0.2

    Build the solution first - this script packages OverTheAir's output folder as it stands:

        dotnet build OverTheAir.sln -c Release
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputPath,

    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$binDir = Join-Path $repoRoot "OverTheAir\bin\$Configuration\net8.0-windows"
$packageSource = Join-Path $repoRoot "OverTheAir.Setup\Package.wxs"
$bundleSource = Join-Path $repoRoot "OverTheAir.Setup\Bundle.wxs"
$iconFile = Join-Path $repoRoot "OverTheAir\Assets\app.ico"
$assetsDir = Join-Path $repoRoot "OverTheAir.Setup\Assets"

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "OverTheAir.Setup\bin\OverTheAirSetup.exe"
}

$msiPath = Join-Path (Split-Path -Parent $OutputPath) "OverTheAir.msi"

if (-not $Version) {
    $csproj = Join-Path $repoRoot "OverTheAir\OverTheAir.csproj"
    $match = Select-String -Path $csproj -Pattern '<Version>([\d\.]+)</Version>'
    if (-not $match) {
        throw "Could not read Version from $csproj to default -Version. Pass -Version explicitly."
    }
    $Version = $match.Matches[0].Groups[1].Value
}

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "The WiX toolset is not on PATH. Install it once with: dotnet tool install --global wix --version 5.0.2"
}

& wix extension add WixToolset.BootstrapperApplications.wixext/5.0.2
if ($LASTEXITCODE -ne 0) {
    throw "wix extension add failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $binDir)) {
    throw "No $Configuration build to package at $binDir. Build the solution first."
}

$exe = Join-Path $binDir "OverTheAir.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "OverTheAir.exe is missing from $binDir."
}

if (-not (Test-Path -LiteralPath $iconFile)) {
    throw "The application icon is missing from $iconFile."
}

$licenseFile = Join-Path $assetsDir "License.rtf"
if (-not (Test-Path -LiteralPath $licenseFile)) {
    throw "License.rtf is missing from $assetsDir."
}

$outputFolder = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputFolder)) {
    New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
}

Write-Output "Packaging $binDir (version $Version)"

& wix build $packageSource `
    -d "BinDir=$binDir" -d "IconFile=$iconFile" -d "ProductVersion=$Version" `
    -o $msiPath

if ($LASTEXITCODE -ne 0) {
    throw "wix build failed for the .msi with exit code $LASTEXITCODE."
}

Write-Output "Wrote $msiPath"

& wix build $bundleSource -ext WixToolset.BootstrapperApplications.wixext `
    -d "MsiPath=$msiPath" -d "IconFile=$iconFile" -d "ProductVersion=$Version" -d "AssetsDir=$assetsDir" `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "wix build failed for the bundle with exit code $LASTEXITCODE."
}

Write-Output "Wrote $OutputPath"
