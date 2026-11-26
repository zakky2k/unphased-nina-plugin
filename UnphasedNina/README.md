# Unphased Night Planner

A dockable night-planning panel for [N.I.N.A.](https://www.nina.ms): the Sun and Moon altitude
against the horizon and the astronomical twilight bands, over one night, three nights or a week
stitched together, with an hour-by-hour scrub and a moon-phase glyph.

Ephemerides come from [CosineKitty.AstronomyEngine](https://github.com/cosinekitty/astronomy)
through `UnphasedCore`, evaluated for the observatory in your N.I.N.A. profile.

Status: **0.x beta**. It runs and is used, and the [changelog](CHANGELOG.md) says what is still rough.

## Requirements

Built against `NINA.Plugin` **3.2.0.9001** for **net8.0-windows**, and it declares that as the
minimum application version. N.I.N.A. 3.1 and older will refuse to load it; a 3.2.x installation
loads it. (This tree also carries a 3.3/net10 experiment in `UnphasedCore` notes —
this plugin stays on the shipped 3.2 line until the app it targets is released.)

## Install

The plugin is **not in N.I.N.A.'s plugin list yet**, deliberately — see *Release and the plugin
catalogue* below. Until it is, install it from the GitHub release:

1. Download `UnphasedNina.0.7.0.0.zip` from the
   [releases page](https://github.com/zakky2k/unphased-nina-plugin/releases) — 154,585 bytes,
   SHA256 `7da5fd2421f93b54d540a7460a8e6eb43bbf2258b6b456be2bb5186e602c0d1f`.
2. Unzip it into `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Unphased Night Planner\`, creating that folder.
   The archive holds everything: `UnphasedNina.dll`, `UnphasedCore.dll`, `astronomy.dll`,
   `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `UnphasedNina.pdb` (the `.pdb` is optional).
3. Restart N.I.N.A. The panel is the **puzzle-piece button in the Info bar** on the Imaging page;
   the options page is *Unphased Night Planner* in N.I.N.A.'s options.

To build it instead: `dotnet msbuild src\UnphasedNina\UnphasedNina.csproj -t:DeployPlugin -v:m`
stages and copies into the live folder in one go (close N.I.N.A. first — the target refuses while it
runs); the staged files are also sitting in `artifacts\stage\Debug` if you prefer to copy yourself.

The plugin's identity is the GUID in `Plugin.props`; N.I.N.A. keys the settings file off it, so the
GUID is permanent once published.

## Options

*Panel night mode* re-colours the panel for use at the telescope. Sun, Moon and horizon colours can
be set on the options page and the chart repaints as you pick them. Settings are global, stored at
`%LOCALAPPDATA%\NINA\PluginSettings\09572065-E196-4DBA-B954-48F84B351DD1\settings.json`.

## Develop

Windows, .NET 8 SDK and PowerShell 7. Open `UnphasedNina.slnx` or use the CLI. The repository root
holds this plugin (`UnphasedNina/`), the night computation it shares with the web planner
(`UnphasedCore/`) and that core's accuracy tests (`UnphasedCore.Tests/`).

```powershell
dotnet build UnphasedNina.slnx
dotnet test tests\UnphasedNina.Tests\UnphasedNina.Tests.csproj -c Debug -f net8.0-windows
```

Copy `local.props.example` to the ignored `local.props` and point `NinaExecutable` at your N.I.N.A.
installation. Normal builds do not deploy; `DeployPlugin` does, and needs N.I.N.A. closed.

Two things to know before running the suite: the settings tests write to the **live** settings file
shown above, so re-pick your colours afterwards, and the process may hold exactly one
`System.Windows.Application` — `CompositionTests` owns it, so a new fixture must not create another.
`PlannerChartRenderSmoke` is `[Explicit]`; run it with
`--filter FullyQualifiedName~PlannerChartRenderSmoke` to write nine chart previews.

## Release and the plugin catalogue

```powershell
./build/Build-Release.ps1 -Tag v0.7.0.0 -Repository zakky2k/unphased-nina-plugin -Channel Beta
```

`-Repository` is what the archive's `Installer.URL` and the manifest's `Repository` are built from,
so it must name **this** repository and the tag you are about to publish — without it the installer
URL falls back to a placeholder the catalogue CI rejects.

That restores, builds Release, runs the tests, zips the staged files, and writes
`artifacts\release\UnphasedNina.<version>.zip` plus `UnphasedNina.<version>.manifest.json`,
validated against N.I.N.A.'s own manifest schema (`build\vendor\manifest.schema.json`, vendored
unmodified — see [PROVENANCE.md](build/vendor/PROVENANCE.md)). Nothing is uploaded or tagged.
`Verify-ReleaseArtifacts.ps1` re-checks an archive against its manifest (checksum, URL, version).

**Status: 0.7.0.0 is released but deliberately *not* submitted to the catalogue.** Listing means a
pull request into someone else's repository, reviewed and merged by a N.I.N.A. maintainer, once per
version you want listed — a gate that is fine but is not free. The manifest is already written and
already passes the catalogue's own validation (`node validate-latest-manifest.js` after committing:
schema valid, and its own re-hash of the hosted ZIP matched), so it is kept here as
`catalogue\manifest.0.7.0.0.json` and submitting it later is a two-minute job. Nothing about the
catalogue is load-bearing: it holds no code, the binaries always come from this repository's
releases, and an unlisted or rejected manifest changes nothing about whether the plugin works.

Then, by hand:

1. Create the GitHub release for the tag and attach the ZIP; the manifest's `Installer.URL` is
   built from that tag, so the URL and the asset name have to match exactly. **Do this before
   submitting anything**: the catalogue's CI downloads `Installer.URL` and re-hashes the bytes, and
   a 404 or a hash mismatch fails the pull request. Never rebuild the ZIP after hashing it.
2. Submit the manifest to the [N.I.N.A. plugin manifests repository](https://github.com/isbeorn/nina.plugin.manifests)
   — that repository *is* the catalogue; it holds JSON only, the binaries stay here. Fork it, add
   the manifest as
   `manifests/U/Unphased Night Planner/3.2.0.9001/0.7.0.0/manifest.json`, and open a pull request.
   Its CI validates against the schema and re-hashes the download; on merge, `gather.js` publishes
   the file to `https://nighttime-imaging.eu/wp-json/nina/v1/...`, which is what N.I.N.A. fetches
   (`PluginFetcher.cs:38`). A manifest that fails validation is simply never listed, so
   `Package-Plugin.ps1` validates locally with the same schema first.
   Start with `"Channel": "Beta"` (what `Build-Release.ps1 -Channel Beta` writes): beta manifests
   land in a separate list that users opt into from *Options › General › Plugin Repositories*.
3. The store page shows `Descriptions.ShortDescription` (max 256 characters), the long description
   (max 10000), up to 20 tags, and the images at `ScreenshotURL`/`AltScreenshotURL`/`FeaturedImageURL`.
   `FeaturedImageURL` is set — it is `assets/unphased-256.png` in this repository, and N.I.N.A. draws it both in the plugin list and on the plugin's settings page, so it must not be moved or renamed without rebuilding. The two screenshot URLs are **empty in this build on purpose** — there are no hosted screenshots yet, and a
   made-up URL would only 404 in the store. Upload panel screenshots as release assets and fill the
   properties in `Plugin.props` when they exist.
4. `ChangelogURL` points at this repository's raw [CHANGELOG.md](CHANGELOG.md); N.I.N.A. 3.3 shows
   that document in the plugin view, and 3.2 lists it as a link — either way the changelog stays in
   the repo rather than being duplicated in the store.
5. The plugin's identity is its GUID and `Name` together: the catalogue rejects two manifests that
   share a GUID with different names, and N.I.N.A. keys settings and de-installation off the GUID.
   Both are frozen from the first published version.

`PluginPublishingTests` builds the manifest from the built assembly and validates it against the
same schema, so metadata that is too long, empty, or still the template placeholder fails the suite
instead of quietly disappearing from the catalogue.

## Maintenance and disclosure

Accountable human maintainer: **zakky2k** (the `Author` in the listing). Development of this plugin
has been materially AI-assisted (Anthropic's Claude acting as the programmer, with the maintainer
reviewing, testing and deciding every change); the catalogue's rules ask for that to be stated, and
the code here is reviewed rather than shipped unexamined.

Redistributed third-party code and its licences are listed in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), which ships in the release archive next to
LICENSE.txt. Every source file here opens with the MPL-2.0 notice in the licence's own wording —
N.I.N.A.'s wording, since the plugin is built to sit inside that codebase — and
`LicenceHeaderTests` walks the tree and fails on any file that arrives without it. Files under
`build/vendor/` are the exception on purpose: they are unmodified third-party code carrying their own
licence (see `build/vendor/PROVENANCE.md`).

## License

MPL-2.0, see [LICENSE.txt](LICENSE.txt).
