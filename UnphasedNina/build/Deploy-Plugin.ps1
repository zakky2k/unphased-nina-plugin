[CmdletBinding()]
param([Parameter(Mandatory)][string]$Stage, [Parameter(Mandatory)][string]$AssemblyName, [string]$Destination, [string]$NinaExe)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!(Test-Path -LiteralPath (Join-Path $Stage '.nina-template-stage'))) { throw 'Use the DeployPlugin target to stage files first.' }
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Stage $AssemblyName))
    $folder = $metadata.FileDescription.Replace('\', '-').Replace('/', '-')
    foreach ($character in [IO.Path]::GetInvalidFileNameChars()) { $folder = $folder.Replace([string]$character, '_') }
    if ([string]::IsNullOrWhiteSpace($folder) -or $folder -in @('.', '..')) { throw 'Plugin display name does not form a valid folder name.' }
    $Destination = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "NINA/Plugins/3.0.0/$folder"
}
# N.I.N.A. refuses to load a plugin whose MinimumApplicationVersion is newer than the installed app,
# and it fails silently in the UI. That happens when the plugin is built against a newer NINA.Plugin
# package than the installed N.I.N.A. (for example a net10.0 build against the 3.3 nightly), so stop here.
$mainAssembly = Join-Path $Stage $AssemblyName
$minimumVersion = [regex]::Match([IO.File]::ReadAllText($mainAssembly, [Text.Encoding]::ASCII), 'MinimumApplicationVersion[\x00-\x7f]{0,8}?(\d+\.\d+\.\d+\.\d+)').Groups[1].Value
if ($minimumVersion) {
    if ([string]::IsNullOrWhiteSpace($NinaExe) -or !(Test-Path -LiteralPath $NinaExe)) {
        Write-Warning 'Cannot verify plugin compatibility: set NinaExecutable in local.props to the installed NINA.exe.'
    } else {
        $ninaVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($NinaExe).FileVersion
        if ($ninaVersion -and [version]$minimumVersion -gt [version]$ninaVersion) {
            throw "Staged $AssemblyName declares MinimumApplicationVersion $minimumVersion but the installed N.I.N.A. is $ninaVersion ($NinaExe). Build against the matching NINA.Plugin package (see Directory.Build.props NinaPackageVersion) before deploying."
        }
        Write-Host "Compatibility check: plugin needs N.I.N.A. $minimumVersion, installed app is $ninaVersion."
    }
}
$files = @(Get-ChildItem -LiteralPath $Stage -File -Recurse | Where-Object Name -ne '.nina-template-stage')
# Check locks before copying anything. Recheck errors during copying as well.
foreach ($file in $files) {
    $target = Join-Path $Destination ([IO.Path]::GetRelativePath($Stage, $file.FullName))
    if (Test-Path -LiteralPath $target) {
        try { $handle = [IO.File]::Open($target, 'Open', 'ReadWrite', 'None'); $handle.Dispose() }
        catch { throw "Cannot replace '$target'. Close NINA and retry. $($_.Exception.Message)" }
    }
}
foreach ($file in $files) {
    $target = Join-Path $Destination ([IO.Path]::GetRelativePath($Stage, $file.FullName))
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force -ErrorAction Stop
}
Write-Host "Deployed plugin to $Destination. Restart NINA to load it."
