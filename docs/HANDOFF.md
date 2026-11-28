# Handoff

## Baseline

The source and [installer](../README.md) are **0.8.3.0 Beta**.
The plugin targets **.NET 8 for Windows** and requires
**N.I.N.A. 3.2.0.9001** or later. Use the **.NET 10 SDK** pinned in
[`global.json`](../global.json) and use N.I.N.A. 3.2 as the host compatibility reference.

## Where to start

- [`UnphasedCore`](../src/UnphasedCore/) calculates night data. The
  [WPF planner](../src/UnphasedNina/Features/planner/) owns date selection, zoom,
  multi-night stitching and rendering.
- The [README build commands](../README.md#build-from-source) cover both projects.
  Use the [contributor guide](../CONTRIBUTING.md) for local deployment and opt-in tests
  and the [release guide](RELEASING.md) for packaging and catalogue submission.

## Behaviour to preserve

- **Date selection and background work.** The picker identifies a night by its
  morning date, with the frame anchored at the preceding local noon. Keep the
  requested date visible while loading and when no night is available. Background
  results must respect the latest date and zoom. Window growth uses calendar days
  across daylight-saving changes. Publish UI changes on the dispatcher and drop
  queued callbacks during shutdown. See the
  [planner tests](../tests/UnphasedNina.Tests/Features/planner/).
- **Chart rendering.** Keep the shared altitude scale at **+90° to −24°** and
  clip curves at the plot boundaries. Mouse scrubbing follows the pointer
  continuously. The step controls move by an hour.
- **WPF tests.** Use [`WpfHost`](../tests/UnphasedNina.Tests/WpfHost.cs) for
  fixtures that need a WPF application. [`TestEnvironment`](../tests/UnphasedNina.Tests/TestEnvironment.cs)
  redirects N.I.N.A. data to a unique test directory to protect live settings.

- **Feature descriptions.** [`PillarIdentityTests`](../tests/UnphasedNina.Tests/Features/settings/PillarIdentityTests.cs)
  checks shared feature names and artwork. The Options page and catalogue
  listing share their full descriptions. The README has its own introduction
  and feature descriptions.
