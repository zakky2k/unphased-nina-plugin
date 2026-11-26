# Changelog

## 0.7.0.0 — a calendar, a taller chart, three palettes, and no Refresh button

Round 8, straight from the 0.6.2.0 visual review. Seven commits, one per item, suite green after
each (57 → 92 tests). Everything here has been rendered in-process and looked at; **none of it has
been looked at inside a running N.I.N.A. yet**, which is the first thing to do before shipping.

- **The arrow keys work from anywhere in the panel** (`851b735`). The chart used to handle them in
  `OnKeyDown`, a bubbling event, so they only worked while the chart itself held keyboard focus —
  which only a click on the chart gave it — and Up/Down were not handled at all, so WPF's
  directional navigation walked focus into the night list, which then spent Left/Right on its own
  selection. A `PreviewKeyDown` handler on the panel root sees every key aimed at anything inside
  the panel before any child does, and consuming it there also stops the focus walk. Text fields
  keep their keys, because the caret owns Left/Right/Up/Down.
- **The options page stops writing half-typed sites** (`f970fdf`). The two lat/lon boxes used to
  write straight into the stored pair, so a half-entered pair sat in the settings while
  `EffectiveSite()` quietly carried on using the profile — no message, and no way to see which box
  was wrong. They now edit drafts behind an **Apply/Reset** pair, arrive pre-filled with whatever is
  in effect, keep Apply disabled until the pair is usable, and clamp an impossible pair to what a
  sphere holds and say so. N.I.N.A.'s own `UnitTextBox` carries the degree unit (it needs an explicit
  width: its template sizes itself to its content). The three hex boxes and their separate Pick
  buttons collapse into one button whose content **is the swatch**, with the exact value in the
  tooltip.
- **The plugin shows its own icon** (`4120ea7`). `FeaturedImageURL` now points at
  `assets/unphased-256.png`, and one property covers both places the review pointed at: N.I.N.A.
  draws it at 25 px in the plugin list and 150 px on the plugin's settings page, read off the DLL
  rather than from `manifest.json`.
- **A calendar replaces the night list** (`3a35bcf`). A list is a poor way to answer "what is the
  sky doing on the 3rd", and the rows were spending height the chart needed. The header carries a
  stock WPF `DatePicker` — the same control the target-planning and night-summary plugins use, so
  N.I.N.A. themes it for free — and the figures that used to be read off a row now sit in **three
  chips** under the caption: which night, how long it is dark, and where the Moon is. Those chips
  describe the highlighted night, not the span the chart is zoomed into, which meant filtering the
  payload's samples to `NightStart..NightEnd`: a payload's samples cover the whole noon-to-noon
  frame.
- **The chart is 320 DIP tall and reads altitudes off both edges** (`6394dd3`). It had been asking
  for 260 DIP while its own canvas is 320 units tall, so every altitude arrived squashed to 0.81 of
  what it claimed and the three twilight depths had to share the last quarter of the panel. Rules
  now step every 15° across the whole window, with numbers every 30° plus each twilight depth,
  printed on **both** edges from one shared plan, so the two sides can never disagree. A number that
  cannot be given its own room is dropped rather than pushed into its neighbour — which is why at
  the panel's shortest height the ladder shows 90° and 60° and nothing below.
- **Three palettes instead of a night switch** (`20fbffd`). One switch could only be on or off, and
  on meant the red of N.I.N.A.'s own night mode, so there was no way to be high-contrast without
  borrowing a look someone might not want. The chart now offers **Default** (the user's N.I.N.A.
  theme), **Night** (the red set, panel included) and **Enhanced** — a navy set whose sun, moon and
  horizon are separated by hue rather than brightness, which is what a red-green viewer needs:
  measured contrast against the plot is sun 10.31:1, moon 11.05:1, horizon 9.27:1, ink 16.82:1. The
  three buttons sit in the panel header with a small readout beside them, and Enhanced is chart-only
  on purpose: the panel keeps the user's own N.I.N.A. chrome.
  **The night-mode flag in an old settings file is read once and then written out of the file**, so
  a panel that used to come up red at every launch now comes up as the theme and stays as you left
  it. The palette choice itself is deliberately not remembered between runs.
- **The window of nights grows by itself, and Refresh is gone** (`ccc93a6`). The panel opens
  holding twenty nights ahead and ten behind, and as the calendar comes within two nights of either
  end it asks for **ten more** on that side on a background thread — calculating only the nights
  beyond what it already holds, because the nights it has are kept. A date far outside the window
  re-anchors the window on the night that was picked instead of paging twenty times to reach it.
  That left the Refresh button with nothing to do, so it and its command are gone; a small
  "computing..." readout on the status line says what the panel is doing while it waits.

**Suite now 92 passing.** The new coverage is a fixture that drives the real calculator on the
planner's own background path — window shape, growing at both edges, the nights being reused rather
than recomputed (asserted by object identity), and re-anchoring — plus a deterministic WPF host so
panels can be built in more than one fixture, and a drawing-inspection test that reaches `OnRender`
from a test-local subclass, because WPF has no way to ask a control what it drew.

**Still rough, and unproven:**
- **Not yet applied: the agreed button design.** The plan asked for buttons with a minimum height of 30 DIP, a width
  around 34–44 DIP, 14 DIP semibold text and word labels. The `<` `>` (night) and `-` `+` (zoom) buttons only set a
  padding, so the symbol itself decides how big the button is. Left for the next revision.
- The in-process PNGs are still the only rendered evidence: they are built without N.I.N.A.'s theme dictionaries, so
  in them the button labels are barely visible and the `DatePicker` shows its light stock chrome — they prove
  geometry, not the live theme.
- The `DatePicker` is stock: night mode does not restyle it, and its drop-down keeps N.I.N.A.'s own
  look. The Xceed control N.I.N.A's own calendar uses was rejected — it drags in a licensed
  dependency for one field.
- Whether the "computing..." readout appears is pinned at the markup level (its style triggers on
  the same flag the compute path sets) and by the flag's announcements; seeing it blink in the live
  panel has not been done.
- The test suite writes the plugin's **live** settings file when it exercises the options page, so
  re-check any picked colours and the site override before starting the app after a test run.
- 0.7.0.0 is the build deployed in N.I.N.A.'s plugin folder, but `Build-Release.ps1` has not been
  run for this version and there are still no hosted screenshots, so the store listing keeps the
  image URL only.

## 0.6.2.0 — the listing text, the release archive, and what the catalogue actually checks

Nothing about the chart changed. This round was about being publishable, and it turned up that most
of the metadata N.I.N.A. uses for a store listing was still the template's.

- **Where the listing text comes from.** N.I.N.A. reads a plugin's identity off the assembly:
  `AssemblyTitle` → name, `AssemblyCompany` → author, **`AssemblyFileVersion`** → version,
  `AssemblyDescription` → the one-line description, and `[AssemblyMetadata]` entries for licence,
  homepage, repository, changelog, tags and long description (`PluginBase.cs`). NuGet's
  `<Authors>`/`<Copyright>` are never read. So `Plugin.props` now carries real values: the stale
  `RepositoryUrl` (`github.com/zakky2k/unphased`, a repository that does not exist) is now
  `github.com/zakky2k/AstroScripts`, `ChangelogUrl` points at this file's raw URL (checked: HTTP
  200), nine tags, a one-sentence description and a multi-paragraph long description — which matters
  more than it looks, because the store page renders the long description and **never renders a
  README**. Screenshot and featured-image URLs are deliberately left empty: there are no hosted
  screenshots yet and a made-up URL would only 404 in the listing.
- **The release path has now been run for the first time.** `Build-Release.ps1 -Tag v0.6.1.0
  -Repository zakky2k/AstroScripts -Channel Beta` built Release, ran the suite, and wrote
  `artifacts/release/UnphasedNina.0.6.1.0.zip` (UnphasedNina.dll, UnphasedCore.dll, astronomy.dll,
  LICENSE.txt, the .pdb) plus a manifest with `Type: ARCHIVE`, a SHA256 checksum and the beta
  channel; `Verify-ReleaseArtifacts.ps1` then re-checked the checksum, the URL and that the version
  matches the tag. Previously this folder had never existed, and without `-Repository`/`-Tag` the
  packaging quietly emits `https://example.invalid/releases/...`, which the catalogue CI would
  reject.
- **A hole in the repository, found by trying to publish it**: the whole of `UnphasedNina/build/` —
  `Plugin.targets` (which is what `StagePlugin`/`DeployPlugin`/`PackagePlugin` live in), the
  packaging and verification scripts, and `build/vendor/` with the schema and `CreateManifest.ps1` —
  was **never committed**, because the parent repository's `.gitignore` ignores `build/` for web
  tooling and that pattern matches at any depth. The release path above only worked because those
  files exist on this machine; a fresh clone could not have built or packaged the plugin at all.
  `git ls-files` shows zero tracked files under that folder; the ignore now carves out
  `nina-plugin/UnphasedNina/build/` specifically and the tooling is committed.
- **Two facts about that CI worth knowing before submitting**: the catalogue is
  `isbeorn/nina.plugin.manifests`, JSON only (the binaries stay here), and its validator
  **downloads `Installer.URL` and re-hashes the bytes**. So the GitHub release has to exist, and be
  byte-identical afterwards, before the manifest PR goes in. Submission layout for this plugin:
  `manifests/U/Unphased Night Planner/3.2.0.9001/0.6.2.0/manifest.json`.
- **New tests (suite now 57 passing).** `PluginPublishingTests` builds the store manifest out of the
  built assembly and validates it against the schema N.I.N.A. itself uses — the same
  `PluginManifest.Schema` that decides whether a plugin appears in the list at all — using the
  schema file vendored unmodified under `build/vendor/`. It also fails on text that breaks the
  limits (name ≤ 50, short description ≤ 256, long description ≤ 10 000, ≤ 20 tags of ≤ 30
  characters), on anything still saying "A NINA plugin.", and on a changelog URL that is not this
  repository's. `TeardownReleasesTheSettingsSingletonSoReloadingThePluginStartsFresh` covers the
  unload case that the settings singleton makes possible: after `Teardown` the singleton is gone, a
  reload against a different profile service gets a live object of its own, and the disposed one no
  longer listens to the old service.
- **`THIRD-PARTY-NOTICES.txt`** now sits next to LICENSE.txt and ships in the archive, because
  `astronomy.dll` is redistributed: Astronomy Engine 2.1.19 by Don Cross, MIT, read off the
  package's own `.nuspec` rather than assumed. The README was still the plugin template's and
  described a NINA 3.3/.NET 10 plugin, which this is not; it now describes the real install folder,
  the real requirements, the release flow and the disclosure the catalogue asks for (AI-assisted
  development, named human maintainer).
- **The single-night axis question, answered with pictures rather than an opinion.** One night,
  Prague, 24 Nov 2026, rendered twice at 760×300: noon→noon as it ships, and sunset (16:02) →
  sunrise (07:37, 935 minutes). The night-anchored version fills the width with the part that
  matters and the curves come out right from the same samples — but it is **not** just a different
  anchor: the clock row is hard-coded to noon (`NightChartControl.cs:198`,
  `$"{(12 + min / 60) % 24:00}:00"`) and the gridlines step from the frame start rather than
  snapping to whole hours, so the second frame's labels claim 12:00 at what is really 16:02. The
  series themselves are fine because `XAt` works off `Payload.FrameNoon`, which is `init`-only, so
  the experiment needed a copy of the payload. No shipping axis behaviour was changed; the
  experiment fixture was temporary and is gone. **Decision, after looking at both frames: keep
  noon → noon** — the one-night panel stays consistent with the multi-night frames and with the
  scrub convention. If this is ever revisited, the label/gridline/scrub work above is the price.
- 0.6.1.0, as deployed, started cleanly: two launches today produced log files with **no ERROR
  lines** at all. The colour fix is now **confirmed by the user** ("the color picker is working"), so what
  remains is presentation only — the swatch is decorative and the hex box is doing too much work.

- **0.6.2.0 is deployed** (six files now, `THIRD-PARTY-NOTICES.txt` among them, every one SHA256-identical to the
  staging folder; `ProductVersion 0.6.2.0+8cdcce1`). N.I.N.A. was restarted against it and wrote no log file, which is
  the quietest signature available — and worth knowing, because **N.I.N.A. does not log a successful plug-in load at the
  default log level**: healthy sessions leave a 1,153-byte file with only the version banner and one `NINALogWatcher`
  warning, while the 0.6.0.0 crash left the `TargetParameterCountException` line and the abandoned 3.3-targeted builds
  left `Failed to load plugin … version 1.0.0.0 / requires a minimum version of 3.3.0.1060`. "No news" is the good
  news; a human still has to see the panel.
- Still rough: the catalogue submission itself (no GitHub release, no manifest PR) and therefore no
real `Installer.URL`; per-file MPL-2.0 headers are missing from the plugin's own sources; the suite still writes the live settings
file; NUnit warns that the marshalling fixture's `Timeout` attribute is obsolete (it wants
`CancelAfter`); and no hosted screenshots exist for the listing. The visual round on 0.6.2.0 has now happened and its
findings are planned out rather than left vague — the two real defects it turned up are that **Up/Down arrows move
keyboard focus out of the chart into the night list and Left/Right then stop working** (the chart's `OnKeyDown` has no
Up/Down case, so WPF's directional navigation takes over, and nothing sets `IsTabStop`, so Tab cannot get back — the
chart recovers only by clicking it), and that **paging past the edge of the computed window silently snaps the selection
back to the middle** instead of computing more nights, which a test currently pins. Both are fixed in the round-8 plan in
`HANDOFF.md`, together with the button sizing, the two-sided altitude labels and the Options-page rough edges.
- **The plugin icon is now set** (item 1.b of that review, settled before any of the other round-8 work): the desktop
  icon's 256 px frame, re-encoded as `assets/unphased-256.png` (256×256, transparent corners) and referenced from the
  `FeaturedImageURL` assembly metadata, which is the one value N.I.N.A. draws both in the plugin list (25 px) and on the
  plugin's settings page (150 px) — it replaces the default jigsaw in both places. It ships in the DLL, so it becomes
  visible on the **next deploy**, not the deployed 0.6.2.0. Two honest caveats: the URL points into this repository and
  must stay there, and N.I.N.A. shows a blank rather than the jigsaw if the image ever goes missing. Panel screenshots
  for the store page are still not hosted, so `ScreenshotURL`/`AltScreenshotURL` remain empty.

## 0.6.1.0 — 0.6.0.0 crashed N.I.N.A. on start-up, and this is the fix

- **0.6.0.0 would not start.** On launch N.I.N.A. offered the "unhandled exception, should it
  recover" dialog, and after recovering the options page was there but the panel had no
  chart. The log held one useful line: `System.Reflection.TargetParameterCountException:
  Parameter count mismatch`, raised inside `System.Windows.Threading.ExceptionWrapper` with
  **no plugin frame anywhere in the stack**, which is why it looked like an application fault
  rather than a plugin one.
- The cause was the bounded dispatch added in 0.6.0.0. Asking `Dispatcher.Invoke` for both a
  return value and a timeout routes the call through reflection with an argument list, and a
  delegate that takes no arguments comes back as `TargetParameterCountException` — thrown on
  the interface thread, inside whatever was pumping at the time. The repair is
  `ui.Invoke(action)` with nothing else: WPF has **no** `Invoke(Action, priority)` overload, so
  writing `ui.Invoke(action, DispatcherPriority.Normal)` re-binds the same call to
  `Invoke(Delegate, priority, params object[])` — it looked plain, compiled, and crashed the
  test host with the identical exception. Guarding against a dispatcher that has stopped
  processing is now a check up front rather than a deadline, so a settings change arriving
  while N.I.N.A. closes still cannot hang it.
- `PlannerThreadMarshallingTests` reproduces the shape of the failure: a colour change from a
  worker thread onto a dispatcher running its real message loop. Against the 0.6.0.0 call it
  fails with that same `TargetParameterCountException`; against this build the announcement
  arrives on the interface thread. Suite: **52 passing**.
- Two rules the harness taught, both of which cost a hung test run before being written down:
  the test process may hold exactly **one** `System.Windows.Application`, and `CompositionTests`
  owns it; and a hand-pushed `DispatcherFrame` pushes at Normal, so an exit item queued below
  that floor leaves the frame waiting for ever.
- Sharpened from 0.6.0.0: the suite does use the same path the live application uses,
  `%LOCALAPPDATA%\NINA\PluginSettings\<id>\settings.json`, so treat a suite run as something
  that can touch picked colours — the tests that change colours do reset them, and in these
  runs the live file kept its earlier stamp and empty colours, so re-pick a colour after a
  suite run rather than trusting it survived.

Still rough: the marshalling fixture ends on a five-second watchdog, which is where most of the
suite's wall time now goes; the single settings object is still one the plugin creates itself,
so unloading the plugin without `Teardown` leaving the old instance behind has not been
exercised inside a running application; the colour and `-` button fixes are proven by tests and
by the rendered frames, but not yet by eye in the panel. And the "is it loaded" check I can run
from here is weak — a running N.I.N.A. with no log file and no `Unphased*` entry in its module
list is what the previous launch looked like, so the confirmation that matters is the panel
actually drawing a chart.

## 0.6.0.0 — picked colours arrive, the minus button works, and nothing lies on the floor

- **A colour picked on the options page now repaints the chart, without an Apply button.**
  The wiring was live all along; there were two settings objects. N.I.N.A. composes a
  plugin's manifest at `PluginLoader.cs:356` and its dockable panels separately at `:464`,
  and `GetContainer` (`:491`) builds a **new** `CompositionContainer` for each call — so
  `[PartCreationPolicy(Shared)]` gave the options page one `PluginSettings` and the panel
  another. Both write the same `settings.json`, and the panel's copy (all three colours
  empty) was written last, which is why the file read
  `{"sunColor":"","moonColor":"","horizonColor":""}` seconds after a pick. The plugin now
  asks for the object itself through `PluginSettings.For`, so the manifest and the panel
  hold the same instance. Side effect, for the better: the night-mode checkbox on the
  options page reaches the panel too, which it never did before.
- **The `-` button un-greys.** A WPF button's enabled state is the AND of its own
  `IsEnabled` and its command's `CanExecute`, so the binding added in 0.5.0.0 was still
  being overridden by the command's predicate, and a dockable pane does not reliably
  re-query commands anyway. The zoom commands now always allow execution, the binding is
  the only thing that greys a button, and clicking past an end of the range is a no-op
  rather than a refusal.
- **The sun and moon tracks stop at the bottom of the chart.** The data was already cut —
  the polyline is split into runs where a track leaves the window — but the renderer began a
  new figure only at the very first point of the whole string, so the pen joined the runs
  with a straight line across every gap. On a one-night Prague frame that was **518 pixels**
  of sun colour hugging the bottom edge, which reads as the sun sitting on the floor for
  hours. Each run is now its own figure; re-rendered frames measure 0–2 pixels of track
  colour along the bottom edge.
- A settings change or a finished calculation that reaches the panel while N.I.N.A. is
  closing can no longer hang the application: the dispatch onto the interface thread has a
  two-second deadline and falls back to the calling thread instead of waiting on a
  dispatcher that will never run again. (Kept as history: the deadline was the right idea and
  the way it was asked for crashed the application — see 0.6.1.0.)

Still rough: the single settings object is one the plugin creates itself, so unloading the
plugin without calling `Teardown` would leave the old instance behind — reload has not been
exercised inside the running application; the colour fix is proven by the file and by tests,
but its effect on the live panel has to be confirmed by eye; the test suite still writes the
real plugin options file, so running it clears any colours picked in the live app; scrubbing
to the hour of a moonrise or moonset puts the phase glyph on top of that chip; N.I.N.A.'s own
button bodies keep the application's colours in night mode; a week on a narrow panel drops
roughly every other horizon label; a single-night frame is anchored at local noon; the mouse
wheel over the chart steals panel scrolling.

## 0.5.0.0 — colours you can set, and a night mode that stays in the panel

- The **horizon is green** by default. Its old colour came from the theme's own red, which
  put it on top of the red moon track and made it read as one more curve. Night mode keeps
  a red horizon, because there everything else is red too.
- The moon's phase is written **once**, on the scrub line. The static glyph and the
  `NN% moon at NN°` chip that used to sit at the highest point of the moon's track are
  gone; the phase now reads in the scrub line's own readout
  (`13:00  sun +20°  moon +5°  23% moon`) and the glyph on the marker is larger. Where the
  moon's phase matters is the hour being looked at, not one hour chosen by the ephemeris.
- **Zoom buttons know their own limits.** The enabled state comes from the view model
  rather than from the commands: in a dockable pane nothing re-queried the `-` button after
  `+` had been clicked, so it stayed greyed and the frame could only ever grow.
- **The window is deeper ahead than behind** — 10 nights back, 20 forward, where it was 10
  either side. Planning looks forward, and a week-long frame picked near the old end of the
  window had nothing left to stitch from, which showed up as blank space on the right.
- The **options page has colour pickers** for the sun path, the moon path and the horizon.
  Each row shows a swatch of what the chart actually paints right now (a picked colour, or
  what the current scheme resolves to), a hex field that can be typed exactly, and `Pick`
  for the colour dialog. An empty field means follow the palette. **Reset colours to
  defaults** clears all three: sun and moon go back to the live N.I.N.A. theme brushes, the
  horizon back to green. The picked values are stored with the other plugin options.
- The panel has a **night button** (`night: off` / `night: on`, the same setting as the
  checkbox in the options). It puts the values of N.I.N.A.'s own red "Dark" scheme into the
  panel's resource dictionary, and WPF stops its colour lookup there — so the panel, its
  text and the chart go night-red while every other tab keeps the user's palette. The
  app-wide version of this is still N.I.N.A.'s own eye button in the title bar.

Still rough: the colour dialog has never been opened inside the running app (it is a WinForms
`ColorDialog`, the only thing in the plugin that needs WinForms); scrubbing to the hour of a moonrise or moonset
puts the phase glyph on top of that chip; the plugin can recolour what it draws, but not N.I.N.A.'s own button
style —
those styles bound the palette when the application started, so the button bodies keep the
app's colours and only their labels follow the night scheme; the chart keeps its own reds
in night mode rather than the theme's darker ones, because they were reviewed for
legibility; the test suite writes the real plugin options file, so running it clears any
colours picked in the live app; a week on a narrow panel still drops roughly every other
horizon label; a single-night frame is anchored at local noon; the mouse wheel over the
chart still steals panel scrolling.

## 0.4.0.0 — a floor under the sky, and a week in one frame

- The altitude window runs **+90° down to −24°**. The −90 floor was never asked for and
  bought nothing: nothing on the chart happens below about −30°, and it squeezed the
  twilight bands and the sun's winter dip into the bottom tenth of the panel. −24° keeps
  the three twilight lines (−6 / −12 / −18°) far enough apart to label at panel height
  and clear of the clock row. The zenith is unchanged, so a high summer moon still
  lands where it belongs.
- **Zoom, from one night to a week.** `-` / `+` in the header, with the frame written
  between them. A wider frame is built in the plugin by stitching consecutive nights of
  the computed window into one payload (`NightFrame.For`): each slice is cut at its *own*
  local noon, so daylight saving transitions fall where they belong, and a duplicated
  moonrise/moonset at a seam is dropped. If a night dropped out of the window entirely
  (polar day or night), the stitch stops rather than bridging the hole. The clock grid
  thins as the frame widens — 4 h, then 8 h, then 12 h — or the times merge into a band.
- **Nothing draws past the edge of the frame.** The sun and moon curves are cut at both
  ends at the interpolated crossing, and twilight shading is clamped to the canvas.
  Before this a one-night chart drew 48 hours of curve and colour off the right-hand end
  of the panel, because WPF does not clip what `OnRender` paints.
- Every night in a zoomed frame gets its **own sunset / sunrise labels**, read off the
  plotted curve where it crosses the horizon rather than from the selected night's pair,
  so a label never sits where no curve crosses.
- The scrub marker's moon dot is the **same phase glyph** the peak of the night carries,
  so dragging across the chart also shows what the moon looked like at that hour. It
  sits on a small dark disc so it stays readable when a label is underneath it, and is
  left off entirely when the moon is below the plotted window — the readout still names
  its altitude.
- **Labels step around each other.** A chip that would land on one already drawn is left
  out, clock labels never slide off their own gridline, and the degrees column keeps its
  corner of the panel.

Still rough: a week on a narrow panel drops roughly every other horizon label — legible,
but less informative than zooming in; rows near the end of the computed window have
fewer nights after them, so their zoomed frame is blank on the right; a single-night
frame is anchored at local noon, so daylight occupies both ends of the panel (worth
comparing with how the desktop planner anchors its one-night view); the mouse wheel over
the chart still steals panel scrolling; zoom is covered by automated tests but has not
yet been driven by hand in the running app.

## 0.3.0.0 — the whole sky back, and a scrub marker

- The altitude window is **±90° again**. The +80 / -32 window introduced in 0.2.0.0
  made the twilight bands roomier, but it cut high moons out of the chart and pressed
  the deep half of the night against the bottom edge. The degree labels are close
  together as a result; what keeps them readable is the chips and the walk that pushes
  a crowded label down, not a shortened axis.
- The sun and moon curves now **stop where they leave the chart** instead of running
  flat along the bottom edge. A curve that goes out and comes back is drawn as separate
  runs, joined at the edge at the interpolated crossing point — a flat line along an
  edge looked like a feature of the sky.
- `sunset` / `sunrise` labels hang **under** the horizon, mirroring `moonrise` /
  `moonset` above it, so the two sets of words no longer compete for the same pixels.
- The fixed vertical marker lines are **gone** (the sunset/sunrise solids and the
  moonrise/moonset dashes). They were the verticals that looked meaningful but were not.
- In their place, an **hour-by-hour scrub marker**: click or drag on the chart, scroll
  the wheel over it, press ← / → (or Home / End, Escape to clear), or use the new
  `< hr` `hr >` `off` buttons in the header. It snaps to whole hours and prints the clock
  time with the real sun and moon altitudes at that instant, plus a dot on each curve.
  Stepping can never walk it off the panel, and it stays put when you change night, so
  the same clock hour can be compared across nights.

Still rough: the scrub input itself (drag, wheel, arrow keys) is not covered by the
automated tests — only the snapping, clamping and the readout are — and the readout chip
can land on top of the right-hand degree labels when the marker is near the right edge.

## 0.2.0.0 — chart readability pass

Nothing new is computed; the chart is made legible at panel size.

- Chart text a couple of points larger (labels 10, markers 11, illumination readout 12,
  tip 13) and the moon phase glyph 50 % bigger.
- Every label is now a chip: a tight rounded box with near-black backing and a hairline
  edge in the label's own colour, so words stay readable over the bright daylight band.
- `moonrise` / `moonset` are labelled at the horizon, where the moon actually crosses it.
- The 0° horizon gets its own heavier, brighter line, and each altitude line
  (0 / -6 / -12 / -18°) carries its degrees on the right, never stacked on each other.
- Twilight shading merged into one rectangle per stretch of band instead of one per
  sample: the faint vertical seams that read as meaningless lines across the daylight
  parts are gone. Hour gridlines every 4 hours rather than every 2.
- Altitude window narrowed from ±90° to +80 / -32: empty sky used to occupy two thirds
  of the panel, which is what squeezed the twilight bands and their labels together.
  (Reverted in 0.3.0.0: it also cut off high moons and flattened the deep night.)

## 0.1.0.0 — very early beta

First build that runs inside N.I.N.A.: a dockable **Unphased Night Planner** panel
(Image page → the puzzle-piece button in the Info bar) drawing the selected night
from N.I.N.A.'s own astronomy.

- Dockable panel with a Tonight / step-by-one-night header, a window of nights below
  the chart, and the selected row kept in sync with what the chart shows.
- Hand-drawn chart: sun altitude curve, moon track across the whole frame, the
  standard 0 / -6 / -12 / -18° twilight bands painted as background colour,
  labelled sunset/sunrise and moonrise/moonset, and a moon phase glyph — lit limb
  plus terminator, mirrored for the observer's hemisphere — at the moon's highest
  point of that night, labelled with its illumination.
- The time scale adapts to the panel: two nights side by side while they stay
  legible, otherwise one noon-to-noon night stretched to the full width.
- The astronomy is a C# port (`UnphasedCore`), checked against the web planner's
  Prague / summer / Sydney fixtures.
