# HANDOFF — Unphased Night Planner (N.I.N.A. plugin), state at 2026-11-26 (round 8 delivered, version 0.7.0.0)

For the next session. Parent repo `AstroScripts` (remote `github.com/zakky2k/AstroScripts.git`, main).
The tip of `main` is round 8: all seven planned items implemented in the planned order, **one commit each, suite
green after every one (92 passing), pushed each time**, with the 0.7.0.0 release commit on top of `ccc93a6`. The
start-up crash that **0.6.0.0** shipped with was undone in 0.6.1.0 (`TargetParameterCountException`, sixth pass).
Round 8's five open questions were answered by the user **before** implementation — three header chips; the chart
element asks for 320 DIP; Enhanced is chart-only; refraction stays geometric 0°; no extras — so they are settled and
should not be re-asked. Read **"Round 8 delivered"** below: it records what each item became, the new test-harness
laws round 8 produced, and what round 9 should start with.
**0.7.0.0 is deployed and the round-8 review is closed** (deployed 00:55 on 26 Nov, verified; the user looked at it in
N.I.N.A. and accepted it, with one carry-over item about button sizing — recorded under "Round 9 starts here").
What is left of the release is packaging: `Build-Release.ps1` has not been run for 0.7.0.0, the GitHub release and the
catalogue manifest PR are still to do, and there are still no hosted screenshots.
Root `AstroScripts/HANDOFF.md` covers the web planner; this file covers only `nina-plugin/`.

## What exists and works
A dockable panel inside N.I.N.A. 3.2.1.3002: **Imaging page → the puzzle-piece button in the Info bar**
(not Options). Header row `‹  [DatePicker]  ›` with the night's caption beside it ("Tue 24 Nov → Wed 25 Nov 2026"),
then zoom `-` / `+` with the frame written between them (1 night … 7 nights), the hour-scrub buttons `< hr` `hr >`
`off`, and three palette buttons `default` `night` `enhanced` with a `palette: …` readout. Three chips under the
caption carry the highlighted night's figures (dark window, length, where the Moon is). **The night list and the
Refresh button are both gone** — the calendar and the planner's own window growth replaced them. The chart is 320 DIP
tall, ruled every 15° and numbered every 30° **on both edges**, +90 at the top down to −24. Options → plugin page:
latitude/longitude drafts behind **Apply/Reset**, three colour **swatch buttons** with the exact value in the tooltip,
and a reset. Status line reads `Site 50.0880, 14.4208 - window 31 nights`, widening by ten nights at a time as the
calendar nears an edge (profile `842b5009-…`, Prague). All of this is verified **in-process** — real data, templates
built and rendered in tests, plus wiring tests — **and 0.7.0.0 was then looked at inside a running N.I.N.A. and
accepted** on 26 Nov; the deployment evidence is under "Round 8 delivered".

Layout of `nina-plugin/`: `UnphasedCore` (C# port of the planner's astronomy: `NightCalculator.cs`, `Models.cs`),
`UnphasedCore.Tests` (accuracy vs the web fixtures Prague/summer/Sydney), `UnphasedNina/` — `src` (Plugin.cs,
`Features/planner/{Plugin,VM,chart}`, `Features/settings`), `tests`, `build` (Stage/Deploy/Package/Verify +
`build/vendor/CreateManifest.ps1`), `probe` (payload compared byte-wise to the web `probe/night-*.json`),
`reflect` (scratch reflection dump over NINA assemblies).

## Commands (all proven this session)
- Tests: `dotnet test AstroScripts\nina-plugin\UnphasedNina\tests\UnphasedNina.Tests\UnphasedNina.Tests.csproj -c Debug -f net8.0-windows --nologo` → **92 passed** (~7 s). Run it from `Workspace`, not from inside `AstroScripts`: the path in the command is prefixed, and from the wrong directory MSBuild reports `MSB1009: Project file does not exist`.
- Release packaging (proven on 25 Nov, nothing uploaded):
  `pwsh -File AstroScripts\nina-plugin\UnphasedNina\build\Build-Release.ps1 -Tag v0.6.1.0 -Repository zakky2k/AstroScripts -Channel Beta`
  → `UnphasedNina/artifacts/release/UnphasedNina.<ver>.zip` + `.manifest.json` (schema-validated, SHA256, `Type: ARCHIVE`,
  `Channel: Beta`); re-check with `build\Verify-ReleaseArtifacts.ps1 -Directory … -Tag … -Repository …`. Without
  `-Repository`/`-Tag` the installer URL falls back to `https://example.invalid/releases/…`, which the catalogue CI rejects.
- **Visual smoke (this is how chart work gets verified here)**: same command plus
  `--filter "FullyQualifiedName~PlannerChartRenderSmoke"` → writes `%TEMP%\planner-preview-<day>-z<night>-at-<w>x<h>-scrub<…>.png`,
  one per palette (`-night.png`, `-enhanced.png`), then `read_image` them. The test is `[Explicit]`, so it never runs
  in the normal suite, and it draws with **stand-in theme brushes** because a unit-test host has no N.I.N.A. theme
  dictionaries.
- **Whole-panel smoke**: `--filter "FullyQualifiedName~PlannerPanelRenderSmoke"` → `%TEMP%\planner-panel-at-620.png`
  and `-at-1200.png`: the real data template, the real dockable VM, a real 31-night window computed for Prague. Run it
  after touching `Features/planner/Resources.xaml`. Same caveat, and a sharper one: without N.I.N.A.'s theme
  dictionaries the button labels are barely visible and the `DatePicker` shows light stock chrome, so these PNGs prove
  layout and bindings, not the look inside N.I.N.A.
- Deploy: close N.I.N.A. first (it holds a lock on the DLLs), then
  `dotnet msbuild "AstroScripts\nina-plugin\UnphasedNina\src\UnphasedNina\UnphasedNina.csproj" -t:DeployPlugin -v:m -nologo`
  → `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Unphased Night Planner\`. Verify with `Get-FileHash` against
  `AstroScripts\nina-plugin\UnphasedNina\artifacts\stage\Debug`. The script prints
  `Compatibility check: plugin needs N.I.N.A. 3.2.0.9001, installed app is 3.2.1.3002.` and refuses with
  `Cannot replace '…astronomy.dll'. Close NINA and retry.` when the app is running.
- Log check: newest `%LOCALAPPDATA%\NINA\Logs\*-3.2.1.3002-*.log`, grep `ERROR` and `not compatible with this version`.

## Chart laws (do not regress)
- Abstract canvas `NightChartVM.Width=1000 / Height=320`; payload frame is **local noon → +48 h**
  (`FullFrameMinutes=2880`). X is `XAt(payload,t)`, Y is
  `YSun(alt) = (TopAltitude-alt)/(TopAltitude-BottomAltitude)*320` with **`TopAltitude=90 / BottomAltitude=-24`**.
  History, because this axis has been changed three times: +80/-32 (0.2.0.0, my call) → ±90 (0.3.0.0, the user asked
  for it back) → +90/-24 (0.4.0.0, the user corrected themselves: the *zenith* is what must stay, and the below-
  horizon half only needs enough room for the −6 / −12 / −18° labels). −24 puts the horizon just above mid-height,
  leaves ~17 px between degree chips at a 360 px row and ~13.6 px at 260 px — which is why the de-stacking walk is
  still load-bearing. **`YMoon == YSun`** — one shared altitude domain, so nights are comparable. Changing the band
  changes every Y-dependent test, and **ask the user before touching the axis**: they have corrected this twice.
- Curves are **cut at the window edge, never clamped to it**: `NightChartVM.TrimmedPolyline(payload, series, yOf)`
  emits points separated by spaces and separate drawable runs separated by `" | "`, with the boundary crossing
  interpolated so a run ends exactly on the edge. `NightChartControl.DrawCurve` starts a fresh figure per run.
  Clamping every sample instead drew a flat horizontal line along the bottom of the panel — asserted against by
  `CurvesStopAtTheWindowEdgeInsteadOfRunningAlongIt`.
- The **scrub marker** is state on `NightPlannerVM`, not on the chart control: `ScrubMinutes` (minutes from frame
  noon, snapped to whole hours by `NightChartVM.SnapToHour` with `MidpointRounding.AwayFromZero`, clamped to
  `VisibleSpanMinutes`, which follows the **zoom** — `FrameMinutesAtZoom(zoomNights)`), plus `SetScrub` /
  `StepScrub` / `ClearScrub` and the `ScrubLabel` readout. Commands `ScrubBack/ScrubForward/ScrubClearCommand` on
  `PlannerDockableVM`; input on the chart is click-drag, wheel, ←/→, Home/End, Escape.
  `NightChartControl.SetScrub(double?)` is the drawing mirror so the control renders in tests without a planner.
  Altitudes come from `NightChartVM.AltitudeAt` (interpolated, NaN outside the frame). The moon marker on the scrub
  line is the **phase glyph**, not a dot — `NightChartVM.ScrubMoonGlyph(minutes)` returns `(illum, litOnRight)`
  (illumination interpolated over `Payload.MoonSamples`, limb from `Payload.Limb`) and the control draws it on a
  small dark disc so it reads over a label. It is skipped when the body is outside the plotted window; the readout
  deliberately is not clamped, so it can still name an altitude that is off-canvas.
- The **frame is chosen by the panel, not by the payload**: `NightChartVM.MaxZoomNights=7`,
  `FrameMinutesAtZoom(nights) = clamp(nights,1,7) * OneNightMinutes`, and `HourGridMinutes(frameMinutes)` →
  4 h ≤2 nights, 8 h ≤4, else 12 h. `sx = w / Width` always maps the *frame* onto the panel; the abstract canvas
  is never stretched by the span. `new NightChartVM(payload)` deliberately keeps the payload's own 48 h frame, so
  a bare-payload chart still shows everything it carries — the panel always passes a frame explicitly.
- Wider frames are stitched **in the plugin** by `NightFrame.For(payloads, nights)`: one day-slice per consecutive
  payload, each cut on *that payload's* `FrameNoon` (so DST seams stay exact), half-open except the last, moon
  rise/set deduplicated at the seams. The guard measures the **gap** between successive `FrameNoon`s (12..36 h is
  a sequence); comparing against an expected noon measured 0 h and silently cut the frame to one day. A payload
  for a night that dropped out stops the stitch instead of bridging the hole. Core is untouched: it is shared with
  the web planner and has fixtures.
- **Nothing may draw past the frame edge.** `NightChartVM.WithinFrame` cuts the sun/moon series at both frame
  edges with interpolated crossings (so `SunPolyline`/`MoonPolyline` only contain x in 0..Width) and
  `ShadingRuns(payload, frameMinutes)` clamps each run to the canvas before merging. WPF does **not** clip
  `OnRender` output, so before this a 1-night chart painted 48 h of curve, colour and chips off the right end of
  the panel. Asserted by `NothingLeaksPastTheFrameEdge`.
- Horizon event labels come from the **plotted curve**, not the payload's event list:
  `NightChartVM.SunCrossings()` interpolates every crossing of the trimmed sun polyline, so each night *in the
  frame* gets its own sunset/sunrise and nothing off-frame gets one. `moonrise`/`moonset` still come from the
  payload (they were already per-frame events).
- **Labels step around each other**: `DrawGrid` records the boxes its own axis labels occupy (`axisChips`) and the
  event chips skip any box that would intersect one, per band (`Fits`). A clock label is never slid sideways off
  its gridline — if its gridline has no room before the degrees column, the label is left out and the line stays.
- Moon track runs the **whole frame** like the sun curve (`MoonPolyline` filters nothing). The phase is written
  **only on the scrub marker** — the static glyph and the `NN% moon at NN°` chip at `NightChartVM.NightMoonPeak()`
  were removed on 25 Nov (fourth pass: "lets keep this as main glyph and remove the static glyph and chip").
  `NightMoonPeak()` itself stays, because tests use it; the readout gets the phase from `NightChartVM.MoonIllumAt(t)`.
- Glyph: `NightChartControl.MoonPhaseGeometry(cx,cy,r,illum,litOnRight)` — public only so it can be unit-tested.
  Lit limb semicircle + terminator half-ellipse of half-width `r*|1-2f|`; gibbous bulges into the dark side,
  crescent into the lit side; `litOnRight` comes from `payload.Limb` ("left" ⇒ lit on the left), so southern
  hemispheres mirror correctly. New moon = outline only, full moon = solid disc.
  WPF trap: `StreamGeometryContext.ArcTo` takes **7 arguments** including a `SweepDirection`; `Sweep(bool)` is the helper.
- Twilight background = `PlannerPalette.Twilight(dark, sunAltitude)`, four alpha overlays for >0 / 0..-6 / -6..-12 /
  -12..-18, nothing below -18 (the bare chart background is the darkest layer). Red night-vision variants exist;
  both ladders are asserted distinct by `TwilightPaletteFollowsTheStandardAltitudeBands`.
- The bands are painted as **runs, not samples**: `NightChartVM.ShadingRuns(payload)` (pure, testable) merges
  consecutive samples in the same `PlannerPalette.TwilightBand` into one `(x0, x1, band)` rectangle with exact
  shared edges. One hairline rect per ~10-minute sample plus a `+0.5` overlap fudge is what produced the comb of
  "vertical lines that don't represent anything" in the daylight part — never go back to it, and keep the
  rect-count test (band transitions + 1) green.
- Legibility consts live at the top of `NightChartControl`: `LabelFs=10` (degrees, hour clock), `MarkerFs=11`
  (sunset/sunrise/moonrise/moonset), `NoteFs=12` (the scrub line's time / altitude / phase readout), `LegendFs=11`,
  `TipFs=13`, `MoonGlyphRadius=9.0` — one constant, the dark disc behind the glyph is drawn at `+2.0`, which is also
  the collision patch for labels underneath it. **Every** label goes through the chip helper (fill `PlannerPalette.ChipFill`, a
  translucent near-black, with a hairline border in the label's own ink) — plain text over the daylight band is
  unreadable. Keep `moonrise`/`moonset` worded **above** the horizon crossing and `sunset`/`sunrise` **below** it
  (`NightChartVM.ChipAbove` / `ChipBelow`, which flip to the other side rather than let a label leave the canvas),
  and the four degree labels walked top-to-bottom so they can never stack in a short panel. No full-height vertical
  marker lines for those events — they are the lines "that dont seem to represent anything", and the scrub line is
  now the only vertical that moves.
- The 0° horizon has its own pen: 1.6 px `PlannerPalette.Horizon(dark)`, drawn after the 0.6 px twilight lines.
  **Default is green** (`#4FC87C`), not the theme's red — the user asked for it, because a red horizon sat on top of
  the red moon track. Night mode keeps a red horizon (`HorizonDefault(dark)`).
- Every chart colour goes through a **three-way precedence: picked hex › scheme default › theme lookup**.
  `PlannerPalette.SetOverrides(sun, moon, horizon)` is fed from the options by `NightChartControl.ApplyChartColors()`
  (hooked in `HookPlanner()`, re-applied on `PlannerChanged`); `ParseHex` accepts `#rgb`/`#rrggbb`/`#aarrggbb` and
  returns **null** for empty or nonsense, so a bad field degrades to the palette instead of throwing in `OnRender`.
  Hour gridlines follow `HourGridMinutes` (4 / 8 / 12 h by zoom) and the degrees column is drawn **before** the
  clock row, because the clock labels have to know how much of the right edge is already taken.
- Row label shows the **evening** date; navigation and the header use the **morning** date (`MorningDay = EveningDay+1`).
- Build is **net8.0-windows** with `NinaPackageVersion 3.2.0.9001`. Do NOT modernize to net10 / 3.3 nightly:
  N.I.N.A. 3.2.1.3002 then rejects the plugin with `requires a minimum version of 3.3.0.1060`.

## Round of 25 Nov — all five review items delivered in `6c240ce`
Kept verbatim as the record of what was asked:
> *"glyph and text in the actual chart are too small, increase font size a couple of points and glyph by 50%.*
> *including words moonrise and moonset.*
> *if possible make the test into chips with small tight bounding box around the labels and a black background inside the box.*
> *The horizon line at 0 deg alt should be bolder and maybe slightly thicker. each parallel line below horizon and horizon itself should have degree label*
> *too many verticle lines, that dont sem to represent anything, mainly present during the daytime parts of teh graph"*

Where it landed: `NightChartControl.cs` (font consts, the chip helper, the horizon pen, moonrise/moonset wording
at the horizon crossing), `NightChartVM.cs` (`ShadingRuns`, the +80/-32 altitude band), `PlannerPalette.cs`
(`Horizon`, `ChipFill`, `TwilightBand`), plus four new assertions in `PlannerChartTests.cs`. The "Chart laws"
section above is the outcome. Two calls made without asking, both worth re-checking with the user: chips are
translucent near-black `#E6000000` rather than literally solid black (solid black hid the chip edge on the dark
theme), and the altitude window went from ±90 to +80/-32 — that narrowing is what actually made the twilight
labels readable, but the user has not seen it yet and may want it back.

Standing verify order for chart work: unit tests (expect **51**) → the `[Explicit]` smoke render (nine frames: one
night at 1200 and 720 px, two- and three-night frames, a week at 1200 and at 620×260, plus scrubbed frames — a near
full moon at 01:00, a 24 % crescent at 13:00 to prove the phase glyph on the scrub line, and a three-night scrub) →
`read_image` → ask the user to close
N.I.N.A. → `-t:DeployPlugin` → `Get-FileHash` against `artifacts\stage\Debug` → relaunch → log check. Re-run the
deploy after any commit that touches `Plugin.props`: the deployed DLL kept reporting 1.0.0.0 through two chart
deploys because the staged build predated the stamp.

## Round of 25 Nov (second pass) — ±90 back, edge-cut curves, scrub marker → `0.3.0.0`
Asked verbatim:
> *"actually we must keep the 90 degree altitude visibility"*
> *"For sunset and sunrise place them similar as the moon chips but under the horizon instead."*
> *"stop drawing the moon and sun path lines if they reach the bottom of the graph, instead of having a flat horizontal line."*
> *"no need for the dashed vertical red line as it exists currently, i was hoping to get a scrubbing mechanism to move it hour by hour"*

The ±90 revert was the correction of an unasked-for decision from the previous round — that is the second time a
convenience change to an axis surprised the user, so chart-axis changes get asked about, not assumed. Not yet
verified inside N.I.N.A.: the scrub *input* (drag / wheel / keys) has no automated coverage, only snapping,
clamping and the readout do, and the readout chip can land on the right-hand degree labels when the marker is near
the right edge. Next round: watch for that overlap and for whether the wheel-steals-scroll complaint appears.

## Round of 25 Nov (third pass) — −24 floor, scrub phase glyph, zoom to a week → `0.4.0.0`
Asked verbatim:
> *"the scrubbing mechanism is perfect and love that i can click and drag, great implementation."*
> *"it seems i may have misspoke... There's no need to display the full -90deg altitude, only the +90deg alt, for the portion below the horizon what was displayed previous was more than enough, and could be even reduced a bit further just enough to ensure the -6, -12 and -18 degrees are visible."*
> *"for the moon can the same glyph currently display that changes in function of the phase be use instead of just the dot when scrubbing?"*
> *"new requirement. for some reason when switching to plugin the display cover 2 nights instead of just one on the desktop app. lets have a zoom button than can be used to scale from one night through to a week (or what maximum you feel is suitable for testing)"*

Item 1 needed nothing. Item 2 was the axis change the user initiated, so it went ahead (−90 → −24, zenith kept);
the four degree chips were then measured on the canvas (horizon y≈252.6, −6 → 269.5, −12 → 286.3, −18 → 303.2)
rather than assumed. Item 4 turned out to be a *frame* problem, not a rendering bug: the payload has always carried
noon → +48 h, and until now the chart drew all of it. Hence `MaxZoomNights=7`, `NightFrame.For` and the frame-edge
clipping law above. `-` means fewer nights and `+` more, matching the label between them.

Discovered while testing this round, worth remembering: **the 1-night frame is anchored at local noon**, so the
sun's daily maximum sits exactly at the left and right edges and the daylight arc reads as a shallow dip. Correct,
but the first thing the user will notice at 1-night zoom — the open question is whether to anchor a single-night
frame sunset → sunrise instead (the desktop app's one-night view may do that; ask before changing it).

## Round of 25 Nov (fourth pass) — green horizon, colour pickers, panel-only night mode → `0.5.0.0`
Asked verbatim:
> *"The nights + button works to expand the chart. 7 nights max seems acceptable."*
> *"The nights - button is greyed out and non responsive, not possible to reduce the chart size"*
> *"lets adjust how many nights are pre-loaded lets add additional 10 in the future, keep past the same."*
> *"i dont like the horizon line as red. lets make it green."*
> *"i love the scrubbing moon glyph, lets keep this as main glyph and remove the static glyph and chip showing at the maximum altitude"*
> *"in the setting page, add color picker for the chart elements, sun path, moon path and horizon, a reset to defaults button (reset colors and take from nina environment palette)"*
> *"is it possible for a night mode button (with a default red color palette, that is the same as the desktop app) be used to affect only the plugin panel in the imaging tab ? i.e. my question does nina allow custom colors to be used and override the app's color palette ? if yes, then introduce a dark mode button."*

Item 1 needed nothing. Item 2 was **not** a chart bug: `+` worked, so `zoomNights` was changing; the `-` button's
`CanExecute` simply was never re-queried in a dockable pane. Fix is in two places on purpose — the view model now
exposes `CanZoomIn` / `CanZoomOut` (INPC) and the XAML binds `IsEnabled` to them ("the braces"), while the command
also calls `CommandManager.InvalidateRequerySuggested()` ("the belt"). Bind enabled-state to properties, never rely
on `CommandManager` requery, in this panel.

Item 7 was a question, and the honest answer is **half yes**: a plugin can define its own `ResourceDictionary` and
shadow N.I.N.A.'s brush keys (`BackgroundBrush`, `PrimaryBrush`, …) for its own subtree, because 1549 of N.I.N.A.'s
brush references are `StaticResource` resolved per-element — but it **cannot** recolour N.I.N.A.'s own implicit
control styles, which bound the palette when the application started. So: the panel's text, chrome and chart go red;
N.I.N.A.'s button *bodies* stay the app's colours (only their labels follow). Merging the dictionary at application
scope would affect every tab, which is the opposite of what was asked. N.I.N.A.'s own app-wide toggle is the eye
button in the title bar (`OptionsVM.ToggleColors()` → `ColorSchemaSettings.ToggleSchema()`, swapping
ColorSchema ↔ AltColorSchema). There is **no theme XAML in N.I.N.A. at all** — the 18 palettes are hard-coded C#
`ColorSchema` objects in `NINA.Core/Utility/ColorSchema/ColorSchema.cs`, and the red night-vision scheme the user
means is the one N.I.N.A. calls **"Dark"**, the shipped default `AltColorSchema`. Its verified hexes are copied into
`PlannerPalette.NightChrome`; note `Secondary` is navy (`#1B2A41`), not red, while `Primary`/`Border` are `#550C18`.
Read-only source snapshot used: `%TEMP%\nina-src-32\nina-Version-3.2` (repo `github.com/isbeorn/nina`, tag `Version-3.2`).

Mechanics worth keeping: `PlannerViewGlue.NightPalette` is an attached property that **assigns a fresh owned
`ResourceDictionary`** to the panel root — `FrameworkElement.Resources` is *inherited*, so mutating the value you get
back would recolour ancestors. The plugin's own XAML uses `DynamicResource` for brushes, which is what lets the
shadow take effect; `FrameworkElement.ResourcesProperty` does not exist, so "is this dictionary ours?" cannot be
asked that way.

## Round of 25 Nov (fifth pass) — one settings object, honest zoom buttons, figures per run → `0.6.0.0`

The user's list, verbatim:

1. "color picker in plugin config screen allows a color to be chosen but doesn't seem to
   effect anything ibn the chart. does it need an 'apply' button to trigger the color change ?"
2. "the - button still doesn't work, it is still greyed out (faded ,darker shade)"
3. "the night mode works great, like it a lot."
4. "still getting the straight path lines under the horizon for the sun and moon as they dip
   below the minimum altitude on the graph."

**Item 1 — no Apply button is needed; the plugin had two `PluginSettings` objects.** The
binding chain was right end to end (options page → `{Binding Settings.X}` on the manifest →
`NightPlannerVM` mirror → chart), so the fault had to be instance identity. N.I.N.A.'s
`PluginLoader.cs` composes the **manifest** at line 356
(`GetContainer(plugin).ComposeParts(manifestImport)`) and then the **parts** — dockables,
templates — at line 464 (`Compose(plugin, manifest.Name)`), and `GetContainer` at line 491
does `new CompositionContainer(catalog)` on **every call**. Two containers, so
`[PartCreationPolicy(Shared)]` produced two shared instances: one behind the options page
(the options DataTemplate's DataContext is the `IPluginManifest` instance) and one injected
into `PlannerDockableVM`. Each writes the same `settings.json`; the live file proved which
one won — written 17:35:41 with `{"darkMode":false,"sunColor":"","moonColor":"","horizonColor":""}`,
i.e. the panel's untouched copy stamped over the pick. Fix: the MEF export is gone and
`PluginSettings.For(IProfileService)` hands out one instance (`Shared` + a lock), `Plugin.cs`
imports `IProfileService` and exposes `Settings` as that instance, `Initialize()` calls
`Settings.Initialize()`, `Teardown()` disposes `Shared` (and dispose drops the reference, so
a reload builds a fresh one rather than reviving a stopped object). Side effect in the user's
favour: the night-mode checkbox on the options page now reaches the panel, which it never had.

**Item 2 — the fourth-round fix was not enough, because of an AND rule.**
`ButtonBase.IsEnabledCore()` returns local `IsEnabled` **AND** `command.CanExecute(...)`. In a
dockable pane the requery is unreliable, so a `RelayCommand` carrying a predicate re-greys
the `-` button whatever the `IsEnabled="{Binding Planner.CanZoomIn}"` binding says. Both zoom
commands now pass `() => true` and the INPC-bound `IsEnabled` is the only gate; a click past
an end of the range is a harmless no-op because `NightPlannerVM.SetZoom` clamps. Rule worth
remembering: **in this plugin, a command predicate on a dockable toolbar is a bug — put the
gate in the binding.**

**Item 4 — the polyline was already cut; the renderer was joining the pieces.** The trimmed
string is correct (`… 139.42,320.00 | 353.18,320.00 | 354.17,318.72 …`, runs separated by
`|`), but `NightChartControl.DrawCurve` called `BeginFigure` only for the very first point of
the whole string, so each later run was continued with a `LineTo` — a straight chord along
the bottom edge. Pixel evidence from `planner-preview-24-z1-at-1200.png` (before the fix):
row 359 carried **518 sun-coloured pixels spanning x=332..849** (canvas x≈277..707, exactly
the below-floor gap) while the rows above it carried 5–6. Now each run starts its own figure;
the nine re-rendered smoke frames measure 0–2 px of sun or moon colour on the bottom rows.
`PlannerChartPixelTests` guards it **on pixels**: 260 px flat run with the old renderer
(verified by reintroducing the bug), a handful now.

**Extra — bounded dispatch onto the interface thread.** Writing the colour test exposed an
unbounded `Dispatcher.Invoke` in `NightPlannerVM`: once some other thread had owned an
`Application`, the settings→planner announcement waited forever and the whole test run hung.
`OnUi` now dispatches with a two-second deadline, catches a shut-down dispatcher and falls
back to the calling thread — the same hazard would have hung N.I.N.A. on exit if a settings
write landed during teardown.

**Tests: 47 → 51, all passing.** New: `PlannerChartPixelTests` (floor crossing, above);
`TheOptionsPageAndThePanelShareOneSettingsObject` (`For` twice → same object, `Shared` null
after `Dispose`); `TheZoomCommandsNeverRefuseSoOnlyTheBindingGreysAButton`;
`AChartColourSetAnywhereReachesThePlannerAndAnnouncesItself`. `OptionsLookupUsesTheManifestName`
now builds `new Plugin()` because `Settings` is a computed property, not a settable one.
Note for whoever runs these next: a fixture that creates a WPF `Application` on its own thread
owns `Application.Current` for the whole test process — that is how the hang started.

Deploy: `0.6.0.0` built and pushed; the app (PID 17940) was still running 0.5.0.0, so
`-t:DeployPlugin` waits for the user to close N.I.N.A.

## Round of 25 Nov (sixth pass) — 0.6.0.0 crashed on start-up → `0.6.1.0`

User report, verbatim: *"this latest build does not execute successfully. upon nina launch i get the attached
error … if i choose to recover, i am able to view the plugin screen but no chart is displayed"* — the dialog
being "Unhandled Exception occurred … Should the application try to recover from the error?".

The log held one interesting line, and no plugin frame in the stack:
`System.Reflection.TargetParameterCountException: Parameter count mismatch` under
`System.Windows.Threading.ExceptionWrapper.InternalRealCall`
(`20261125-181411-3.2.1.3002.23312-202611.log`, 18:19:15, eight seconds after start).

Cause: the bounded dispatch added in the fifth pass —
`ui.Invoke(new Func<bool>(() => { … }), DispatcherPriority.Normal, TimeSpan.FromSeconds(2))`. Asking
`Dispatcher.Invoke` for a return value *and* a timeout hands the delegate to
`Delegate.DynamicInvoke` with an argument list, and a delegate taking no arguments comes back as
"Parameter count mismatch" — thrown on the interface thread, inside WPF's own wrapper, which is why it
looked like an application fault rather than a plugin one.

Fix: `ui.Invoke(action)` and nothing else, with `HasShutdownStarted` / `HasShutdownFinished`
checked first and an `InvalidOperationException` catch, so the dispatcher-already-gone case runs on
the calling thread. The deadline was the right instinct; the overload was not.

The first repair attempt was wrong in a way worth remembering: `ui.Invoke(action,
DispatcherPriority.Normal)` reads as the plain form, but WPF has no `Invoke(Action, priority)`
overload, so it binds to `Invoke(Delegate, DispatcherPriority, params object[])` — the reflection
path again — and aborted the whole test run with the same exception
(`PlannerChartPixelTests` → `PluginSettings.cs:183` → `NightPlannerVM.cs:68`, stack frame
`Dispatcher.Invoke(Delegate method, DispatcherPriority priority, Object[] args)`).

**Law: the call must bind to `Invoke(Action)`.** No priority, no timeout, no return value; if the
caller needs a result, capture it in a local.

`tests/…/Features/planner/PlannerThreadMarshallingTests.cs` reproduces the shape: an STA thread runs
`Dispatcher.Run()`, a worker thread writes `PluginSettings.SunColor`, and the test asserts the interface
thread took no exception and that the announcement arrived on it. Against the 0.6.0.0 call it fails with the
same `TargetParameterCountException` — that was verified by putting the bad call back for one run; against
this build it passes.

Two harness rules, each learned by hanging the suite for ten minutes:
- the fixture must **not** create a `System.Windows.Application`: `CompositionTests` owns the only instance
  the process is allowed, and shutting that one off also breaks `Application.LoadComponent` for everything
  later (`The Application object is being shut down`);
- a hand-pushed `DispatcherFrame` pushes at Normal and its `PushPriority` is read-only, so an exit item
  queued at Background never runs and the frame waits forever. `Dispatcher.Run()` plus a five-second
  watchdog calling `InvokeShutdown` is the shape that works.

**Tests: 51 → 52, all passing** (about five seconds of the wall clock is that watchdog).

Deploy: 0.6.1.0 deployed at 18:44 after the user closed the app (`UnphasedNina.dll` 73,728 B, all five
deployed files SHA256-identical to `artifacts\stage\Debug`, deployed `FileVersion` 0.6.1.0), relaunched as
PID 32668. There is **no log file for PID 32668**, which means nothing was logged — no crash, no
compatibility complaint — but the load is still unproven: `Process.Modules` on 32668 lists 183 modules and
**no** `UnphasedNina.dll`, which I could not explain. Needs the user's eyes on Imaging → puzzle-piece.

## Seventh pass (2026-11-25 late evening) — catalogue/publishing readiness, version 0.6.2.0

The user's choice for iteration 7 was **"Catalogue/publishing readiness"**, and they answered the axis question with
**"show me both as rendered frames"**. The chart was therefore left alone.

**Research** (subagent, full report at `nina-plugin/UNPHASED-PUBLISH-RESEARCH.md`, all claims against primary sources
on branch `release/3.2.x`, which is what the installed 3.2.1.3002 was built from):

- The catalogue is `github.com/isbeorn/nina.plugin.manifests` — a repo of **JSON manifests only**; binaries are hosted
  by the author (a GitHub release asset). Flow: fork → add
  `manifests/U/Unphased Night Planner/3.2.0.9001/<version>/manifest.json` → PR → CI (`validate-latest-manifest.js`
  validates the schema **and downloads `Installer.URL` and re-hashes it**) → merge → `gather.js` splits
  Release/`Channel: Beta` → FTPS to `nighttime-imaging.eu` → the app reads
  `https://nighttime-imaging.eu/wp-json/nina/v1/plugins/manifests` (`PluginFetcher.cs:42`).
- Two metadata layers. Assembly (read by `PluginBase.cs`): `Identifier` ← `[Guid]`, **Version ← `AssemblyFileVersion`**
  (fallback silently `1.0.0.0`), Name ← `AssemblyTitle`, **Author ← `AssemblyCompany`** (never `<Authors>`),
  ShortDescription ← `AssemblyDescription`, and `[AssemblyMetadata]` keys `License/LicenseURL/Homepage/Repository/
  ChangelogURL/Tags/LongDescription/images/MinimumApplicationVersion`. Manifest JSON required keys: `Name, Identifier,
  Version, Author, Repository, License, LicenseURL, MinimumApplicationVersion, Descriptions, Installer`; limits
  Name ≤ 50 (it is also the install folder name), ShortDescription ≤ 256, LongDescription ≤ 10 000, Tags ≤ 20 × 30.
- `Descriptions.LongDescription` **is** the store page body; no README is ever rendered by the application.
  `ChangelogURL` is rendered as a document only in 3.3 (a link in 3.2).
- Loading: the plugin folder is `%LOCALAPPDATA%\NINA\Plugins\**3.0.0**\<Name>` — `3.0.0` is NINA's own
  `PluginMinimumApplicationVersion` (ABI floor), **not** the app version, so it is the same folder on 3.0/3.1/3.2/3.3.
  Scan is `*.dll` one level deep; gates are "references an assembly named *NINA.Plugin*" + a **type-level**
  `Export` of `IPluginManifest`. Each plugin gets its own collectible `AssemblyLoadContext` that resolves private DLLs
  **from its own folder, by simple name, first match, no version check** — and ordinary probing runs first, so an
  assembly of the same simple name in the NINA app directory wins. Verified that 3.2.1.3002's 439 DLLs contain **no**
  `astronomy.dll`; re-check on every NINA update.
- Versions: the load gate is `MinimumApplicationVersion` (ours 3.2.0.9001 passes against 3.2.1.3002); an **unstamped**
  minimum defaults to `1.11.0.0` and is then auto-rejected by the 3.0.0.0 floor. Components must stay ≤ 65535
  (`65535.0.0.0` is the deprecated/blacklist sentinel). The GUID **and** Name are frozen forever — `gather.js` errors
  on "Id collision" when one GUID carries two names.
- Our own logs already showed this gate firing: two 2026-11-25 entries `requires a minimum version of 3.3.0.1060, but
  N.I.N.A. is 3.2.1.3002`, reported as *version 1.0.0.0* — the missing-`AssemblyFileVersion` fallback, from an early
  3.3-targeted build.
- Closed-source plugins are **not accepted**; AI-assisted development is allowed but must be disclosed and needs an
  accountable human maintainer.

**What changed:** `Plugin.props` carries the real listing text (short/long description, 9 tags, Homepage, changelog URL
pointing at the raw CHANGELOG — checked HTTP 200 — and the corrected `RepositoryUrl`: it pointed at
`github.com/zakky2k/unphased`, which does not exist). Screenshot/featured URLs are **deliberately empty** until real
hosted screenshots exist. New `THIRD-PARTY-NOTICES.txt` (Astronomy Engine 2.1.19, Don Cross, MIT — read off the
package's own `.nuspec`) staged into the archive via `PluginPackageFile`. README rewritten — it was still the template's
and described a NINA 3.3/net10 plugin, which this is not; it now covers install, options, the two harness laws, the
release flow, the submission path, and the AI-use disclosure. New tests: `PluginPublishingTests` (4) builds the store
manifest from the built assembly and validates it with **NJsonSchema against the vendored
`build/vendor/manifest.schema.json`**, exactly like `PluginFetcher.cs:78`, plus limit/placeholder/https checks; and
`PluginTests.TeardownReleasesTheSettingsSingletonSoReloadingThePluginStartsFresh` proves the settings singleton is
dropped on unload, that the old one stopped listening to its profile service, and that a reload against a different
`IProfileService` gets a fresh live object. **Suite 52 → 57, all passing**; the Release build inside `Build-Release.ps1`
also ran 56 green before the teardown test was added.

**A repository hole found while publishing:** `UnphasedNina/build/` — `Plugin.targets` (which defines
`StagePlugin` / `DeployPlugin` / `PackagePlugin`), the package/verify/release scripts and `build/vendor/`
(`manifest.schema.json`, `CreateManifest.ps1`, their `LICENSE` and `PROVENANCE.md`) — was **entirely untracked**: the parent
repo's `.gitignore` line `build/`, written for web output, matches at any depth. `git ls-files` returned nothing under it, so
a fresh clone could not build, deploy or package the plugin, and the release run above only worked because of local files. The
ignore now carves out `!nina-plugin/UnphasedNina/build/` + `/build/**` and the tooling is committed; `artifacts/` stays ignored.

**Axis comparison, rendered (Prague, 24 Nov 2026, 760×300):** `%TEMP%\axis-a-noon-to-noon.png` (as shipped) and
`%TEMP%\axis-b-sunset-to-sunrise.png` (sunset 16:02 → sunrise 07:37, 935 min). The curves in B are correct from the
same samples, because everything geometric goes through `Payload.FrameNoon`. **But a night-anchored frame is not just a
different anchor**: the clock row is hard-coded to noon — `NightChartControl.cs:198`
`string clock = $"{(12 + min / 60) % 24:00}:00";` — and the hour gridlines step from the frame start
(`for (min = 0; min <= spanMinutes; min += step)`) instead of snapping to whole clock hours, so B's labels said 12:00
at what is really 16:02. Also `NightPayload.FrameNoon` is `init`-only, so a re-anchored payload has to be copied field
by field, and the scrub convention everywhere is *minutes from frame noon* (`NightPlannerVM.cs:258-269`,
`NightChartControl.cs:458-469`, `NightChartVM.SnapToHour`), which would land on 16:02/17:02 rather than whole hours.
The experiment fixture was scratch and has been **deleted**. Awaiting the user's decision; multi-night stitching
(`NightFrame.For`) is built on noon-to-noon slices, so only the 1-night frame is a small change.

## Round 8 plan — agreed 2026-11-25 ~23:10 (**delivered** — kept as the record of what was asked and why; what each item became is in "Round 8 delivered" further down)

The user reviewed 0.6.2.0 and confirmed **the colour picker works**. Round 8 is a UI/UX pass on both the Options page
and the panel. Nothing below is implemented yet; every line reference was verified by a read-only inventory this
session (options page, panel, N.I.N.A. 3.2 reference source, and the public plugin repos). Shorthand:
`settings/` = `UnphasedNina/src/UnphasedNina/Features/settings/`, `planner/` = `…/Features/planner/`,
`core/` = `UnphasedCore/`, `tests/` = `UnphasedNina/tests/UnphasedNina.Tests/`, `NINA\` = the local reference tree.

### Decisions already taken — do not re-ask these

| # | Decision |
|---|---|
| 1.b | Icon: use `UnphasedNina/assets/unphased.ico` (verified: 7 frames, 16/24/32/48/64/128/256 px, all 32 bpp, 372 KB, currently referenced by **nothing** in the build). Generate a hi-res PNG from the 256 px frame for the options page. |
| 1.c | **Standing design rule for this plugin: a colour is a clickable swatch.** No hex TextBox, no separate Pick button — clicking the swatch opens the picker and saving it. Applies to all future colour UI. |
| 1.e | **Apply / Reset** replace the ON/OFF switch semantics for the site: typed coordinates do nothing until **Apply**; **Reset** copies N.I.N.A.'s current site into the boxes *and* applies it immediately. Both buttons under the latitude label. |
| 1.e2 | Latitude/longitude boxes fixed width (≈5 digits, `Width≈90`, `MaxLength`), pre-populated with the current N.I.N.A. location rather than starting blank. |
| 3 | The **calendar/date picker replaces the night list** entirely; per-night figures move up into the panel header/status line (see T3 open question). |
| 3.a | Date field shows the date, the calendar only appears when clicked (WPF's default popup behaviour). |
| 4.a | Button standard everywhere: **min height 30 DIP**, width ≈34–44 DIP, 14 DIP semibold text, **word labels**, tooltip on every control. Header grows ~12 DIP taller. Chart-drawn text is a separate ladder (`NightChartControl.cs:22-26`: 10/11/12/13) and gets its own bump. |
| 4.b | Manual **Refresh button removed**; the pre-computed window auto-extends by **10 nights** on whichever side you approach, computed **off the UI thread**. |
| 4.c | Altitude top stays **+90° hard cap** (it already is: `NightChartVM.cs:67-68 TopAltitude = 90`). |
| 4.d/4.f | Faint horizontal gridlines every **15° above the horizon** (0 → +90), matching the existing ones below; **numbers every 30°** (user chose gridlines 15°, labels 30°), on **both** left and right edges. |
| 4.e | **−6 / −12 / −18 are kept**: verified to be the official civil / nautical / astronomical depression angles, measured on the sun's geometric centre, and the band convention already matches (0 → civil, −18 → full night, tested at `tests/Features/planner/PlannerChartTests.cs:103`). They must also appear on both sides. |
| 5 | Three mutually exclusive palette buttons: **Default** (N.I.N.A. theme colours, active at launch), **Night** (today's red night-vision scheme), **Enhanced** (new static palette designed by me, presented for approval before wiring). The three colour pickers **override on top of** whichever palette is active — that is already how `PlannerPalette.EffectiveSun/Moon/Horizon` behave (`PlannerPalette.cs:152-157`, tested at `PlannerChartTests.cs:257`). |
| 5.a | **Palette choice is never persisted**: Default is re-selected on every launch. Only picked colours persist. |
| 6 | Arrow keys handled at **panel level**, so Left/Right/Up/Down work no matter where focus is — **except** while a TextBox has focus (so Options typing is never hijacked). |

### T0 — Why the arrow keys break (fix this first: small, isolated, protects everything else)

Verified cause: the chart handles keys with **`OnKeyDown` (bubbling)** at `planner/NightChartControl.cs:439-450`;
Left/Right/Home/End/Esc are handled, **Up/Down have no case at all** so `e.Handled` stays false and WPF's *directional
navigation* moves keyboard focus down to the night ListBox (`planner/Resources.xaml:116`, a **sibling** in the same Grid
`:110-113`). Up/Down then keep working — as the ListBox's own selector navigation, which drives the two-way
`SelectedItem` binding `:118` — but Left/Right are dead because a key pressed inside the ListBox never routes through a
sibling's handler. Nothing ever calls `Focus()` except the click at `:415`, and `IsTabStop` is unset across the whole
repo (default false for a bare `FrameworkElement`), so **Tab cannot get back either**: today the only recovery is clicking
the chart.

Plan: attach `PreviewKeyDown` to the panel root (in `planner/PlannerViewGlue.cs`, which already owns attach-point glue
`:54-75`), map Left/Right → `StepScrub(∓1)`, Up/Down → `PrevNight/NextNight`, Home/End/Esc as today, `e.Handled = true`,
and return early when `Keyboard.FocusedElement is TextBox`. Keep the chart's own handler for click-to-focus semantics.
Also set `IsTabStop = true` on the chart so Tab can reach it, and consider
`KeyboardNavigation.SetDirectionalNavigation(chart, None)` so Tab/arrow navigation cannot park focus between controls.
**New tests**: nothing in the suite covers keyboard, focus or the ListBox today (the gap is admitted in
`CHANGELOG.md:270-272`). Load the panel DataTemplate (the way `tests/CompositionTests.cs:42` does), raise the routed key
events on the root, assert the scrub/selection moved and assert a TextBox case does *not* move it.

### T1 — Options page (`settings/Resources.xaml`, 86 lines, `DataTemplate x:Key="OptionsTemplate"` :5)

Facts to work from: the page is a MEF-exported `ResourceDictionary` re-keyed to `"<plugin name>_Options"`
(`settings/Resources.xaml.cs:5-7`) and hosted in the plugin-store Settings tab
(`NINA/NINA/View/Plugins/PluginsView.xaml:922-934`); the DataContext is the manifest, so bindings are `{Binding Settings.X}`
(`Plugin.cs:21`). The key contract is asserted at `tests/Features/settings/FeatureTests.cs:153-159`.

- **1.c colour rows** (`settings/Resources.xaml:53-78`): today each row is label + decorative `Border` swatch (48×18,
  not clickable) + hex `TextBox` + a separate `Pick` button driving `System.Windows.Forms.ColorDialog`
  (`PluginSettings.cs:194-204`, WinForms enabled only for this at `UnphasedNina/Directory.Build.props:5-8`). Replace with
  one focusable **Button whose content *is* the swatch** (Background = the effective brush, Command = the existing
  `Pick*ColorCommand`), delete the hex TextBox and the Pick button, and put the role name + current hex in the
  **ToolTip** so the value is still discoverable. Do not remove the hex *string* properties (`SunColor` etc.
  `:119-152`) — persistence and `ResetColors` (`:180-187`) stay as they are, and the planner tests set colours through them.
- **1.e Apply/Reset + 1.e2 sizing**: lat label `:18`/box `:19-20`, lon `:21-22-23`, star-width column `:11-17` — that star
  column is exactly why the boxes are absurdly wide. New model:
  - new **draft** properties on `PluginSettings` bound `UpdateSourceTrigger=LostFocus`, pre-populated from the effective
    site so the boxes are never blank;
  - `ApplySiteCommand`: validate + clamp (±90 / ±180), write the per-profile override (`SiteLatOverride/SiteLonOverride`
    `:235-244`);
  - `ResetSiteCommand`: read `profiles.ActiveProfile.AstrometrySettings.Latitude/Longitude`
    (the real path — verified at `PluginSettings.cs:250-259` and `NINA/NINA.Profile/AstrometrySettings.cs:57,70`;
    **`Profile.Settings.TelescopeSettings.SiteLatitude` does not exist**, it is a telescope-device property
    `NINA/NINA.Equipment/Interfaces/ITelescope.cs:83`), put it in the drafts, **and** clear the override so the plugin
    follows the profile — that keeps the existing "empty means inherit" rule
    (`EffectiveSite()` `:250-259`, tested `FeatureTests.cs:30-42`) while behaving as the user asked;
  - show the profile site as read-out text under the buttons ("N.I.N.A. site 50.0875 / 14.4213"), because today the
    boxes look empty even when a site is in effect;
  - fixed `Width="90"`, `MaxLength="9"`, a `°` suffix, and prefer **N.I.N.A.'s own `UnitTextBox` +
    `LatitudeRule`/`LongitudeRule`** (`NINA/NINA/View/Options/GeneralView.xaml:974-992, 1038-1056`) —
    `NINA.CustomControlLibrary.dll` is already in the plugin's output, referenced not redistributed;
  - kill the silent failure (`TryCoord` `:261-266` falls back with no feedback): an invalid or half-filled pair must show
    an inline error, and the buttons should disable rather than revert.
- **The ON/OFF switch the user hates has a hidden bug**: N.I.N.A.'s implicit CheckBox style is a 65×25 pill whose template
  has **no ContentPresenter** (`NINA/NINA.WPF.Base/Resources/Styles/CheckBox.xaml:14-88`, merged app-wide at
  `NINA/NINA/App.xaml:33`), so the label written at `settings/Resources.xaml:26` **never renders** — the switch is
  completely unlabelled — and the same style sets `Focusable="False"` (`:15`), so it is not keyboard reachable. Any
  CheckBox left on the page needs a separate `TextBlock` label beside it.
- **Wording**: the page says "Red night-vision scheme", the panel button says "night: on/off" — T5 replaces both with the
  three palette buttons, so settle the vocabulary once: *Default / Night / Enhanced*.
- Tests: `FeatureTests.cs:30-42` (partial-override fallback) will need deliberate rewriting for Apply/Reset; add
  Apply-round-trip, Reset-copies-and-applies, clamping, and a template walk asserting no colour TextBox remains.

### T2 — Icon (mechanism now resolved; one lever covers both places the user pointed at)

**The plugin-list icon and the options/config-page icon are the same single value**: `Descriptions.FeaturedImageURL`,
read from the assembly metadata key `FeaturedImageURL` (`nina32/…/NINA.Plugin/PluginBase.cs:104, :110`) and rendered at
**25 px** in the installed list (`NINA/View/Plugins/PluginsView.xaml:189-193`) and **150 px** in the detail header that
sits directly above the Options group (`PluginsView.xaml:352-359`, the settings `GroupBox` at `:364-369`). There is no
`ImageURL`/`AltImageURL` in 3.2 at all. For an **installed** plugin the value comes from the DLL, not from
`manifest.json` — the bound object is the plugin's own MEF-exported manifest (`NINA.Plugin/PluginLoader.cs:360-364, :402`);
`manifest.json` only feeds the Available tab. Our build already emits the metadata
(`UnphasedNina/Plugin.props:52` ← `$(PluginFeaturedImageUrl)`, empty at `:34`), and `build/vendor/CreateManifest.ps1:161-162`
copies it into the manifest, so **filling in `PluginFeaturedImageUrl` is the whole change for 1.b**.

**Decided by the user (23:25): use the ICO's 256 px frame as the listing logo, as-is.** Done, and verified rather than
assumed: `nina-plugin/UnphasedNina/assets/unphased-256.png` is committed (10,908 B, 256×256, Bgra32, 14,496 fully
transparent pixels — the corners outside the disc, so it sits cleanly on either N.I.N.A. theme), and
`PluginFeaturedImageUrl` is now set to
`https://raw.githubusercontent.com/zakky2k/AstroScripts/main/nina-plugin/UnphasedNina/assets/unphased-256.png`
(`Plugin.props:34`, with a comment explaining the dead-URL trap). The built DLL carries it —
`meta: FeaturedImageURL = https://raw.githubusercontent.com/…/unphased-256.png` read back off
`src/UnphasedNina/bin/Debug/net8.0-windows/UnphasedNina.dll`, `FileVersion 0.6.2.0`.
The 256×256 frame of `assets/unphased.ico` is a plain BMP payload (270,376 B = 40-byte header + 262,144 XOR + 8,192 AND
mask), **not** an embedded PNG, so it had to be re-encoded. The recipe that works in plain PowerShell (no WPF app needed):
`New-Object IconBitmapDecoder -ArgumentList ([Uri]$ico), [BitmapCreateOptions]::PreservePixelFormat, [BitmapCacheOption]::OnLoad`,
pick `Frames | Sort PixelWidth -Descending | Select -First 1`, add it to a `PngBitmapEncoder` and `Save`.
Do **not** use `System.Drawing.Icon(stream, 256, 256)` — it silently returned the **128×128** frame (a 0×0 width byte in
the ICO directory is how 256 is encoded, and GDI+ misses it).

Traps measured by probing the real converters (all against the 3.2 tree):
- The jigsaw being replaced is `DefaultImage` → `/NINA;component/Resources/plugin.png` (`PluginsView.xaml:32`), a
  240×240 blue puzzle piece **compiled into `NINA.dll`** (`NINA.csproj:213`) — no loose copy on disk, so it cannot be
  swapped by dropping a file.
- `NullImageConverter` is a passthrough (`NINA.Core/Utility/Converters/NullImageConverter.cs:28-32`), so WPF's own
  `ImageSourceConverter` decides. An **empty** string makes it throw, which is what triggers the
  `FallbackValue`/`TargetNullValue` puzzle — that is the current state and it is benign. A **non-empty but unreachable**
  URL fails *silently* with no fallback: you get a blank image, which is worse than setting nothing. So the URL must be
  live before it is set, and the commit that sets it must land in the same push that adds the PNG.
- Relative paths resolve against the **process working directory**, not the plugin folder; the
  `/Assembly;component/…` form fails as a bound string; a `pack://` URI needs a registered resource assembly and is
  unproven for a runtime-loaded plug-in. A local absolute path or `file:///` URI does load today — but **do not use it**:
  3.3's new `PluginImage` control rejects anything that is not absolute HTTP/HTTPS
  (`develop:NINA/View/Plugins/PluginImage.cs:69-73`), so only the HTTPS URL survives the next N.I.N.A. update. 3.3 also
  caches icons for 7 days (`develop:NINA/View/Plugins/PluginContentCache.cs:32`), so an icon change lags.
- Setting the URL changes the DLL, so any *published* manifest's `Installer.Checksum` would need regenerating. Nothing is
  published yet, so this is free now — but it is the reason to settle the icon **before** the release/manifest PR.
- **Not verifiable here:** WPF's HTTP image download could not be made to succeed from this sandbox (dispatcher not
  pumped), so "HTTPS icons work in 3.2" is a code reading plus a working HEAD fetch of the same URL, not an end-to-end
  measurement. Confirm by eye in the deployed app — the icon only appears after the **next rebuild and deploy**, because
  it lives in the DLL; the currently deployed 0.6.2.0 still shows the puzzle.
- The user's picked colours are now real, and are a round-8 migration case: the live settings file read
  `{"darkMode":true,"sunColor":"#FEF80E","moonColor":"#00FFFF","horizonColor":""}` at 22:50:34. T5 must keep those two
  picks and map `darkMode: true` onto the Night palette, while "always start on Default" means the *night mode* flag
  itself stops being honoured at launch — say so in the changelog rather than letting it look like a lost setting.

The **dockable panel tab is a different mechanism and probably not what the user meant**: 3.2 has no raster icon there.
`IDockableVM.ImageGeometry` is a `GeometryGroup` (`NINA.Equipment/Interfaces/ViewModel/IDockableVM.cs:25`) defaulted to
`PuzzlePieceSVG` (`NINA.WPF.Base/ViewModel/DockableVM.cs:37`, field `:65` — settable, so `PlannerDockableVM` can assign its
own `Geometry.Parse(...)` result), and it is drawn in the **OverView tool list** (`NINA/View/Imaging/OverView.xaml:271-302`),
not in the dock tab: the anchorable title template is a text-only `TextBlock` (`NINA/Resources/Styles/AvalonDock.xaml:465-472`).
Treat "give the panel a vector glyph instead of the puzzle" as an optional extra (it needs path data, which we do not have
from the ICO), and tell the user the tab itself has never shown an icon.

### T2b — Which reference tree to cite (do this before trusting any NINA line number)

The icon research caught a source-of-truth problem that affects earlier notes in this file: **`Workspace/NINA` is N.I.N.A.
`develop` at 3.3.0.1070**, not the version we ship against. The tree that matches the installed app (3.2.1.3002) is
**`Workspace/nina32/nina-release-3.2.x`**, and its layout is flat — `nina-release-3.2.x/NINA/…`,
`nina-release-3.2.x/NINA.WPF.Base/…`, `nina-release-3.2.x/NINA.Plugin/…` — so paths written `NINA\NINA\…` are the 3.3
layout. `Workspace/_nina_fetch/` is a flat dump where `V32__*` are 3.2 files and bare/`DEV__*` are 3.3 — never mix them.
Spot-checked after the fact, the round-8 citations **do** hold in 3.2 (`App.xaml:28,33,47,52` style merges;
`Styles/DatePicker.xaml:212,233,240,246`; the CheckBox pill at `Styles/CheckBox.xaml:14-17` with `Focusable=False` at `:15`
and the implicit style at `:88`; `PluginBase.cs:104,110`; `DockableVM.cs:37`) — but re-check any line number that a future
task leans on heavily, especially around colours, since 3.2 themes `ColorPicker` (`App.xaml:52`) and 3.3 changes the icon
pipeline entirely.

### T3 — Calendar replaces the night list (3, 3.a)

**Answer to the user's question: yes, Tom Palmer's plugin is open source and the control is free.**
`github.com/tcpalmer/nina.plugin.targetplanning` is public, **MPL-2.0** (same licence as this plugin), and its start-date
field is the **plain WPF `DatePicker`** with no extra package
(`…/NINA.Plugin.TargetPlanning/Options.xaml:194-196`: `<DatePicker Margin="25,0,0,0" SelectedDate="{Binding StartDate}" />`);
its csproj references only `NINA.Plugin 3.2.0.9001`, like ours. `targetscheduler` does the same in four places and
`vorticose/nina.plugin.nightsummary` uses `<DatePicker Width="130" />`. N.I.N.A. itself ships the **style**:
`NINA/NINA.WPF.Base/Resources/Styles/DatePicker.xaml` has implicit `DatePicker` (`:246`) and `DatePickerTextBox` (`:233`,
format `yyyy-MM-dd` at `:240`) styles plus a keyed `calendarStyle` (`:212`), merged app-wide at `NINA/NINA/App.xaml:47`,
and it uses a `DatePicker` in its own SkyAtlas view (`View/SkyAtlasView.xaml:83`). So: **zero new dependencies, zero
shipped DLLs.** Do **not** reach for Xceed's `DateTimePicker` — `DotNetProjects.Wpf.Extended.Toolkit.dll` 5.0.124 *is*
present and resolves transitively (`obj/project.assets.json:251-257`), but it is MS-PL, has **no N.I.N.A. theme at all**
(zero `DateTimePicker` hits in the whole NINA tree), is never used by NINA's own UI, and shipping a private copy would
break our own "reference, never redistribute" rule (`build/Plugin.targets:14-26`, `THIRD-PARTY-NOTICES.txt:40-44`) and
could **shadow NINA's copy** (`NINA/NINA.Plugin/PluginLoader.cs:799-814` resolves by simple name from the plugin folder
first).

Implementation notes:
- `NightPlannerVM` already exposes `SelectedDate` (`planner/NightPlannerVM.cs:28, 84, 119`, a `DateTimeOffset` defaulting
  to today) — `DatePicker.SelectedDate` is a `DateTime`, so add a small `DateTime` proxy property or a one-line
  converter; do not change the `DateTimeOffset` model.
- Remove the ListBox (`planner/Resources.xaml:116-127`) and move its two jobs: the **first-compute trigger**
  (`PlannerViewGlue.cs:35-37` hooks the ListBox's `Loaded` — move it to the DatePicker/chart or the VM) and the
  **selection→chart path** (`PlannerViewGlue.cs:28-32` listens for the ListBox's `PropertyChanged` → `chart.SetRow`;
  drive the chart from the VM's `SelectedNight`/`SelectedDate` instead, which is the cleaner seam).
- Per-night figures must land somewhere: the ListBox row text is `NightRowVM.Summary` (`NightRowVM.cs:20`).
  **Open question for the user:** what goes in the header/status line — sunset/sunrise + dark minutes + best-altitude
  window? Propose three compact chips in row 3 (`Resources.xaml:103-105`, currently `FontSize="11"` — bump to 13 with
  the new standard).
- Night mode will not re-colour a stock DatePicker: N.I.N.A.'s picker styles use **StaticResource** brushes
  (`DatePicker.xaml:234-252`), while our night chrome is a private dictionary installed at runtime
  (`PlannerViewGlue.cs:54-75`). Plan: when the Night palette is active, add local `DatePicker`/`DatePickerTextBox`
  styles to that dictionary (copy the keyed `calendarStyle` from the app resources and re-brush it) instead of leaving a
  light box in a dark panel.
- Tests to rewrite on purpose: `PlannerSelectionTests.cs:104, :118, :149` (row-named selection, click-a-row-to-anchor,
  empty window) become date-driven; keep the `MorningDay` naming contract (`NightRowVM.cs:30-36`).
  `tests/CompositionTests.cs:42` will fail on any XAML slip in the new header, which is a feature.

### T4 — Altitude axis: gridlines, both-side labels, +90 (4.c–4.f)

Current state: altitude maps `(90 − alt)/114 × Height` (`NightChartVM.cs:67-76`), so +90 is already the cap and nothing
is *drawn* above the horizon. Horizontal lines come from one hard-coded loop `{-18, -12, -6, 0}`
(`NightChartControl.cs:165-168`, 0.6-DIP faint pen `:158`, 1.6-DIP horizon `:159`); the degree chips come from a second
loop `{0, -6, -12, -18}` drawn **right side only** at `w − s.Width − 3` (`:183-195`) with a `floor` stacking trick, and
the left edge is only used to reserve clock-row space (`degreesLeft` `:190, :204`).
Plan: one ladder for gridlines every 15° from −24 to +90 (faint pen below 0 as now, same pen above — 4.f); label ladder
every 30° (−90…+90) rendered on **both** edges; keep the twilight chips (0/−6/−12/−18) as their own labelled markers on
both edges, since the user asked for them on both sides too; mirror the anti-overlap `floor` logic for the left column.
Tests that pin this and must be updated deliberately: `PlannerChartTests.cs:115` (pins `TopAltitude == 90`,
`BottomAltitude == −24`, `YSun` endpoints), `:136` (needs the sun polyline to contain `|`, i.e. the −24 floor),
`:367 NothingLeaksPastTheFrameEdge`, `:185` chip-flipping, and `PlannerChartPixelTests.cs:28`. Add new asserts: a label
exists at +90 on both sides, gridline count matches the 15° ladder, left labels do not collide with the clock row.
Extend `[Explicit] PlannerChartRenderSmoke.cs` (`:46-59` renders nine cases) with new cases rather than editing existing
ones — that is the eyeball check for this task.
**Related open question:** the real element height is a hard-coded `const double ChartHeight = 260`
(`NightChartControl.cs:18`) while the VM's canvas assumes `Height = 320` (`NightChartVM.cs:13`), so everything is
vertically squeezed ~0.81 (`sx = w/1000, sy = h/320` at `:137-138`). Thirteen labels in 260 DIP is tight; propose making
the real height 320 (or letting the row size to content) as part of this task — ask before changing it, since it changes
the panel's proportions.

### T5 — Three palettes: Default / Night / Enhanced (5, 5.a–5.c)

Today it is one bool: `PluginSettings.DarkMode` (`settings/PluginSettings.cs:99-110`, global JSON written on every
change `:268-278`), consumed by the panel button (`PlannerDockableVM.cs:90, 92-93`), the Options checkbox
(`settings/Resources.xaml:25-27`), the chart (`NightChartControl.cs:58, 85, 102-106`) and the panel chrome dictionary
(`PlannerViewGlue.cs:54-75` ← `PlannerPalette.NightChrome` `:229-239`). Every palette method takes `bool dark`
(`PlannerPalette.cs:80-101, 160-173, 190-201, 207-210`) — there is no scheme enum.
Plan: introduce a scheme (string/enum `default|night|enhanced`) on `PluginSettings`, **not persisted** (user chose
"always start on Default"), with the old `darkMode` bool read for migration only (`darkMode: true` with no scheme →
Night) and no longer written. Replace the single `night: off/on` button with three mutually exclusive header buttons in
the new 30 px style; keep picked colours overriding the active scheme (already true). `DarkMode` consumers should derive
`bool dark = scheme != Default` where code still needs it (the night chrome and the red set), and Enhanced needs a
decision: proposal is **chart-only** Enhanced (dark navy canvas, native N.I.N.A. panel chrome) rather than a third chrome
dictionary — cheaper, and the panel still looks like N.I.N.A.
Tests: `FeatureTests.cs:74-88` (`DarkModePersistsAcrossInstances`) and `:91-121` (colours + reset) must be re-pointed at
the new scheme key; `PlannerChartTests.cs:455 TwilightPaletteFollowsTheStandardAltitudeBands` loops only
`dark = false/true` and asserts four distinct bands — it becomes three schemes; `:241` and `:257` cover the horizon
default and picked-colour precedence. New test: scheme selection is exclusive, resets to Default on a fresh instance, and
picked colours still win under every scheme.
**Enhanced palette proposal (measured, not guessed)** — WCAG contrast on the canvas:
canvas `#0B1020`; sun `#F5B942` (10.31:1), moon `#9BD0F5` (11.05:1), horizon `#45D0A0` (9.27:1), scrub/accent
`#FF8A5C` (7.69:1), ink/labels `#E8EEF7` (16.82:1), faint gridline `#2C3A5C` (1.36:1, deliberately faint), chip backing
`#1A2338` with white text; band steps `#16233F` daylight → `#121C33` civil → `#0E1628` nautical → canvas at
astronomical (steps 1.10/1.04/1.03 — subtle shading, not stripes). Curve pairs differ by ~1.07–1.19 in luminance, so the
three lines separate by hue, not brightness — which is the right call for a red-green viewer but must be **rendered as
a preview and approved by the user before wiring**.

### T6 — Auto-extend the window, drop Refresh (4.b)

`Refresh` exists: `planner/Resources.xaml:45-47` → `PlannerDockableVM.cs:53` → `NightPlannerVM.RequestRecompute`
(`:303`). The window is `PastNights = 10 / FutureNights = 20` (`:23-24`), built by `ComputeWindow` (`:345-360`) on
`Task.Run` (`:313`) with results marshalled through `OnUi` (`:327-336`) and stale runs dropped by `computeToken`
(`:311, :326`); `Busy` is set (`:170-172`) but **never bound in XAML**, so there is no busy indicator at all.
Two real problems to fix in passing: `RebuildRows` (`:223-234`) runs **inside the UI callback** and re-stitches 31
frames on the interface thread, and paging past the edge today does `Nights[Nights.Count / 2]` (`:162`) — the selection
silently snaps back to index 15 (anchor + 5 days) with no recompute, a behaviour *pinned by*
`tests/Features/planner/PlannerSelectionTests.cs:129-134`.
Plan: delete the Refresh button; keep `RequestRecompute` private; when the selected night is within **2 nights** of
either edge, grow that side by **10 nights** on the existing `Task.Run` path; cache payloads by frame noon in a
`Dictionary` so extension computes only the new nights instead of recomputing all 31 (`core/NightCalculator.cs` costs
~289 sun + ~290 moon samples plus bisections per night); move row stitching off the UI callback (or only stitch the
visible frame); bind `Busy` to a small "computing…" indicator. Rewrite `:344` (which pins the constants — they become
initial values, not limits) and `:129` (which must now expect the window to re-anchor and the requested night to be
found). Respect the marshalling law: `OnUi` → `ui.Invoke(action)` (`:63-80`; the `Invoke(action, priority)` overload trap
is documented at `:44-53`).

### Sequencing, version and hygiene

Suggested order, one commit each, suite green before moving on, commit **and push** every time:
**T0** arrow keys (+ first keyboard tests in the repo) → **T1** options page → **T2** icon →
**T3** date picker replacing the list → **T4** altitude labels/gridlines → **T5** three palettes →
**T6** auto-extend + Refresh removal → version bump + CHANGELOG/HANDOFF.
Version: **0.7.0.0** (UI overhaul across both surfaces; `UnphasedNina/Plugin.props:7`). Deploy at the end with N.I.N.A.
closed, verify the deployed files hash-match `artifacts/stage/Debug`, then the visual round.
**Do T2 (the icon) before any release tag**: `FeaturedImageURL` is compiled into the DLL, so setting it changes the
checksum a published manifest must match — commit `assets/unphased-256.png` and set `PluginFeaturedImageUrl`
(`Plugin.props:34`) in the same commit, and make sure the URL is already live, since a dead URL renders as a silent blank
rather than falling back to the puzzle.
Standing traps for whoever implements: one `System.Windows.Application` per process (owned by `CompositionTests`);
`Invoke(Action)` only; the pane template key must stay `"<VM FullName>_Dockable"` and the options key
`"<plugin Name>_Options"`; `PluginSettings` is one singleton (`For()`), never construct a second; the suite writes the
**live** settings file, so after the scheme change re-check the user's colours before they test; keep chart axis
*behaviour* (noon anchor, +90 cap, −24 floor) as decided unless the user says otherwise; add `[Explicit]` render-smoke
cases and look at each PNG once rather than dumping near-identical images into the transcript.

### Still open, to settle during round 8

1. What the header summary shows once the night list is gone (T3).
2. Chart height: raise the hard-coded 260 to 320 (or size to content) to make room for 13 labels (T4).
3. **CLOSED (23:25):** the listing logo is the ICO's 256 px frame, as-is — see T2. What remains optional is the
   OverView tool entry: replacing the puzzle `GeometryGroup` needs vector path data we do not have, and the dock tab
   itself has never shown an icon.
4. Whether Enhanced repaints the panel chrome or only the chart (T5) — proposal: chart only.
5. A real astronomy question the user should decide on, not a styling one: the calculations run with
   **no atmospheric refraction and no aberration** (`core/NightCalculator.cs:33-39`) and sunset/sunrise is the geometric
   0° crossing (`:67, :70`), so times sit a minute or two off published almanac values. The −6/−12/−18 depths are
   correct; the *times* are the part that differs. Switching to apparent-horizon values would also change every fixture
   the web planner shares.
6. Longstanding, still unanswered: per-file MPL-2.0 headers; wheel-over-chart stealing panel scroll; moon glyph
   overlapping its chip in night mode; degree labels existed only on the right (fixed by T4).

## Round 8 delivered — version 0.7.0.0, seven commits, suite 92

Implemented in the planned order, one commit each, `dotnet test` green after every step, pushed every time:
`4120ea7` T2 icon → `851b735` T0 keyboard → `f970fdf` T1 options page → `3a35bcf` T3 calendar → `6394dd3` T4 axis →
`20fbffd` T5 palettes → `ccc93a6` T6 window growth, then the release commit (`Plugin.props:7` = **0.7.0.0**,
`UnphasedNina/CHANGELOG.md`, this file). The order differs from the plan in one way only: T2 (the icon) was committed
first, ahead of T0; everything else followed the planned sequence.

**What each answer became.**
- **T3 calendar.** A stock WPF `DatePicker` in the header (the same control the target-planning and night-summary
  plugins use, so N.I.N.A. themes it for free; Xceed's `DateTimePicker` was rejected — a licensed dependency for one
  field). `PlannerViewGlue` carries the chart wire on the picker, and its tooltip says the date is the day the night
  *ends* on, which is `NightRowVM.MorningDay` — the value `SelectedDate` matches on. Three chips under the caption
  replaced the row's figures; they describe the **highlighted night**, not the zoomed span, so they filter the
  payload's samples to `NightStart..NightEnd`, because a payload samples the whole noon-to-noon frame.
- **T4 axis.** The element asks for 320 DIP, matching the abstract canvas (`NightChartVM.Height = 320`), so altitudes
  are no longer drawn at 0.81 of what they claim and the three twilight depths get their own room. Rules every 15°,
  numbers every 30° plus each twilight depth, printed on both edges from one shared plan. A label with no room of its
  own is dropped rather than pushed into its neighbour, which is the accepted cosmetic trade-off: at the panel's
  160 DIP minimum the ladder shows only 90° and 60° (the green horizon rule is still drawn).
- **T5 three palettes.** `enum ChartScheme { Default, Night, Enhanced }`; every `PlannerPalette` role method takes the
  scheme; the `NightChrome` dictionary is installed in the panel root's own resources **only** for Night
  (`PlannerViewGlue.NightPaletteProperty` holds the scheme the root was told about). The scheme is deliberately **not
  persisted**: a legacy `darkMode: true` in the options JSON is read once in `Initialize()` and then physically
  removed by a one-shot `SaveGlobal()`, so a panel that used to come up red at every launch now comes up as the theme.
  Enhanced is chart-only by the user's answer. Measured contrast against the plot ground: sun 10.31:1, moon 11.05:1,
  horizon 9.27:1, scrub/accent 7.69:1, ink 16.82:1, faint gridline 1.36:1 (deliberate), band steps 1.10/1.04/1.03 —
  and the curve pairs differ only 1.07–1.19 in luminance, so they separate by hue, which is the whole point of the set.
- **T6 the window grows by itself.** `PastNights = 10` / `FutureNights = 20` are the **opening reach, not a limit**;
  `EdgeNights = 2` and `ExtensionNights = 10` govern growth. `NightPlannerVM` keeps `windowFirstDay`/`windowLastDay`
  and a payload cache keyed by frame-noon `UtcTicks` (nights a site cannot produce are cached as absent, so a polar
  window does not re-derive them), so extending costs only the new nights — asserted with `ReferenceEquals` on the
  payloads in `PlannerWindowGrowthTests`. Rows are stitched on the compute thread (`Stitch` touches no
  brush and no dispatcher); `ApplyWindow` re-matches the selection by `MorningDay` on the UI thread. A date far outside
  the window **re-anchors on the picked date**, and the picked date has to be captured before the middle-of-window
  stand-in moves it — that ordering was a real bug found while writing it, and the test now pins it.
  `RequestRecompute` is private, `EnsureComputed()` (the glue calls it on `Loaded`) is the only public entry point, and
  `RefreshCommand` plus its button are gone; a `TextBlock` "computing..." readout triggers on `Planner.Busy`.

**New test-harness laws (round 8 is what produced these).**
- The suite's single WPF `Application` is owned by **`WpfHost.Ensure()`** (`tests/UnphasedNina.Tests/WpfHost.cs`),
  which merges N.I.N.A.'s `StaticResources/{ProfileService,SVGDictionary,Brushes,Converters}.xaml`, names the root
  `RootGrid`, and never shuts the Application down. It is created on the **calling test thread** — creating it inside
  a short-lived worker thread strands the dispatcher and hangs the run, and the run then leaves a `testhost` holding
  the test DLL (`MSB3027`/`MSB3021` on the next build; `Get-Process testhost | Stop-Process -Force` clears it).
  `CompositionTests` no longer owns it.
- `[Explicit]` render smokes that draw use a **fixture-lifetime STA "studio" thread** pumping a static
  `System.Windows.Threading.Dispatcher.Run()` (call it statically — `CS0176` on an instance call), one Application for
  the whole fixture, opened by `OpenTheStudio()` and closed by `CloseTheStudio()`. Two smokes each opening their own
  Application is the `Cannot create more than one System.Windows.Application instance` failure.
- A test waiting on the planner's background compute must **pump the dispatcher while it waits**
  (`PlannerWindowGrowthTests.WaitFor`): the result arrives through `OnUi`, and in a test the interface thread *is* the
  test thread, so `Thread.Sleep` polling just burns the budget. Pump only when that dispatcher's `CheckAccess()` says
  it belongs to this thread; otherwise the dispatcher belongs to a thread that is no longer listening and the wait
  should fail rather than hang.
- What a control drew can be inspected deterministically: a test-local subclass exposes the `protected OnRender` and
  hands it a hand-made `DrawingGroup`. WPF has no `VisualTreeHelper.GetContent`.
- **Never put a `Style`/`DataTrigger` on a `Button` in the panel template** — an explicit style replaces N.I.N.A.'s
  implicit button template and the chrome disappears. The palette buttons are plain buttons (`Padding="8,2"`) plus a
  separate readout. A `Style` on a `TextBlock` is safe, and the busy indicator needs no converter: the plugin ships
  none and N.I.N.A.'s are not guaranteed in scope inside a plugin template.
- A button's command is not run by raising `ButtonBase.ClickEvent`; read `ButtonBase.CommandProperty` and call
  `Execute(null)` (`PlannerPanelWiringTests.Press`). Keyboard tests do need a hosted, visible, off-screen window
  (`Left = -32000`, `Opacity = 0`), because routed key events need a `PresentationSource`.
- Tests only see public members (no `InternalsVisibleTo`), and a mocked `IAstrometrySettings` with finite lat/lon on
  the mocked active profile is how a test gets a **site without writing the live settings file** — unlike
  `SiteLatOverride`, which persists.

**0.7.0.0 was deployed and reviewed on 26 Nov.** `DeployPlugin` at 00:55 put `UnphasedNina.dll` (90,112 B,
`FileVersion 0.7.0.0`) into `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Unphased Night Planner`, all six files SHA256-identical
to `artifacts\stage\Debug`. N.I.N.A. 3.2.1.3002 started at 00:56:10 and that run's log (PID 8628) is the healthy
1,153-byte banner file with no ERROR, while `UnphasedNina.dll` and `UnphasedCore.dll` do appear in the process's module
list — load proof stronger than the banner-only calibration under "Evidence rules". **The user looked at the panel and
the options page in the live app and accepted the build as 0.7.0.0.**

**Round 9 starts here.**
1. **Button sizing: the agreed design has not been applied to the symbol buttons.** Round 8's decision 4.a asked for
   buttons at **min height 30 DIP, width ≈34–44 DIP, 14 DIP semibold text, word labels, tooltip on every control**.
   The four symbol buttons — `<` and `>` (previous / next night) and `-` / `+` (zoom) — still set only
   `Padding="8,2"` (`Features/planner/Resources.xaml:49,56,95,100`), so the glyph decides how big the button ends up.
   The user's review words: *"symbols < > + - used as the size of the button instead of design we agreed"*. Tooltips
   are already there on all four; what is missing is the minimum size and the word labels. `< hr`, `hr >` and `off` in
   row 2 are the same category, so decide once for the whole panel: fixed-size buttons with symbols inside, or words —
   remembering that an explicit `Style` on a `Button` kills N.I.N.A.'s implicit template (see the rule above), so the
   size has to come from `MinHeight`/`MinWidth`/`Padding` attributes, not a style.
2. `Build-Release.ps1 -Tag v0.7.0.0 -Repository zakky2k/AstroScripts -Channel Beta`, `Verify-ReleaseArtifacts.ps1`, the
   GitHub release (the archive must stay byte-identical afterwards — catalogue CI re-hashes `Installer.URL`), then the
   manifest PR at `manifests/U/Unphased Night Planner/3.2.0.9001/0.7.0.0/manifest.json`. Screenshots are still not
   hosted, so the listing shows the icon only.
3. Still not seen in the live panel: whether the "computing..." readout ever visibly appears while paging sideways
   past the edge of the window (its markup and the flag it triggers on are pinned by tests only), and what the
   `DatePicker` looks like at other panel widths. Re-check the user's picked colours afterwards — the suite writes the
   live settings file.
4. Still undecided, carried forward: per-file MPL-2.0 headers; the wheel over the chart stealing panel scroll; the
   moon glyph overlapping its chip in night mode; whether to move to apparent-horizon sunrise/sunset (the user chose
   to keep geometric 0°, and changing it would invalidate every fixture the web planner shares).

## Open items and decisions
- **0.6.2.0 is the deployed build** (deployed 21:46 on 25 Nov, after `8cdcce1`): `UnphasedNina.dll` **74,240 B**,
  `ProductVersion 0.6.2.0+8cdcce1…`, and now **six** files in the folder — `THIRD-PARTY-NOTICES.txt` (2,799 B) sits beside
  LICENSE.txt — every one SHA256-identical to `artifacts\stage\Debug`. N.I.N.A. was relaunched at 21:45:44 (PID 13672,
  main window up, `Responding=True`) and wrote **no log file at all**, which here means a clean plug-in scan. For
  calibration: healthy sessions leave a **1,153-byte** file holding only the version banner and one `NINALogWatcher`
  warning (18:44, 21:02, and others); the 0.6.0.0 crash left **2,031 bytes** including
  `ERROR|App.xaml.cs|Current_DispatcherUnhandledException|…System.Reflection.TargetParameterCountException` at 18:19:15;
  the two **rejections** are in the 00:26 and 00:31 logs —
  `Failed to load plugin … Unphased Night Planner version 1.0.0.0` / `requires a minimum version of 3.3.0.1060, but
  N.I.N.A. is 3.2.1.3002`, from the abandoned 3.3/net10 build, and the `1.0.0.0` is the missing-`AssemblyFileVersion`
  fallback. **N.I.N.A. logs no success line at the default level**, so there is no `Successfully loaded plugin …` to grep
  for — the evidence is "banner only, no ERROR", plus the user seeing the panel. `Process.Modules` listing 183 entries
  without `UnphasedNina.dll` is normal and is **not** a symptom.
- **Closed by the round-8 review:** the picked chart colours do reach the chart — the user reported "the color picker is
  working". The live settings file read
  `{"darkMode":false,"sunColor":"","moonColor":"","horizonColor":""}` when last checked (17:35:41) but may now carry a
  colour, so **re-read it before assuming**; remember the test suite writes that same live file.
- **Publishing, what is left** (metadata itself is now real — the old placeholder item below is closed):
  1. no GitHub release exists, so there is no real `Installer.URL` and no archive to attach; create tag `v0.6.2.0`,
     run `Build-Release.ps1 -Tag v0.6.2.0 -Repository zakky2k/AstroScripts -Channel Beta`, attach the ZIP, and never
     rebuild it after hashing;
  2. no manifest PR against `isbeorn/nina.plugin.manifests` yet (`manifests/U/Unphased Night Planner/3.2.0.9001/…`);
     its CI re-downloads and re-hashes, so the release has to exist first;
  3. screenshots: `ScreenshotURL`/`AltScreenshotURL`/`FeaturedImageURL` are empty by design — host two panel PNGs as
     release assets and fill `Plugin.props`;
  4. the plugin's own source files have **no per-file MPL-2.0 headers**, which MPL expects and which is worth fixing
     before the source is announced;
  5. the repo must be public with the plugin + `UnphasedCore` + tests + build scripts in it (closed source is rejected).
- `Plugin.props` metadata is **no longer** catalogue-placeholder (fixed in 0.6.2.0): it used to say
  `RepositoryUrl = github.com/zakky2k/unphased` and `Description = "A NINA plugin."`.
- Commit convention here is commit **and push** to main; `6c240ce` and the handoff are pushed.
- **Now chosen, in round 8 (task T4):** degree labels on the **left** edge as well as the right, gridlines every 15°
  above the horizon, numbers every 30°, on both sides.
- **Done in the fourth pass instead:** the deeper computed window. `NightPlannerVM.PastNights = 10` /
  `FutureNights = 20` (public, so a test can pin the shape) → 31 payloads; the user asked for "+10 in the future,
  keep past the same". The first compute is a little longer; the blank right edge on a 7-night frame near the end of
  the window is mostly gone.
- **Single-night axis anchor — CLOSED, decided 25 Nov after both frames were shown: keep noon → noon.** The user chose
  it deliberately, so the one-night panel stays consistent with the multi-night frames and with the "minutes from frame
  noon" scrub convention. Treat the anchor as fixed unless they raise it again. If it is ever revisited, the cost is
  localized but real: `NightChartControl.cs:198` (clock text derived from `Payload.FrameNoon` instead of `12 + min/60`),
  gridlines snapped to whole clock hours, the scrub's hour snapping, and the fact that `NightFrame.For` stitches
  noon-to-noon slices for zoom levels above one night.
- **The visual round on 0.6.2.0 happened** (the round-8 review above). Screenshots still do not exist, so
  `ScreenshotURL`/`AltScreenshotURL`/`FeaturedImageURL` stay empty until a release attaches real ones — and the panel will
  look different after round 8, so take them from the round-8 build, not from 0.6.2.0.
- Still-open input wrinkle: the mouse wheel over the chart is handled by the scrub, so the panel cannot be scrolled
  while the pointer is over the chart. Watch for the complaint.
- Leftovers from the earlier net10 experiment: stale `bin/Debug/net10.0-windows` trees under `src` and `tests`
  (gitignored, safe to delete).

## Gotchas learned
- **There are two N.I.N.A. source trees on this machine and only one is right.** `Workspace\NINA` is `develop` at
  **3.3.0.1070**; the installed app is **3.2.1.3002**, whose tree is `Workspace\nina32\nina-release-3.2.x` (flat layout:
  `nina-release-3.2.x/NINA/…`, not `NINA/NINA/…`). `Workspace\_nina_fetch\` mixes both (`V32__*` = 3.2, bare/`DEV__*` = 3.3).
  Cite the 3.2 tree; treat anything 3.3-only (the `PluginImage`/`PluginContentCache` icon pipeline, `net10` targets) as
  forward-compat information, not as the shipped behaviour.
- **Assembly metadata is the plugin's identity to N.I.N.A.** — Version comes from **`AssemblyFileVersion`** and silently
  falls back to **`1.0.0.0`** when it is missing (our own log proved it); Author comes from `AssemblyCompany`, never
  `<Authors>`; a missing `MinimumApplicationVersion` metadata defaults to **`1.11.0.0`** and is then rejected by the
  3.0.0.0 floor. Version components must stay ≤ 65535 (`65535.0.0.0` means deprecated). The store page body is
  `Descriptions.LongDescription`; **no README is ever rendered**.
- The plugin folder `…\Plugins\3.0.0\` is N.I.N.A.'s **ABI floor**, not the app version — the same folder on 3.0/3.1/3.2/3.3.
  Each plugin gets its own collectible `AssemblyLoadContext` resolving private DLLs from **its own folder, by simple
  name, first match, no version check**, with ordinary probing (the NINA app directory) running first — so a bundled
  `astronomy.dll` is safe only while no such name exists in the app directory. Check after every NINA update.
- A catalogue manifest is not accepted on trust: `gather.js` rejects GUID↔Name collisions, and the PR CI
  **downloads `Installer.URL` and re-hashes it**, so any rebuild after hashing breaks the listing. GUID and Name are
  frozen for the plugin's lifetime; N.I.N.A. keys settings and de-installation off the GUID.
- The panel is opened from the Info-bar puzzle button; the DataTemplate key must be exactly
  `"<VM type FullName>_Dockable"` (`PlannerDockableVM`), and it is a `DockableVM`, MEF-exported as `IDockableVM`.
- Setting `ListBox.SelectedItem` from inside a `CollectionChanged` Add notification does not stick; select after the
  collection settles (`NightPlannerVM.ApplySelection`, `NightPlannerVM.Ui` dispatcher).
- A unit-test host has no N.I.N.A. theme dictionaries, so `PlannerPalette` falls back to flat `Brushes.Gray` —
  background and curves then share a colour and a render looks blank. `PlannerChartRenderSmoke` injects stand-in
  brushes (`TertiaryBackgroundBrush`, `PrimaryBrush`, `SecondaryBrush`, `BorderBrush`, `ButtonBackgroundSelectedBrush`).
- Only **one** `System.Windows.Application` per AppDomain: build it once and reuse it across the render loop, on a
  single STA thread, or WPF throws. Since round 8 the suite's one Application is owned by
  **`WpfHost.Ensure()`** (`tests/UnphasedNina.Tests/WpfHost.cs`), which any fixture that needs WPF calls first and
  never shuts down — any fixture that builds its own fails the rest (`Cannot create more than one
  System.Windows.Application instance`), and one that shuts it down breaks every later XAML load
  (`The Application object is being shut down`). It must also be created on the **test thread**, not inside a
  short-lived worker thread: that strands the dispatcher, hangs the run, and leaves a `testhost` locking the test DLL
  (`MSB3027`/`MSB3021` at the next build). See "Round 8 delivered" for the full harness laws.
- A `Dispatcher` call that binds to `Invoke(Delegate, priority, params object[])` — the explicit delegate form,
  the generic `Invoke<T>(Func<T>, priority, timeout)` form, **or the innocent-looking
  `Invoke(action, DispatcherPriority.Normal)`**, since WPF has no `Invoke(Action, priority)` overload — throws
  `TargetParameterCountException` on the interface thread when the delegate takes no arguments, with WPF's
  `ExceptionWrapper` as the only thing in the stack. That crashed 0.6.0.0 at start-up and aborted a test run during
  the repair. Bind to `Invoke(Action)`.
- Stitching consecutive payloads: the sanity check has to measure the **gap between successive `FrameNoon`s**, not
  compare each noon against an expected one — the expected-difference is 0 h for a correct sequence, so the guard
  rejected every slice after the first and the "3-night" frame silently held one day. When a stitch looks wrong,
  print the `FrameNoon`s and the gaps from a scratch `[Test]`, then delete the scratch file.
- `Math.Min` / `Math.Max` have **no `DateTimeOffset` overload**; the compiler picks `byte` and reports
  `CS1503: cannot convert from 'System.DateTimeOffset' to 'byte'`. Write the ternary instead.
- A command's `CanExecute` in a **dockable pane goes stale**: `CommunityToolkit.Mvvm` relies on
  `CommandManager.RequerySuggested`, which does not fire when a dockable's own command changes state. Symptoms were
  exactly the user's item 2 — `+` worked, `-` stayed greyed. Expose INPC booleans and bind `IsEnabled`.
- N.I.N.A.'s brush keys are **`StaticResource` references resolved per element** (1549 of them vs 3 `DynamicResource`),
  which is why a plugin-local dictionary shadows them for the plugin's subtree — and why the same dictionary cannot
  reach N.I.N.A.'s own control styles, or another tab. App-scope merging works but is not scoped.
- `<UseWindowsForms>true</UseWindowsForms>` (needed only for `ColorDialog`) injects `System.Drawing` and
  `System.Windows.Forms` **implicit usings**, which turn every `Brush` / `Size` in the WPF code into `CS0104`
  ambiguity. `UnphasedNina/Directory.Build.targets` removes those two usings; do not delete it.
- `RelayCommand` is ambiguous between `CommunityToolkit.Mvvm.Input` and **`NINA.Core.Utility`**; the settings VM uses
  a `using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;` alias.
- The plugin options JSON is written from **fields**, so its keys are camelCase (`darkMode`, `sunColor`). Reading with
  `nameof(SunColor)` finds nothing and silently returns empty — the persistence test caught exactly that.
- The plugin options file is the **live one** (`%LOCALAPPDATA%\NINA\PluginSettings\09572065-…\settings.json`), so the
  settings tests overwrite the user's real choices. `FeatureTests` restores what it changed; anything new there must
  clean up after itself too, or split off a temp-path variant. Measured on 25 Nov: a suite run from the agent's
  sandboxed shell left that file's stamp at 17:35:41 untouched, even though a test that writes a colour and reads it
  back passed — so from inside the sandbox the write may be redirected rather than landing on the live path. Unresolved;
  treat "re-pick your colours after a suite run" as the safe assumption either way.
- Keep screenshots and previews small and look at each one once — an earlier session in this workspace spilled ~32 GB
  of embedded near-identical PNGs (root HANDOFF, "Session hygiene").

## How this user works
Enthusiastic, fast visual reviewer: numbered list of chart complaints per round, typos and all — read for intent.
Wants evidence before claims ("show me it works"), closes N.I.N.A. himself when asked, and accepts offered options
(dockable vs options page, automatic vs manual span) with a short yes. Small, honest commits; changelog updated with
the state of the thing, including what is still rough.
