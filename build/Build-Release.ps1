[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Repository,
    [ValidateSet('Release','Beta')][string]$Channel = 'Release'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot/Release-Settings.ps1"
$settings = Get-ReleaseSettings -Tag $Tag -Repository $Repository -Channel $Channel
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE) { throw "dotnet failed with exit code $LASTEXITCODE." }
}
$root = Split-Path $PSScriptRoot
$sourceCommit = git -C $root rev-parse --verify HEAD
if ($LASTEXITCODE) { throw 'Could not determine the release source commit.' }
$changes = git -C $root status --porcelain
if ($LASTEXITCODE -or $changes) { throw 'Build the release from a clean, committed checkout.' }
$localTag = git -C $root tag --list $Tag
if ($LASTEXITCODE) { throw 'Could not check the local release tag.' }
if ($localTag) {
    $tagCommit = git -C $root rev-parse --verify "refs/tags/${Tag}^{commit}"
    if ($LASTEXITCODE -or $tagCommit -ne $sourceCommit) { throw "$Tag points to a different commit." }
}
$solution = Join-Path $root 'UnphasedNina.slnx'
$project = Join-Path $root 'src/UnphasedNina/UnphasedNina.csproj'
Invoke-Dotnet @('restore', $solution)
Invoke-Dotnet @('build', $solution, '-c', 'Release', '--no-restore', "-p:Version=$($settings.Version)", "-p:SourceRevisionId=$sourceCommit", "-p:RepositoryUrl=https://github.com/$Repository")
Invoke-Dotnet @('test', $solution, '-c', 'Release', '--no-build', '--no-restore')
# Query the already evaluated project rather than guessing the DLL name from the display name.
$assemblyName = (& dotnet msbuild $project -getProperty:AssemblyName).Trim()
if ($LASTEXITCODE -or [string]::IsNullOrWhiteSpace($assemblyName)) { throw 'Could not read AssemblyName.' }
$url = "https://github.com/$Repository/releases/download/$Tag/$assemblyName.$($settings.Version).zip"
Invoke-Dotnet @('msbuild', $project, '-t:PackagePlugin', '-p:Configuration=Release', "-p:Version=$($settings.Version)", "-p:SourceRevisionId=$sourceCommit", "-p:RepositoryUrl=https://github.com/$Repository", "-p:PluginInstallerUrl=$url", "-p:PluginChannel=$Channel")
$currentCommit = git -C $root rev-parse --verify HEAD
if ($LASTEXITCODE -or $currentCommit -ne $sourceCommit) { throw 'HEAD changed during the build.' }
$changes = git -C $root status --porcelain
if ($LASTEXITCODE -or $changes) { throw 'The checkout changed during the build.' }
& "$PSScriptRoot/Verify-ReleaseArtifacts.ps1" -Directory (Join-Path $root 'artifacts/release') -Tag $Tag -Repository $Repository -AssemblyName $assemblyName -SourceCommit $sourceCommit -Channel $Channel
Write-Host "Validated release $Tag ($Channel). Assets: $root/artifacts/release"
