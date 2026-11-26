[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Repository,
    [ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$AssemblyName = 'UnphasedNina',
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceCommit,
    [ValidateSet('Release','Beta')][string]$Channel = 'Release'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Release-Settings.ps1"
$settings = Get-ReleaseSettings -Tag $Tag -Repository $Repository -Channel $Channel
$archiveName = "$AssemblyName.$($settings.Version).zip"
$archivePath = Join-Path $Directory $archiveName
$manifestPath = Join-Path $Directory "$AssemblyName.$($settings.Version).manifest.json"
$json = Get-Content -LiteralPath $manifestPath -Raw
if (!(Test-Json -Json $json -SchemaFile "$PSScriptRoot/vendor/manifest.schema.json")) { throw 'Invalid manifest.' }
$manifest = $json | ConvertFrom-Json
$version = "$($manifest.Version.Major).$($manifest.Version.Minor).$($manifest.Version.Patch).$($manifest.Version.Build)"
if ($version -ne $settings.Version) { throw 'Manifest version differs from tag.' }
$repositoryUrl = "https://github.com/$Repository"
$expectedUrl = "$repositoryUrl/releases/download/$Tag/$archiveName"
if ($manifest.Repository -ne $repositoryUrl -or $manifest.Channel -ne $Channel -or $manifest.License -ne 'MPL-2.0') { throw 'Unexpected release metadata.' }
if ($manifest.Name -eq 'My NINA Plugin' -or $manifest.Author -eq 'Plugin Author') { throw 'Set your plugin display name and author in Plugin.props before releasing.' }
if ($manifest.Installer.URL -ne $expectedUrl -or $manifest.Installer.Type -ne 'ARCHIVE' -or $manifest.Installer.ChecksumType -ne 'SHA256') { throw 'Unexpected installer metadata.' }
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $manifest.Installer.Checksum) { throw 'Archive checksum differs from manifest.' }

$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $archivePath).Path)
try {
    $expectedFiles = @("$AssemblyName.dll", "$AssemblyName.pdb", 'UnphasedCore.dll', 'astronomy.dll', 'LICENSE.txt', 'THIRD-PARTY-NOTICES.txt')
    if ($archive.Entries.Count -ne $expectedFiles.Count -or
        (Compare-Object -ReferenceObject $expectedFiles -DifferenceObject @($archive.Entries.FullName) -CaseSensitive)) {
        throw 'Unexpected files in the installer archive.'
    }

    $reader = [IO.StreamReader]::new($archive.GetEntry('LICENSE.txt').Open())
    try {
        $license = $reader.ReadToEnd().Replace("`r`n", "`n")
    } finally { $reader.Dispose() }
    $licenseHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($license)))
    # Mozilla's official plain-text MPL-2.0, with LF line endings and no trailing spaces.
    # https://www.mozilla.org/media/MPL/2.0/index.815ca599c9df.txt
    if ($licenseHash -ne '1F256ECAD192880510E84AD60474EAB7589218784B9A50BC7CEEE34C2B91F1D5') {
        throw 'The installer must contain the full MPL-2.0 licence text.'
    }

    $dll = [IO.Path]::ChangeExtension([IO.Path]::GetRandomFileName(), '.dll')
    $dll = Join-Path ([IO.Path]::GetTempPath()) $dll
    try {
        [IO.Compression.ZipFileExtensions]::ExtractToFile($archive.GetEntry("$AssemblyName.dll"), $dll)
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll)
        if ($info.FileVersion -ne $settings.Version -or $info.ProductVersion -ne "$($settings.Version)+$SourceCommit") {
            throw 'The packaged DLL version or source revision differs from the release build.'
        }
    } finally { [IO.File]::Delete($dll) }
} finally { $archive.Dispose() }
Write-Host 'Verified release archive and manifest.'
