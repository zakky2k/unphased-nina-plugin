# Unphased Night Planner

**Visualise darkness, twilight and moonlight across consecutive nights in a single interactive
astronomical chart.**

**[Download the Windows plugin ZIP · v0.8.2.0 Beta](https://github.com/zakky2k/unphased-nina-plugin/releases/download/v0.8.2.0/UnphasedNina.0.8.2.0.zip)**

Requires N.I.N.A. **3.2.0.9001 or newer**. This beta is available as a manual install.

1. Close N.I.N.A. and extract the ZIP directly into
   `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Unphased Night Planner\`.
2. Check that `UnphasedNina.dll` sits directly inside that folder, then reopen N.I.N.A.
3. In **Imaging**, use the puzzle-piece button in the Info bar to open the planner.

[All releases](https://github.com/zakky2k/unphased-nina-plugin/releases) · [Changelog](CHANGELOG.md) · [Build and test](#develop)

## Three perspectives. One view of the night.

### Lunar and planetary phases

<img src="assets/pillars/lunar-phases.svg" width="64" height="64" alt="A crescent Moon with its trajectory across the horizon">

Explore the dynamic phases of the Moon, Mercury and Venus alongside their altitude curves. Identify periods
when the Moon is below the horizon, assess potential moonlight interference and discover moon-free
imaging opportunities.

*Understand the Moon and inner planets.*

### Astronomical darkness

<img src="assets/pillars/astronomical-darkness.svg" width="64" height="64" alt="A setting Sun above the twilight bands, falling below the horizon">

Visualise the transition from daylight through civil, nautical and astronomical twilight into
astronomical darkness. Easily identify when darkness begins, when it ends and how much dark imaging
time is available throughout each night.

*Understand the darkness.*

### Multi-night visualisation

<img src="assets/pillars/multi-night-visualisation.svg" width="64" height="64" alt="A calendar carrying three astronomical altitude curves">

Look beyond tonight and explore how astronomical darkness and the Moon evolve across consecutive
nights. Compare upcoming nights and identify favourable windows for future astrophotography
sessions.

*Plan the nights ahead.*

---

Ephemerides come from [CosineKitty.AstronomyEngine](https://github.com/cosinekitty/astronomy)
through `UnphasedCore`, evaluated for the observatory in your N.I.N.A. profile. The planner
visualises astronomical conditions independently of weather forecasts, atmospheric transparency
and local light pollution.

Status: **0.x beta**, under active development. It runs and is used, and the [changelog](CHANGELOG.md)
says what is still rough.

## Options

The options page puts the three perspectives above first, so the purpose of the plugin is read before
its settings are offered. After them:

*Observatory location* uses the observatory in your active N.I.N.A. profile, or pins a manual
override — decimal degrees with *Apply location* and *Use N.I.N.A. profile location*.

*Display palette* has three palettes: **N.I.N.A. theme** — a consistent appearance with the host
application; **Night vision** — a red-toned display designed for use at the telescope, which the
panel follows too; and **High contrast** — enhanced visual distinction between chart elements. The
panel's header abbreviates these to *default*, *night* and *enhanced* because 86 DIP does not hold
"N.I.N.A. theme", and its buttons say so in their tooltips.

*Custom chart colours* sets **solar altitude**, **lunar altitude** and the **horizon**; the chart
repaints as you pick them, and *Restore default colours* hands the colours back to the palette.

Settings are global, stored at
`%LOCALAPPDATA%\NINA\PluginSettings\09572065-E196-4DBA-B954-48F84B351DD1\settings.json`.

## Develop

Use Windows, the .NET 10 SDK version in `global.json`, the .NET 8 Windows Desktop Runtime,
and PowerShell 7. Open `UnphasedNina.slnx` or run these commands from the repository root.

```powershell
dotnet build UnphasedNina.slnx
dotnet test UnphasedNina.slnx
```

Copy `local.props.example` to the ignored `local.props` and point `NinaExecutable` at your N.I.N.A.
installation. Normal builds do not deploy; `DeployPlugin` does, and needs N.I.N.A. closed.
`dotnet msbuild src/UnphasedNina/UnphasedNina.csproj -t:StagePlugin` stages a
Debug install under `artifacts/stage/Debug`. Use `-t:DeployPlugin` to copy it into N.I.N.A.

- [src/](src/) contains the plugin and shared astronomy core.
- [tests/](tests/) contains the plugin and core test suites.
- [build/](build/) contains packaging and deployment scripts.
- [catalogue/](catalogue/) contains the versioned plugin manifests.
- [assets/](assets/), [docs/](docs/), and [tools/](tools/) hold artwork, project notes, and developer utilities.

The settings tests use a unique directory under the test working directory. Use the shared
`WpfHost` for tests that need a `System.Windows.Application`.
`PlannerChartRenderSmoke` is `[Explicit]`; run it with
`--filter FullyQualifiedName~PlannerChartRenderSmoke` to write nine chart previews.

The native astronomy comparison is also opt-in. With the N.I.N.A. installation's `External`
folder copied beside the compiled test assembly, run
`dotnet test tests/UnphasedNina.Tests/UnphasedNina.Tests.csproj -c Release --no-build --filter FullyQualifiedName~OverlayAltitudesAgreeWithIndependentEphemerisAndFollowTheLatestFrame`.
Use the filter `FullyQualifiedName~CatalogTypeRequiresAnExactAliasAndMatchingPosition`
to check against the installed N.I.N.A. catalogue.

## Release and the plugin catalogue

```powershell
./build/Build-Release.ps1 -Tag v0.8.2.0 -Repository zakky2k/unphased-nina-plugin -Channel Beta
```

`-Repository` is what the archive's `Installer.URL` and the manifest's `Repository` are built from,
so it must name **this** repository and the tag you are about to publish — without it the installer
URL falls back to a placeholder the catalogue CI rejects.

Commit the release changes before running the script. It restores, builds Release, runs the tests, zips the staged files, and writes
`artifacts\release\UnphasedNina.<version>.zip` plus `UnphasedNina.<version>.manifest.json`,
validated against N.I.N.A.'s own manifest schema (`build\vendor\manifest.schema.json`, vendored
unmodified — see [PROVENANCE.md](build/vendor/PROVENANCE.md)). Nothing is uploaded or tagged.
The script verifies the exact versioned archive and manifest, including the checksum, installer URL,
packaged DLL version and source revision, and the full MPL-2.0 text inside the ZIP. Tag the
verified source commit when publishing.

N.I.N.A. loads the Clear Horizon artwork from `assets/unphased-256.png`. The
full-size artwork is `assets/unphased-featured.png`, and `assets/unphased.ico`
is the Windows desktop variant with 16, 20, 24, 32, 40, 48, 64, 96, 128 and
256 px frames. The full-size PNG and ICO can be attached directly to the GitHub
release. See the [approved Clear Horizon reference](assets/clear-horizon-reference.png)
for both designs and the small-size previews.

**The beta is distributed through GitHub Releases.** The manifests under
`catalogue/` are ready for a separate catalogue submission. Each catalogue
version requires a pull request reviewed by a N.I.N.A. maintainer, and the
catalogue downloads the installer from this repository's release.

Then, by hand:

1. Create the GitHub release for the tag and attach the ZIP; the manifest's `Installer.URL` is
   built from that tag, so the URL and the asset name have to match exactly. **Do this before
   submitting anything**: the catalogue's CI downloads `Installer.URL` and re-hashes the bytes, and
   a 404 or a hash mismatch fails the pull request. Never rebuild the ZIP after hashing it.
2. Submit the manifest to the [N.I.N.A. plugin manifests repository](https://github.com/isbeorn/nina.plugin.manifests)
   — that repository *is* the catalogue; it holds JSON only, the binaries stay here. Fork it, add
   the manifest as
   `manifests/U/Unphased Night Planner/3.2.0.9001/0.8.2.0/manifest.json`, and open a pull request.
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
