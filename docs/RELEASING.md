# Releasing Unphased Night Planner

## Build and verify

Start from a clean, committed checkout. Use the [build requirements](../README.md#build-from-source)
and run [`Build-Release.ps1`](../build/Build-Release.ps1) in PowerShell 7
from the repository root. Replace the example tag with the version you are publishing.

```powershell
./build/Build-Release.ps1 -Tag v0.8.3.0 -Repository zakky2k/unphased-nina-plugin -Channel Beta
```

Both `-Repository` and `-Tag` are required. `-Repository` takes the GitHub
`owner/repository` and sets the manifest's `Repository` URL. `-Tag` takes the
four-part release tag with a leading `v`. The script uses both values to construct
the manifest's installer URL. Use `-Channel Beta` for the opt-in beta channel.

The script runs the Release build and tests, then writes the ZIP and manifest to
`artifacts/release/`.

- `UnphasedNina.<version>.zip`
- `UnphasedNina.<version>.manifest.json`

It validates the manifest against the vendored N.I.N.A. schema and verifies the
ZIP's SHA-256 checksum, the installer URL, the DLL version and source revision,
and the bundled MPL-2.0 licence. Tagging and uploading are manual steps.

## Publish on GitHub

1. Create or confirm the release tag on the exact commit used to build the ZIP.
2. Publish the GitHub release for that tag and attach the verified ZIP and its
   matching manifest. The asset name must match the manifest's `Installer.URL`.
3. Confirm that the public installer URL downloads the ZIP and that its SHA-256
   matches `Installer.Checksum` before submitting the manifest. Keep the verified
   ZIP unchanged.

## Submit to the N.I.N.A. catalogue

1. Follow the [N.I.N.A. manifest submission guide](https://github.com/isbeorn/nina.plugin.manifests#readme)
   for the folder path and review process. Use the manifest's
   `MinimumApplicationVersion` and plugin version for the path, and name the
   file `manifest.json` in the catalogue. Preserve the published plugin name
   and GUID across releases.
2. Open a pull request against the catalogue repository with the manifest and
   wait for review and merge. A manifest built with `-Channel Beta` is for users
   who have enabled the beta plugin repository in N.I.N.A.
