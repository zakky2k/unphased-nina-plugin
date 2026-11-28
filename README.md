# Unphased Night Planner

**Visualise darkness, twilight and moonlight with altitude overlays for the Moon, planets and deep-sky targets across consecutive nights in a single interactive astronomical chart.**

**[Download the Windows plugin ZIP · v0.8.3.0 Beta](https://github.com/zakky2k/unphased-nina-plugin/releases/download/v0.8.3.0/UnphasedNina.0.8.3.0.zip)**

Requires N.I.N.A. **3.2.0.9001 or newer**. This beta is available as a manual install.

1. Close N.I.N.A. and extract the ZIP directly into
   `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Unphased Night Planner\`.
2. Check that `UnphasedNina.dll` sits directly inside that folder, then reopen N.I.N.A.
3. In **Imaging**, use the puzzle-piece button in the Info bar to open the planner.
4. Custom Horizon feature requires prior setup in N.I.N.A.

[All releases](https://github.com/zakky2k/unphased-nina-plugin/releases) · [Changelog](CHANGELOG.md) · [Build and test](#build-from-source)

## Three perspectives in one view of the night.

### Lunar and planetary phases

<img src="assets/pillars/lunar-phases.svg" width="64" height="64" alt="A crescent Moon with its trajectory across the horizon">

Quick, visual guide of the altitude curves and phases of the Moon, Mercury and Venus for tonight, up to any 7 days in the past or future.
Identify the darkest periods of the upcoming nights when the Moon is below the horizon.


*Understand the Moon and inner planets.*

### Astronomical darkness

<img src="assets/pillars/astronomical-darkness.svg" width="64" height="64" alt="A setting Sun above the twilight bands, falling below the horizon">

Visualise the transition from daylight through civil, nautical and astronomical twilight into
astronomical darkness.
Easily identify when darkness begins, when it ends and how much dark imaging time is available throughout each night.
Assess potential moonlight interference and discover moon-free imaging opportunities for your specific horizon (using custom horizon from N.I.N.A).

*Understand the darkness.*

### Multi-night visualisation

<img src="assets/pillars/multi-night-visualisation.svg" width="64" height="64" alt="A calendar carrying three astronomical altitude curves">

Look beyond tonight and explore how astronomical darkness and the Moon evolve across consecutive
nights. Compare upcoming nights and identify favourable windows for future astrophotography
sessions.
Fast scrubbing through the nights ahead to visualise the evolution of the phases, altitude curves and longer term trends.

*Plan the nights ahead.*

---

Ephemerides come from [CosineKitty.AstronomyEngine](https://github.com/cosinekitty/astronomy)
through `UnphasedCore`, evaluated for the observatory in your N.I.N.A. profile. The planner
visualises astronomical conditions independently of weather forecasts, atmospheric transparency
and local light pollution.

## Options

**Observatory location** uses the active N.I.N.A. profile by default. To plan for another
site, enter its coordinates in decimal degrees and select **Apply location**.
Select **Use N.I.N.A. profile location** to return to the profile location.

**Display palette** offers **N.I.N.A. theme**, **Night vision** and **High contrast**.
N.I.N.A. theme follows the host application, Night vision uses red tones, and High
contrast makes chart elements easier to distinguish.

**Custom chart colours** lets you set the colours for solar altitude, lunar altitude and
the horizon. Select **Restore default colours** to use the palette colours again.

To use **Custom horizon**, configure a horizon in your active N.I.N.A. profile.

## Build from source

Use Windows, the .NET 10 SDK specified in [`global.json`](global.json) and the .NET 8
Windows Desktop Runtime. Run both commands from the repository root.

```powershell
dotnet build UnphasedNina.slnx
dotnet test UnphasedNina.slnx
```

[Contributor guide](CONTRIBUTING.md) · [Maintainer release guide](docs/RELEASING.md)

## Maintenance and disclosure

**zakky2k** maintains the plugin and is responsible for reviewing, testing and approving
changes. Development has been materially assisted by Anthropic’s Claude, Qwen3.8-FlashNext
and ChatGPT/Codex.

## Licence

Unphased Night Planner is licensed under [MPL-2.0](LICENSE.txt). See
[Third-party notices](THIRD-PARTY-NOTICES.txt) for redistributed code and its licences.
