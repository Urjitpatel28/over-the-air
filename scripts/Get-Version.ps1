<#
.SYNOPSIS
    Turns a git ref like "refs/tags/v1.2.3" into the two version strings CI needs.

.DESCRIPTION
    release.yml calls this on a tag push. It validates the tag is a plain three-part semver (no
    prerelease/build suffix - AssemblyVersion and MSI ProductVersion don't have anywhere to put one)
    and prints both required forms:

        AssemblyVersion  1.2.3.0   (four parts - .NET pads the missing revision with 0)
        ProductVersion   1.2.3     (three parts - Windows Installer ignores everything past the third)

.EXAMPLE
    .\Get-Version.ps1 -Ref "refs/tags/v1.2.3"
    .\Get-Version.ps1 -Ref "v0.0.1"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Ref
)

$ErrorActionPreference = "Stop"

$tag = $Ref -replace '^refs/tags/', ''
$tag = $tag -replace '^v', ''

if ($tag -notmatch '^\d+\.\d+\.\d+$') {
    throw "Tag '$Ref' is not a plain vX.Y.Z semver tag (got '$tag')."
}

[PSCustomObject]@{
    ProductVersion  = $tag
    AssemblyVersion = "$tag.0"
}
