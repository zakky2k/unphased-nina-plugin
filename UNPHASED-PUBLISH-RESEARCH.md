# Unphased Night Planner (0.6.1.0) → official N.I.N.A. plugin catalogue: research report

Research only; no plugin code was changed. All claims below were verified on 2026-11-25 against primary
sources: the N.I.N.A. source tree, the catalogue repository, the live catalogue endpoints, and this
machine's installed N.I.N.A. + our deployed plugin.

## 0. Evidence base (what "3.2" means precisely)

* Installed app on this machine: `C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy\NINA.exe`,
  `FileVersion 3.2.1.3002`, `ProductVersion 3.2.1.3002-rc`, `.NET 8.0.31`.
* N.I.N.A. source: repo `isbeorn/nina`. **3.2.x lives on branch `release/3.2.x`** (tip
  `9c1661d99a2af95c33b1657d0f8cb23fa15d1025`, `CommonAssemblyInfo.cs` lines 52-54:
  `AssemblyVersion("3.2.1.3002")`, `AssemblyFileVersion("3.2.1.3002")`,
  `AssemblyInformationalVersion("3.2.1.3002-rc")`). There is **no `Version-3.2.1` tag**; the tag
  `Version-3.2` is the older 3.2.0.9001. Default branch `develop` is **3.3.x** (`3.3.0.1070`,
  `net10.0-windows`). Everything below was read from `release/3.2.x` unless stated otherwise, because
  that is the branch that produced the build actually installed here.
* Catalogue repo: `isbeorn/nina.plugin.manifests` (default branch `main`, MPL-2.0, language
  PowerShell, `pushed_at` 2026-11-24T20:32:19Z; PRs #813/#814/#815 merged 2026-11-24) → still the live,
  current catalogue; not superseded.
* Files read in full: `NINA.Plugin/{PluginLoader,PluginBase,PluginAssemblyReader,PluginCompatibility,PluginInstaller,PluginFetcher,Constants}.cs`,
  `NINA.Plugin/ManifestDefinition/{PluginVersion,PluginManifest}.cs`, `CommonAssemblyInfo.cs`,
  `NINA/ViewModel/Plugins/PluginsVM.cs`, `NINA.Core/Locale/Locale.resx`, plus catalogue
  `README.md` (274 lines), `manifest.schema.json`, `gather.js`, `validate-latest-manifest.js`,
  `.github/workflows/github-action.yaml`, `tools/github-action.yaml`, `tools/CreateManifest.ps1`,
  and docs repo `isbeorn/nina.docs` (`docs/contributing/plugins.md`, `docs/tabs/plugins/{available,installed}.md`).
* Live endpoints (fetched, HTTP 200): `https://nighttime-imaging.eu/wp-json/nina/v1/plugins/manifests`
  and `.../nina/v1/beta/plugins/manifests`. `.../v1/manifest` and `.../v1/manifests` return
  `{"code":"rest_no_route"}`.

---

## 1. What the official catalogue is, and the exact submission path

**It is a metadata repository of JSON manifests, not a NuGet feed and not a GitHub-release-based store.**
Authors host the binaries themselves (normally a GitHub release asset); the catalogue only holds a JSON
manifest per plugin version, which is aggregated and pushed to the N.I.N.A. web server.

Repo README (`https://raw.githubusercontent.com/isbeorn/nina.plugin.manifests/main/README.md`) line 2:

> "All plugin manifests inside this repository are put onto the N.I.N.A. server to be used to show and
> install plugins inside the N.I.N.A. application"

README lines 7-13, the official procedure:

> "1. Meet the prerequisites 2. Develop a plugin 3. Create a manifest 4. Test your manifest against the
> schema 5. Submit a pull request against this repository with your manifest 6. Wait for the pull
> Request to be reviewed and merged"

Pipeline detail (verified in the repo, not just the README):

1. **Fork** (`README.md:48`: "To be able to submit a pull request, the repository needs to be forked first.").
2. **Add one JSON file per plugin version** at
   `manifests\<plugin starting letter><your plugin name>\<nina version>\<your plugin version>\manifest.json`
   (`README.md:243`). Real examples in the tree (194 manifest paths, recursive tree
   `c0257cf9b7e6827dc02bf8676d2e9232e67e22fc`): `Q/Quick Actions/3.2.0.9001/0.9.0.0/manifest.json`,
   `1/10 Micron Tools/3.2.0.9001/manifest.json`, `a/Astraeus/3.2.0.9001/manifest.1.1.1.320.json`,
   `c/Connector/3.2.0/manifest.json`. So both `manifest.json` and `manifest.<version>.json` naming, and
   both `3.2.0` and `3.2.0.9001` as the NINA-version folder, are in active use.
   Counts by NINA-version folder: `3.0` 126, `3.1` 20, `3.2` 22, `3.3` 8.
3. **PR validation (hard gate).** `.github/workflows/github-action.yaml` job `validate-latest-manifest`
   runs `node validate-latest-manifest.js`, which for every changed `.json`: (a) validates against
   `manifest.schema.json` (Ajv + ajv-formats), then (b) **downloads `Installer.URL` and hashes it** and
   compares with `Installer.Checksum` case-insensitively. Failure strings include "Installer URL
   returned HTTP 404 Not Found from <url>…" and "Expected hash to be …, but actually was … from <url>".
4. **Merge → aggregation.** `gather.js` scans `manifests/`, validates each file against the schema,
   rejects identifier collisions (`Id collision for manifest! …`), then writes `Plugins/manifests.json`
   (entries with `Channel === 'Release'` or no `Channel`, with `Channel` stripped) and
   `Plugins/Beta/manifests.json` (`Channel === 'Beta'`).
5. **Publish = FTPS upload, not GitHub Pages.** Workflow job `push-manifest`:
   `curl -T ./Plugins/manifests.json ... "ftp://$FTP_HOST/manifests.json"` and
   `... "ftp://$FTP_HOST/Beta/manifests.json"`.
6. **What the app reads.** `NINA.Plugin/Constants.cs`:
   `public static readonly string MainPluginRepository = "https://nighttime-imaging.eu/wp-json/nina/v1";`
   and `NINA.Plugin/PluginFetcher.cs:42`: `var req = new HttpGetRequest(repositoryURL + "/plugins/manifests", true);`
   → effective URL `https://nighttime-imaging.eu/wp-json/nina/v1/plugins/manifests`. `OptionsVM.cs`
   re-inserts the default repository ("We enforce that the main plugin repository is always present");
   users can add more under **Options > General > Plugin Repositories** (`LblPluginRepositories`).
7. **Beta channel** (`README.md:247-274`): add `"Channel": "Beta"`; users opt in by adding
   `https://nighttime-imaging.eu/wp-json/nina/v1/beta` as a Plugin Repository.

Ways to produce the manifest (README lines 51-136): the recommended CI
(`tools/github-action.yaml` — README writes `github-action.yml`, the file is `.yaml`; trigger tag pattern
`'[0-9]+.[0-9]+.[0-9]+.[0-9]+'`, env `PLUGIN_NAME`, `PLUGIN_SLN_NAME`, `PLUGIN_DLL_NAME`,
`PLUGIN_MANIFEST_PATH`, and an auto-PR job using secret `PAT`), or `tools/CreateManifest.ps1`
(`-file`, `-installerUrl`, `-createArchive`, `-archiveName`, `-includeAll`, `-appendVersionToArchive`,
`-beta`, Bitbucket options). README line 55 warning: "Make sure that your DLL will not be recompiled or
changed after the manifest is created, as the checksum will change each time!" Manual creation is allowed
but: "It is important that the manifest follows the assembly meta data. Especially the `Identifier` and
`Name` needs to be the same as inside the plugin assembly meta data." Local test: `winget install nodejs`,
`npm install`, `node gather.js`.

We already vendor the official tooling: `UnphasedNina/build/vendor/{CreateManifest.ps1,manifest.schema.json}`
(`build/vendor/PROVENANCE.md` pins revision `a6c663f4f92e76abf4f501e91bc511d7c7e544c1`), and
`build/Package-Plugin.ps1` already produces an `ARCHIVE` zip + SHA256 + schema-validated manifest via
`-InstallerUrl`/`-Channel`.

Manual installation remains supported in parallel (3.2.x `Locale.resx`: `LblPluginDllInstallDescription` =
"Install via the N.I.N.A. plugin manager or manually download and copy the DLL file into
%localappdata%\NINA\Plugins\<", and `LblPluginArchiveInstallDescription` = "…extract the archive into
%localappdata%\NINA\Plugins fr…").

---

## 2. Required metadata, and where each field actually lives

There are **two independent metadata layers**, and both must be right.

### 2a. Assembly metadata — read at load time by N.I.N.A.

`PluginBase.cs` (`NINA.Plugin`, release/3.2.x) is the authoritative mapping; the plugin class must be
`[Export(typeof(IPluginManifest))]`. Line numbers from release/3.2.x:

| Manifest field | Assembly attribute actually read |
|---|---|
| `Identifier` | `[Guid]` — `assembly.GetCustomAttribute<GuidAttribute>().Value` (line 36) |
| `Version` | **`AssemblyFileVersionAttribute`** — `?.Version ?? "1.0.0.0"` (line 43) |
| `Name` | `AssemblyTitleAttribute.Title` (line 50) |
| `Author` | **`AssemblyCompanyAttribute.Company`** (line 61) — *not* `<Authors>` |
| `License` / `LicenseURL` / `Homepage` / `Repository` / `ChangelogURL` | `[AssemblyMetadata]` keys `License`, `LicenseURL`, `Homepage`, `Repository`, `ChangelogURL` (54-69) |
| `Tags` | `[AssemblyMetadata("Tags")]`, `tags.Split(',')` (74-78) |
| `MinimumApplicationVersion` | `[AssemblyMetadata("MinimumApplicationVersion")]`, **default `"1.11.0.0"` when absent** (86-87) |
| `Descriptions.ShortDescription` | `AssemblyDescriptionAttribute.Description` (102) |
| `Descriptions.LongDescription` / `FeaturedImageURL` / `ScreenshotURL` / `AltScreenshotURL` | `[AssemblyMetadata]` keys with those exact names (103-106) |
| `Installer` | not from the assembly: `PluginBase` returns an empty `PluginInstallerDetails` ("The installer details are irrelevant in this scope and have to be filled out by the plugin maintainer") |

`NinaPackageVersion` (our `Directory.Build.props`) is a **build-time-only** NuGet reference
(`PackageReference Include="NINA.Plugin"`); `MinimumNinaVersion` (our `Plugin.props`) becomes
`[AssemblyMetadata("MinimumApplicationVersion")]` — that is the field the host compares. Our `Plugin.props`
already emits exactly the key names above, matching the official template
(`isbeorn/nina.plugin.template`, `master:templates/nina-plugin/Plugin.props`, which is byte-for-byte the
same property set at 3.3 values).

Never read by anything in N.I.N.A.: `<Authors>`, `<Copyright>`, `PackageId`/`PackageTags`/
`PackageLicenseExpression`/`PackageProjectUrl`/`PackageIcon`/`PackageReadme` (repo-wide grep for `nuspec`
in N.I.N.A. `*.cs`/`*.csproj` = 0 hits), a `manifest.json` shipped next to the DLL, an icon resource, or a
README file. There is no `[NINAPlugin(...)]`-style attribute; the only "declaration" is MEF
`[Export(typeof(IPluginManifest))]`.

### 2b. Catalogue manifest JSON — `manifest.schema.json` (identical schema embedded in
`NINA.Plugin/ManifestDefinition/PluginManifest.cs` as `Schema`, used by `PluginFetcher.ValidateAndParseManifest`)

`"required": ["Name","Identifier","Version","Author","Repository","License","LicenseURL","MinimumApplicationVersion","Descriptions","Installer"]`

* `Name` maxLength 50 (also used as the **install folder name** — README line 144,
  `PluginInstaller.GetDestinationFolderFromManifest` = `Path.Combine(Constants.UserExtensionsFolder,
  CoreUtil.ReplaceAllInvalidFilenameChars(manifest.Name))`).
* `Identifier` `"format": "uuid"` — README line 148: "must not be changed throughout the lifetime of
  your plugin for version increases. It is used to identify your assembly during the installation and
  de-installation process."
* `Author` maxLength 256; `License` maxLength 512; `Repository` required, "A link to the remote
  repository, where the source code of the plugin is available".
* `Version`, `MinimumApplicationVersion`: objects with **required** `Major`,`Minor`,`Patch`,`Build`
  (digits as string or number). `MinimumApplicationVersion` = the "supports" field: "the plugin manager
  inside the application will serve the plugin manifest with the highest version that is compatible with
  the currently running application using the minimum application version" (README line 177).
* `Descriptions`: only **`ShortDescription` is required** (maxLength 256); `LongDescription` maxLength
  10000; `FeaturedImageURL`, `ScreenshotURL`, `AltScreenshotURL` optional.
* `Tags` ≤ 20 items, each 1-30 chars. `Homepage`, `ChangelogURL` optional.
* `Installer`: required `URL`, `Checksum`, `ChecksumType`, `Type`; `Checksum` `^[A-Fa-f0-9]{32,64}$`,
  `ChecksumType` ∈ `MD5|SHA1|SHA256`, `Type` ∈ `DLL|ARCHIVE`.
* `Channel` is **not** in the schema and not in the C# `PluginManifest` — only `gather.js` filters on it.

Our current values: `Name` = "Unphased Night Planner" (22 chars, OK), GUID
`09572065-E196-4DBA-B954-48F84B351DD1`, `MPL-2.0` + URL, `MinimumApplicationVersion` 3.2.0.9001,
`ShortDescription` = "A NINA plugin." (placeholder), and `LongDescription`/`Tags`/`Homepage`/
`ChangelogURL`/images empty; `Repository` points at a placeholder repo.

---

## 3. Bundled dependency DLLs (`astronomy.dll`, `UnphasedCore.dll`)

**Tolerated, normal, and mechanically supported — this is the standard practice.**

* Evidence in the field: `Hocus Focus\` ships `alglib.net.dll`, `ILGPU.dll`, `KdTreeLib.dll`,
  `MathNet.Numerics.dll`, `OpenCvSharp*.dll`, `ScottPlot*.dll`; `Advanced API\` ships `EmbedIO.dll`,
  `Swan.Lite.dll`, `ninaAPI.dll`, `runtimes\`; `Target Scheduler\` ships
  `NINA.Plugin.TargetScheduler.Shared.dll`, `...SyncService.dll`, `EmbedIO.dll`, `Swan.Lite.dll`.
* Helper DLLs are enumerated as plugin candidates (they are `*.dll` in a first-level folder) but are
  rejected silently: `Logger.Trace($"The dll {file} does not reference NINA.Plugin")` (PluginLoader.cs:428)
  or `"...references NINA.Plugin but does not export IPluginManifest"` (line 351). They are never
  copied/deleted by the loader.
* Resolution mechanism, release/3.2.x `PluginLoader.cs`:

  ```csharp
  var context = new PluginAssemblyLoadContext(Guid.NewGuid().ToString(), file);   // line 356
  var assembly = context.LoadFromAssemblyPath(file);                             // line 357
  ...
  private class PluginAssemblyLoadContext : AssemblyLoadContext {
      public PluginAssemblyLoadContext(string name, string directory, bool isCollectible = false) : base(name, isCollectible) {
          this.dllFiles = Directory.GetFiles(Path.GetDirectoryName(directory), "*.dll", SearchOption.AllDirectories);
          this.Resolving += PluginAssemblyLoadContext_Resolving;
      }
      private Assembly PluginAssemblyLoadContext_Resolving(AssemblyLoadContext alc, AssemblyName assemblyName) {
          var dependencyPath = dllFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == assemblyName.Name);
          ... alc.LoadFromAssemblyPath(dependencyPath) ...
      }
  }
  ```
  → each plugin gets **its own collectible ALC**, and a `Resolving` handler that matches **simple
  assembly names, recursively, inside that plugin's own folder**. This is exactly what the 3.2 release
  notes promise (`https://github.com/isbeorn/nina/releases/tag/Version-3.2`, "AssemblyLoadContext"):
  "This change will isolate third-party plugin dependencies so that one plugin having a different version
  of the same third party library will no longer cause issues."
* Consequences to keep in mind (stated precisely):
  * Matching is **by simple name only, first match wins, no version check**; the `Resolving` handler runs
    after ordinary probing, so if N.I.N.A.'s own install directory ever ships the same simple name, that
    copy wins and the plugin-folder copy is ignored. **Checked: the 3.2.1 install dir has 439 DLLs, and no
    `astronomy.dll`** (only `NINA.Astrometry.dll`), so our bundled `astronomy.dll`
    (CosineKitty.AstronomyEngine 2.1.19, 162,816 bytes) does not collide today.
  * Native dependencies / `runtimes\win-x64` are **not** handled by any plugin code path in N.I.N.A.
    (the resolver matches managed simple names only; no `NativeLibrary.SetDllImportResolver`, no
    plugin-dir `SetDllDirectory`). Unconfirmed how the `Advanced API\runtimes\` natives actually resolve;
    we ship no native deps, so this is not our problem unless we add some.
  * `CopyLocalLockFileAssemblies=true` + an explicit file list is the right pattern — our
    `build/Plugin.targets` already says "Do not add NINA.* assemblies or copy the entire build output",
    which matches how `PluginInstaller`/`gather` expect a self-contained folder.
* **Recommended shipping format:** one `Type: "ARCHIVE"` zip containing the plugin DLL **and** its
  private DLLs. `PluginInstaller.InstallArchive` extracts the zip verbatim into the plugin folder
  (`archive.ExtractToDirectory(destination, true)` with a path-traversal guard), so all dependencies land
  next to the plugin DLL where the ALC resolver looks for them. Most 3.2-era catalogue entries use
  `ARCHIVE` (`10 Micron Tools`, `2-Point Polar Alignment`), and our `Package-Plugin.ps1` already does this.

---

## 4. Plugin loading rules (what `PluginLoader` actually does)

* **Folder path is not the app version.** `Constants.cs`:

  ```csharp
  internal static readonly string ApplicationVersionWithoutRevision = GetPluginMinimumApplicationVersionWithoutRevision();
  public static readonly string UserExtensionsFolder = Path.Combine(CoreUtil.APPLICATIONTEMPPATH, "Plugins", ApplicationVersionWithoutRevision);
  ...
  var attribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "PluginMinimumApplicationVersion");
  var version = new Version(attribute?.Value ?? "3.0.0");
  return $"{version.Major}.{version.Minor}.{version.Build}";
  ```
  with `CommonAssemblyInfo.cs:58` `[assembly: AssemblyMetadata("PluginMinimumApplicationVersion", "3.0.0.0")]`
  (verified also inside the shipped `NINA.Plugin.dll`). So the folder is **`%LOCALAPPDATA%\NINA\Plugins\3.0.0\`**
  on 3.0, 3.1, 3.2 and 3.3 alike — it only changes when the plugin ABI floor is raised. Related staging and
  deletion folders: `PluginStaging\3.0.0`, `PluginDeletion\3.0.0`.
* **Scan depth: exactly one level.** lines 314-318:

  ```csharp
  // Enumerate only 1 level deep, where we'd expect plugin dlls to be in the root of the plugin folder
  files.AddRange(userExtensionsDirectory.GetFiles("*.dll").Select(fi => fi.FullName));
  foreach (var subDirectory in userExtensionsDirectory.GetDirectories()) {
      files.AddRange(subDirectory.GetFiles("*.dll").Select(fi => fi.FullName));
  }
  ```
  → DLLs loose in `Plugins\3.0.0\` and DLLs in one level of sub-folder are candidates. Nothing deeper is
  scanned for plugin DLLs (the ALC resolver does scan deeper, but only for dependencies). In 3.2.x the
  list is consumed in enumeration order (`for (int i = 0; i < files.Count; i++)`); no explicit ordering
  is applied on this branch.
* **Two gates before a DLL is considered a plugin:** (1) `GrabAssemblyReferences(file)` must contain an
  assembly reference whose name contains `"NINA.Plugin"` (line 349, substring test on the PE `AssemblyRef`
  table); (2) `PluginAssemblyReader.HasPluginManifestExport(file)` must be true — a **type-level**
  `System.ComponentModel.Composition.ExportAttribute` whose attribute blob contains the string
  `"NINA.Plugin.Interfaces.IPluginManifest"`. Failing either → silent `Logger.Trace`, plugin not loaded.
  (So `[Export(typeof(IPlugin))]` alone is not enough; IPlugin is not the detection contract.)
* Zone.Identifier ADS is stripped from the DLL before loading (lines 344-346) — downloaded files work.
* Then: per-plugin ALC → `AssemblyCatalog` → MEF `ComposeParts` to obtain the `IPluginManifest` →
  compatibility checks → `Compose(plugin, manifest.Name)` pulls in all `[Export]`s (`ResourceDictionary`
  for DataTemplates, `IDockableVM`, `ISequenceInstruction/Condition/Trigger/Container`,
  `IPluggableBehavior`, `IEquipmentProvider`) → `await manifest.Initialize()`.
* Success is logged as: `Logger.Info($"Successfully loaded plugin {manifest.Name} version {manifest.Version} by {manifest.Author}")`.
* **Exact user-visible/trace messages** (release/3.2.x `PluginLoader.cs` 367-388, 420-421 + `Locale.resx`):
  * `The plugin is not compatible with this version of N.I.N.A. as it requires a minimum version of {MinimumApplicationVersion}, but N.I.N.A. is {appVersion}`
  * `This plugin is deprecated.`
  * `The version of this plugin is not compatible with the current version of N.I.N.A. Please update the plugin.`
  * `The plugin is not compatible with this version of N.I.N.A. as it was compiled against {MinimumApplicationVersion} but it has to be built against at least {MinimumMajorVersion}. Please check if there is a plugin update available.`
  * `LblPluginFailedToLoad` = "Failed to load plugin {0} version {1}. Please check if there is an update available." (the thrown exception text + stack is stuffed into the failed manifest's `LongDescription`)
  * progress text: `LblLoadingPlugin` = "Loading plugin {0} version {1} created by {2}"
  * trace-only: `The dll {file} does not reference NINA.Plugin` / `The dll {file} references NINA.Plugin but does not export IPluginManifest`
* **`.pdb` files are inert.** Nothing in the loader looks at them (the scan pattern is `*.dll`; the ALC
  snapshot is `*.dll`); they travel with the plugin and end up in `PluginDeletion\3.0.0\` on uninstall.
  N.I.N.A. itself ships 12 app-dir `.pdb`s for crash symbolication. Keeping `UnphasedNina.pdb` next to the
  DLL is normal (Orbuculum, Moon Angle, Hocus Focus, Connector all do).
* **Installer side:** destination = `Plugins\3.0.0\<sanitized manifest.Name>`; `Installer.Type` `DLL` =
  single file copy, `ARCHIVE` = zip extract; `ValidateChecksum` compares
  `BitConverter.ToString(hash).Replace("-","")` case-insensitively with `Installer.Checksum`
  (MD5/SHA1/SHA256); a mismatch throws "Checksum of file … does not match expected checksum …";
  uninstall moves the whole folder into `PluginDeletion\3.0.0`.
* **Local proof that this gate fires on our plugin:** `%LOCALAPPDATA%\NINA\Logs\20261125-002642-…log` and
  `…003120-…log` contain

  ```
  ERROR|PluginLoader.cs|LoadPlugin|421|Failed to load plugin at C:\Users\zakfo\AppData\Local\NINA\Plugins\3.0.0\Unphased Night Planner\UnphasedNina.dll - Unphased Night Planner version 1.0.0.0
  System.Exception: The plugin is not compatible with this version of N.I.N.A. as it requires a minimum version of 3.3.0.1060, but N.I.N.A. is 3.2.1.3002
     at NINA.Plugin.PluginLoader.LoadPlugin(String file, Double progress) in /_/NINA.Plugin/PluginLoader.cs:line 368
  ```

  An earlier 3.3-targeted build of ours was rejected with exactly this message, and the reported version
  `1.0.0.0` is the `AssemblyFileVersionAttribute`-missing fallback from `PluginBase.cs:43`. Two lessons,
  both confirmed on our own machine: keep `MinimumNinaVersion` at the 3.2 line, and make sure
  `AssemblyFileVersion` is actually emitted.

---

## 5. Version-number rules

**Which version is the plugin version:** `AssemblyFileVersionAttribute` (→ `PluginBase.Version`), *not*
`AssemblyVersion`, *not* any NuGet property. `AssemblyVersion` is only read in the failure-reporting path
(`PluginAssemblyReader.GrabPluginMetaData`). Our `Plugin.props` sets both to `$(Version)` = 0.6.1.0, which
is why the deployed DLL reports `FileVersion 0.6.1.0` (verified with `VersionInfo`).

**Schema/representation:** catalogue `Version` and `MinimumApplicationVersion` are objects with all four
of `Major/Minor/Patch/Build`. The host maps `System.Version` → manifest `PluginVersion` with a deliberate
skew (`PluginVersion.cs`): `Patch = v.Build`, `Build = v.Revision`.

**Compatibility gate (load-blocking)** — `PluginVersion.IsPluginCompatible(minimumApplicationVersion, appVersion)`,
`appVersion = new Version(CoreUtil.Version)` = **FileVersionInfo of NINA's own assembly** (3.2.1.3002 here):

```csharp
if (plugin.Major < minimumApplicationVersion.Major) return true;
if (plugin.Major <= minimumApplicationVersion.Major && plugin.Minor  < minimumApplicationVersion.Minor) return true;
if (... && plugin.Patch < minimumApplicationVersion.Build)    return true;   // note: Patch vs Build
if (... && plugin.Patch <= minimumApplicationVersion.Build
       && plugin.Build  <= minimumApplicationVersion.Revision) return true;  // note: Build vs Revision
return false;
```
Ours (3.2.0.9001 against 3.2.1.3002): `Patch 0 < Build 1` → compatible. A floor of `3.2.0.9001` is
therefore safe for every 3.2.1.x build.

**Second gate — `PluginCompatibilityMap` (release/3.2.x lines 28-98), keyed by plugin GUID** with a
hardcoded `MinimumVersion` per plugin (Orbuculum 1.0.4.0, ExoPlanets 1.1.4.0, DIY Meridian Flip 1.1.1.0,
Smart Meridian Flip 1.0.1.3, Astro-Physics Tools 0.5.0.0, Ground Station 1.11.0.0, Moon Angle 1.3.0.0,
Scope Control 1.2.2.0, **Framing Cache Generator `65535.0.0.0`**) plus:

```csharp
public readonly Version MinimumMajorVersion = GetPluginMinimumApplicationVersion();  // 3.0.0.0
public readonly Version DeprecatedVersion  = new Version(65535, 0, 0, 0);
IsNotCompatible: version(MinAppl) < MinimumMajorVersion
IsUpdateRequired: version(pluginVersion) <= GetMinimumVersion(plugin)   // '<=' : exactly-at-minimum is rejected
IsDeprecated: version(pluginVersion) >= 65535.0.0.0
```
So: `MinimumApplicationVersion` must be ≥ `3.0.0.0` (with the `PluginBase` default of `1.11.0.0` when the
metadata is missing, an unstamped plugin is auto-rejected on 3.x), version parts must stay ≤ 65535
(`65535.0.0.0` is the "self-destruct/deprecated" sentinel), and maintainers can blacklist our GUID with a
minimum plugin version at any time.

**Which version the catalogue/store compares:** `gather.js` performs **no** per-plugin dedup — all
manifest files are concatenated (it only rejects GUID↔Name collisions). The client dedups, in
`NINA/ViewModel/Plugins/PluginsVM.cs` (release/3.2.x):

```csharp
Dictionary<string, ExtendedPluginManifest> onlinePluginDict = onlinePlugins
    .GroupBy(x => x.Identifier)
    .Select(grp => new ExtendedPluginManifest(grp
        .Aggregate((max, cur) => (max == null ||
            PluginVersion.IsPluginGreaterOrEqualVersion(max.Version, cur.Version)) ? max : cur))).ToDictionary(x => x.Identifier);
...
if (PluginVersionCompare(common.Version, p.Version) > 0) { … State = PluginState.UpdateAvailable; }
else { … State = PluginState.Installed; }
```
`PluginVersionCompare` is a plain Major→Minor→Patch→Build comparison. Entries whose
`MinimumApplicationVersion` is not compatible with the running app are already dropped by
`PluginFetcher.RequestAll` (lines 50-51: `IsPluginCompatible(...) && !compatibilityMap.IsNotCompatible(plugin)`).
NuGet-style version ranges, prerelease suffixes, and `AssemblyInformationalVersion` play no role.

**Rules for updating an existing listing:**
1. `Identifier` (GUID) must never change (README line 148); changing it creates a brand-new plugin and
   orphans installs.
2. `Name` must keep matching the GUID: `gather.js validateIds()` fails the whole build with
   `Id collision for manifest! <a> and <b>` when one GUID is used with two different normalized names
   (`name.replace(/\s+/g,'').toLowerCase()`). Renaming a plugin is therefore effectively a re-publish.
3. Either keep one `manifest.json` and overwrite it, or add a per-version folder/file — README line 140:
   "Each version of your plugin should have a separate manifest, or alternatively when you only want to
   support one leading version, one manifest that will be updated constantly with the newest version
   should be maintained." The highest `Version` per Identifier wins client-side.
4. Every release needs a **new immutable download URL** (or the same URL with new content) whose bytes
   hash to the declared `Installer.Checksum`; CI re-downloads and re-hashes changed manifests, and the
   README warns against recompiling after manifest creation.
5. Bump all four parts consistently: `Plugin.props <Version>` → `AssemblyVersion` + `FileVersion` → the
   manifest `Version` (created automatically from the DLL by `CreateManifest.ps1`) → the release tag
   (`X.Y.Z.W`, and our `Release-Settings.ps1` already requires `v0.6.1.0`-style tags with parts ≤ 65534).

---

## 6. Licensing and packaging expectations

* **Policy, from the catalogue README (lines 15-41), not enforced by schema:**

  > "N.I.N.A. is free open source software… an appropriate open source license should be chosen.
  > [MIT]… [BSD-3-Clause]… and [MPL-2.0] are some of the popular choices"

  > "This repository indexes third-party plugins but does not perform legal reviews. Acceptance of a
  > manifest is not an endorsement or confirmation that the plugin's license is compatible with N.I.N.A.
  > or its dependencies."

  By submitting, the submitter confirms "they are authorized to distribute the plugin", "the declared
  license and source information are accurate", "all included code and dependencies may be distributed
  under their applicable licenses", "they authorize N.I.N.A. to publish the manifest and direct users to
  download and install the plugin", and "The plugin publisher remains responsible for copyright and
  license compliance. The N.I.N.A. maintainers may reject, suspend or remove a manifest when a credible
  licensing concern is raised."

  > "**Closed source plugins will not be accepted for the manifest repository and no support in any form
  > will be given by the community.**"

  > "AI-assisted development is allowed, but every plugin must have an accountable human maintainer.
  > Material AI use must be disclosed." … "A manifest may be rejected or removed if the author cannot
  > explain or maintain the plugin, its provenance cannot be established or it consists of unreviewed or
  > low-quality generated output."

* **Mechanically**, the only licence fields anywhere in the pipeline are the two manifest strings `License`
  ("A short name of the license in use (e.g. MPL-2.0, MS-PL, MIT)", maxLength 512) and `LicenseURL`
  ("Link leading to the license text") — plus the same two as `[AssemblyMetadata]`. There is **no SPDX
  expression field, no `PackageLicenseExpression`, and no requirement to put a `LICENSE.txt` in the
  package** (nothing reads it; ours is a courtesy, and keeping it is good practice since we redistribute
  `astronomy.dll`). No licence allowlist exists in code. MPL-2.0 also matches N.I.N.A.'s own licence
  (every source file: "This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0"),
  so our choice is well aligned.
* **README shown in the app? No.** Neither 3.2.x `NINA/View/Plugins/AvailablePluginsView.xaml` nor
  `develop`'s `PluginsView.xaml` binds a README. The in-app text surfaces are
  `Descriptions.LongDescription` (markdown, 3.2: `mdxam:MarkdownScrollViewer`, group hidden when empty) and —
  **3.3/develop only** — the changelog document fetched from `ChangelogURL`
  (`PluginChangelogVM` + `PluginContentCache`, accepting `text/plain`, `text/markdown`, `text/x-markdown`
  or a `.md/.markdown` extension). In 3.2 `ChangelogURL` is only a hyperlink.
* **Screenshots / featured image: optional.** Not in the schema's `required`, and the UI has fallbacks
  (3.2: `DefaultImage` / `NullImageConverter`; develop: an "no screenshots" label). Docs
  (`docs/tabs/plugins/available.md`) list them among what *can* be shown. No documented size/format/https
  constraints — **unconfirmed**.
* **Packaging:** `Type: "ARCHIVE"` + zip (deps + DLL) or `Type: "DLL"` for a single-file plugin. Ship a
  `.pdb` if you want stack traces in the plugin manager's failure text and in user reports — harmless.
  No `manifest.json`, `nuspec`, icon resource, or installer exe is expected.

---

## 7. Where the docs and the source disagree (or are stale)

1. **Author**: docs/templates set both `<Company>` and `<Authors>`, but `PluginBase` reads
   `AssemblyCompanyAttribute` only. `<Authors>` is decorative.
2. **Plugin version**: nobody documents that it comes from `AssemblyFileVersionAttribute` and silently
   falls back to `"1.0.0.0"`; our own log line proves the fallback is real.
3. **Missing `MinimumApplicationVersion`**: `PluginBase` defaults it to `1.11.0.0`, which `PluginCompatibilityMap`
   then rejects (`< 3.0.0.0`). No doc mentions this trap.
4. **Folder pattern**: README writes `manifests\<plugin starting letter><your plugin name>\…`, which reads
   as one folder; actual entries use a one-letter folder **and** a plugin-name folder, and both
   `manifest.json` and `manifest.<version>.json`, both `3.2.0` and `3.2.0.9001` version folders.
5. **README typo**: `./tools/github-action.yml` → the file is `tools/github-action.yaml`.
6. **"Plugin folder = NINA version"**: users and the docs UI text (`LblPluginDllInstallDescription`)
   suggest `%localappdata%\NINA\Plugins\<something>`; the `3.0.0` segment is the **plugin ABI floor**
   (`PluginMinimumApplicationVersion`), not the app version — it is `3.0.0` on 3.2 and 3.3.
7. **Official template targets 3.3, not 3.2**: `isbeorn/nina.plugin.template@master`
   (`templates/nina-plugin/`, single commit `67e869fb`, 2026-09-28) pins `NINA.Plugin 3.3.0.1060-nightly`
   / `net10.0-windows`; there is no 3.2-pinned modern template state. For 3.2 we correctly use
   `NINA.Plugin 3.2.0.9001` + `net8.0-windows` (the `Version-3.2` tag's `NINA.Plugin.csproj`), and the
   legacy VS template still carries a stale `3.0.0.2017` baseline.
8. **`ChangelogURL` rendering** is documented generically ("homepage and changelog links"), but only
   3.3/develop fetches and renders it; 3.2 shows a link only.
9. **Beta `Channel`** is documented in README but absent from both the JSON schema and the C# model, so
   any server-side validation of it is unverified.
10. **The 3.2 release notes** promise dependency isolation and "No changes to existing plugins are
    required" — true in code (`PluginAssemblyLoadContext` with a plugin-folder `Resolving` handler), but
    the note does not mention that resolution is **simple-name-only with first-match-wins** and that the
    app directory is probed first.

---

## 8. What our plugin still needs to be publishable

Ordered; ✔ = already satisfied and verified, ✖ = missing (blocker), ⚠ = recommended, ? = unconfirmed.

1. ✖ **Public source repository with a real URL.** `Plugin.props <RepositoryUrl>` is still the
   placeholder `https://github.com/zakky2k/unphased`, and the README forbids closed source ("Closed
   source plugins will not be accepted…"). The repo must contain `UnphasedNina` + `UnphasedCore`
   (+ tests/build scripts), a `LICENSE` (MPL-2.0) file, and MPL-2.0 headers or a clear statement that the
   whole plugin is MPL-2.0. `Repository` (manifest, required) and `[AssemblyMetadata("Repository")]` must
   both point at it.
2. ✖ **Dependency licensing attestation.** The submission attests that "all included code and dependencies
   may be distributed under their applicable licenses". We redistribute `astronomy.dll`
   (CosineKitty.AstronomyEngine 2.1.19) and our own `UnphasedCore.dll`. ⚠ Verify AstronomyEngine's actual
   licence and add a `THIRD-PARTY-NOTICES` note next to `LICENSE.txt`. ? AstronomyEngine 2.1.19's licence
   was not verified in this pass.
3. ✖ **Human-readable metadata that is not placeholder.** Replace `Description` = "A NINA plugin." with a
   real ≤256-char `ShortDescription`; fill `PluginLongDescription` (≤10000, rendered as markdown in 3.2 —
   this is *the* store page body, since READMEs are never shown); set `<Company>` (already `zakky2k`, that
   is what becomes `Author`); add `<Copyright>` (currently empty; harmless but expected).
4. ✖ **`Tags`, `PluginHomepage`, `PluginChangelogUrl`.** Tags ≤20 items, each ≤30 chars (comma-separated in
   `PluginTags`). Create a `CHANGELOG.md` in the public repo and point `PluginChangelogUrl` at a raw URL
   (rendered in 3.3, link-only in 3.2 — hence still worth setting).
5. ⚠ **Images**: set `PluginFeaturedImageUrl` + one/two `PluginScreenshotUrl`/`PluginAltScreenshotUrl`,
   hosted at stable raw URLs (precedent: Quick Actions uses `raw.githubusercontent.com` image paths).
   Optional per schema; strongly helps review acceptance and click-through. ? Format/size/https
   requirements undocumented.
6. ✖ **AI-use disclosure + named human maintainer** (README policy: "Material AI use must be disclosed",
   "every plugin must have an accountable human maintainer"). Add a short section to the public README.
   Precedent in the catalogue: Quick Actions ends its `LongDescription` with "Programmed by Claude Code
   (Anthropic) on Tomas Dvoracek."
7. ✔ **Load-time identity is already correct** (verified in the deployed DLL's strings and in
   `src/UnphasedNina/Plugin.cs`: `[Export(typeof(IPluginManifest))] public class Plugin : PluginBase`):
   GUID present, `Name` = "Unphased Night Planner" (≤50), `FileVersion` 0.6.1.0, `Company` zakky2k,
   `License`/`LicenseURL` MPL-2.0, `MinimumApplicationVersion` 3.2.0.9001. Keep the GUID and the Name
   frozen forever.
8. ✔ **Dependency bundling approach is right**: `astronomy.dll` + `UnphasedCore.dll` next to the plugin DLL
   works via the per-plugin collectible ALC (`SearchOption.AllDirectories`, simple-name match); `astronomy.dll`
   does not collide with the NINA install dir (verified). Keep `Type: "ARCHIVE"` zips so the deps land in the
   plugin folder. ⚠ Because matching is simple-name + app-dir-first, watch for future NINA releases that
   might ship an `astronomy.dll`; re-check on each NINA update.
9. ✖ **A real, immutable release with a matching checksum.** Run `Build-Release.ps1`/`Package-Plugin.ps1`
   with `-InstallerUrl` pointing at the GitHub-release asset of a tag matching
   `[0-9]+.[0-9]+.[0-9]+.[0-9]+` (ours: `v0.6.1.0`), so `Installer.URL`/`Checksum(SHA256)`/`ChecksumType`/
   `Type=ARCHIVE` are real (currently `Package-Plugin.ps1` falls back to
   `https://example.invalid/releases/…` when `-InstallerUrl` is omitted — that URL would fail CI).
   Do not rebuild the zip after hashing (README warning + `validate-latest-manifest.js` re-hashes it).
10. ✖ **CI mirror of the official action (optional but recommended).** Copy
    `isbeorn/nina.plugin.manifests/tools/github-action.yaml` into the plugin repo with
    `PLUGIN_NAME`, `PLUGIN_SLN_NAME`, `PLUGIN_DLL_NAME=UnphasedNina`, `PLUGIN_MANIFEST_PATH="u/Unphased Night Planner"`
    and secret `PAT`, so every tag regenerates the manifest and opens the catalogue PR.
11. ✖ **Submit the manifest.** Fork `isbeorn/nina.plugin.manifests`; place the file at
    `manifests/U/Unphased Night Planner/3.2.0.9001/manifest.json` (optionally `…/0.6.1.0/manifest.json`);
    run `npm install` + `node gather.js` locally (or `Test-Json` against the vendored schema, which our
    packaging already does); open a PR and answer review feedback. Consider starting with
    `"Channel": "Beta"` and promoting later.
12. ✖ **Prove it loads on 3.2.1.3002 with the *current* build.** The only recorded plugin-load attempts in
    `%LOCALAPPDATA%\NINA\Logs` are the two 2026-11-25 early-morning failures quoted in §4 (old 3.3-targeted
    build). Start N.I.N.A., then confirm the log line
    `Successfully loaded plugin Unphased Night Planner version 0.6.1.0 by zakky2k` and that the dock panel
    appears. ⚠ Also re-check the single `System.Reflection.TargetParameterCountException` recorded at
    18:19 on 2026-11-25 (after our rebuild) — attribution unknown; ? unconfirmed whether it involves us.
13. ⚠ **3.3 plan.** Our `net8.0-windows` + `NINA.Plugin 3.2.0.9001` build is correct for 3.2.x. The template
    and 3.3 (`develop`, `net10.0-windows`, `3.3.0.1060+`) will need a second build line; the catalogue has 8
    `3.3` manifest folders already, and `MinimumApplicationVersion` is what keeps the two apart. ? Whether a
    net8-targeted plugin would load under a net10 host was not tested here.
14. ⚠ **Versioning discipline for future releases**: bump `Plugin.props <Version>` (4 parts, ≤65534 per our
    own guard, never 65535 which means "deprecated"), keep `AssemblyVersion` == `FileVersion`, publish a new
    GitHub release asset, and let `CreateManifest.ps1` derive the manifest from the built DLL so
    `Identifier`/`Name`/`Version` can never drift from the assembly.
