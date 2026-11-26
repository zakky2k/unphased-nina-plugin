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
loads it. (The `nina-plugin` tree also carries a 3.3/net10 experiment in `UnphasedCore` notes —
this plugin stays on the shipped 3.2 line until the app it targets is released.)

## Install

From N.I.N.A.'s plugin page once it is listed, or by hand:

1. Build and stage: `dotnet msbuild src\UnphasedNina\UnphasedNina.csproj -t:DeployPlugin -v:m`
   (this also copies into the live folder; close N.I.N.A. first — the target refuses while it runs).
2. Or copy the staged files yourself from `artifacts\stage\Debug` to
   `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Unphased Night Planner\`: `UnphasedNina.dll`,
   `UnphasedCore.dll`, `astronomy.dll`, `LICENSE.txt` (the `.pdb` is optional).
3. Restart N.I.N.A. The panel is the **puzzle-piece button in the Info bar** on the Imaging page;
   the options page is *Unphased Night Planner* in N.I.N.A.'s options.

The plugin's identity is the GUID in `Plugin.props`; N.I.N.A. keys the settings file off it, so the
GUID is permanent once published.

## Options

*Panel night mode* re-colours the panel for use at the telescope. Sun, Moon and horizon colours can
be set on the options page and the chart repaints as you pick them. Settings are global, stored at
`%LOCALAPPDATA%\NINA\PluginSettings\09572065-E196-4DBA-B954-48F84B351DD1\settings.json`.

## Develop

Windows, .NET 8 SDK and PowerShell 7. Open `UnphasedNina.slnx` or use the CLI.

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
./build/Build-Release.ps1 -Tag v0.6.1.0 -Repository zakky2k/AstroScripts -Channel Beta
```

That restores, builds Release, runs the tests, zips the staged files, and writes
`artifacts\release\UnphasedNina.<version>.zip` plus `UnphasedNina.<version>.manifest.json`,
validated against N.I.N.A.'s own manifest schema (`build\vendor\manifest.schema.json`, vendored
unmodified — see [PROVENANCE.md](build/vendor/PROVENANCE.md)). Nothing is uploaded or tagged.
`Verify-ReleaseArtifacts.ps1` re-checks an archive against its manifest (checksum, URL, version).

Then, by hand:

1. Create the GitHub release for the tag and attach the ZIP; the manifest's `Installer.URL` is
   built from that tag, so the URL and the asset name have to match exactly. **Do this before
   submitting anything**: the catalogue's CI downloads `Installer.URL` and re-hashes the bytes, and
   a 404 or a hash mismatch fails the pull request. Never rebuild the ZIP after hashing it.
2. Submit the manifest to the [N.I.N.A. plugin manifests repository](https://github.com/isbeorn/nina.plugin.manifests)
   — that repository *is* the catalogue; it holds JSON only, the binaries stay here. Fork it, add
   the manifest as
   `manifests/U/Unphased Night Planner/3.2.0.9001/0.6.2.0/manifest.json`, and open a pull request.
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
LICENSE.txt. The plugin's own source does not yet carry a per-file licence header — one thing to
clean up before the source is announced publicly, since MPL-2.0 expects the notice in each file.

## License

MPL-2.0, see [LICENSE.txt](LICENSE.txt).
