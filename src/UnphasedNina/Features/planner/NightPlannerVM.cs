// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Core.Utility;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// View model behind the planner panel: computes a window of nights around the
    /// selected date for the effective site, mirroring how the Unphased web app
    /// prefetches its night strip. The window starts out reaching further forward than
    /// back, because a night planner is mostly used to look ahead, and it then grows by
    /// ten nights on whichever side the selection approaches, so paging the calendar
    /// sideways never runs out of chart. Computation runs on a background thread so the
    /// UI never blocks.
    /// </summary>
    public sealed partial class NightPlannerVM : BaseINPC {
        // Nights either side of the selection in the window that is computed first. Forward
        // is deeper on purpose: the panel is a planning tool, and a zoomed-out (multi-night)
        // frame near the end of the window has nothing left to stitch from, which shows up as
        // empty space at the right edge of the chart. These are the opening reach, not a
        // limit: paging sideways grows the window, which is what EdgeNights is for. Public so
        // the shape of the window is pinned by a test rather than by memory.
        public const int PastNights = 10;
        public const int FutureNights = 20;

        /// <summary>
        /// How close to either end of the window the selection has to come before that end
        /// moves. Two nights - one either side of the night being looked at - so a frame
        /// zoomed out to three nights still has something to draw while the new nights are
        /// being worked out.
        /// </summary>
        public const int EdgeNights = 2;

        /// <summary>How many nights are added to the side being approached, per approach.</summary>
        public const int ExtensionNights = 10;

        private const string UnavailableNightStatus = "No night is available for the selected date.";
        private readonly PluginSettings settings;
        private readonly Dispatcher ui;

        private DateTimeOffset selectedDate;
        private NightRowVM selectedNight;
        private string statusText = string.Empty;
        private bool busy;
        private Site? computedSite;
        private int computeToken;

        /// <summary>The raw single-night payloads of the window, stitched into rows by RebuildRows.</summary>
        private List<NightPayload> window = new();

        /// <summary>
        /// The calendar days the window covers, first and last inclusive: the day on which a
        /// frame's evening starts. Kept apart from <see cref="window"/> because nights the site
        /// cannot produce drop out (no darkness, no night at all), so the number of payloads
        /// held is not the number of days asked for, and because extending the window is done
        /// in days.
        /// </summary>
        private DateTime windowFirstDay, windowLastDay;

        /// <summary>Whether the current or pending window has known calendar bounds.</summary>
        private bool windowKnown;

        /// <summary>
        /// Every night computed so far, by its frame noon, kept between window rebuilds so a
        /// wider window only ever computes the nights that were not there before. Worth having:
        /// one night is some 289 sun samples, some 290 moon samples and the bisections for
        /// sunset, sunrise, each twilight depth and moonrise, so rebuilding all 41 nights to
        /// add ten was the thing to avoid. A night the site cannot produce is remembered as
        /// absent rather than retried on every pass. Under <see cref="cacheGate"/> because an
        /// older compute keeps running to the end even after a newer request has replaced it.
        /// </summary>
        private readonly Dictionary<long, NightPayload> cache = new();
        private readonly object cacheGate = new();
        private Site? cacheSite;

        private static bool IsShuttingDown(Dispatcher dispatcher) =>
            dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished;

        private void OnUi(Action action) {
            if (IsShuttingDown(ui)) return;
            bool started = false;
            try {
                // Invoke(Action) avoids the delegate/params overload's argument-count errors.
                ui.Invoke(() => {
                    if (IsShuttingDown(ui)) return;
                    started = true;
                    action();
                });
            } catch (OperationCanceledException) when (!started) {
                // Dispatcher shutdown can abort a queued invocation.
            } catch (InvalidOperationException) when (!started && IsShuttingDown(ui)) {
                // Shutdown started before the dispatcher accepted the invocation.
            }
        }

        public NightPlannerVM(PluginSettings settings) : this(settings, null) { }

        internal NightPlannerVM(PluginSettings settings, Func<FramingTarget, string> resolveTargetType) {
            this.settings = settings;
            ui = Dispatcher.CurrentDispatcher;
            this.resolveTargetType = resolveTargetType;
            selectedDate = DateTimeOffset.Now.Date;
            Nights = new ObservableCollection<NightRowVM>();
            InitializeOverlays();
            InitializeChartElements();
            settings.PropertyChanged += (_, e) => {
                if (e.PropertyName is nameof(PluginSettings.SiteLatOverride) or nameof(PluginSettings.SiteLonOverride))
                    OnUi(RequestRecompute);
                if (e.PropertyName == nameof(PluginSettings.Scheme))
                    OnUi(() => {
                        RaisePropertyChanged(nameof(Scheme));
                        RaisePropertyChanged(nameof(SchemeLabel));
                        RaisePropertyChanged(nameof(OverlayLegend));
                    });
                if (e.PropertyName is nameof(PluginSettings.SunColor) or nameof(PluginSettings.MoonColor)
                    or nameof(PluginSettings.HorizonColor))
                    OnUi(() => RaisePropertyChanged(e.PropertyName));
                if (e.PropertyName == nameof(PluginSettings.OverlayTargetsJson))
                    OnUi(ReloadTargets);
                if (e.PropertyName == nameof(PluginSettings.ActiveHorizon))
                    OnUi(HorizonProfileChanged);
            };
        }

        public ObservableCollection<NightRowVM> Nights { get; }

        /// <summary>
        /// Which palette the chart paints with (and, for Night, this panel's chrome too).
        /// Mirrored from the plugin settings, which is where the header buttons and the
        /// options page both write: global like the colours, because it is a viewing
        /// preference rather than a property of the site.
        /// </summary>
        public ChartScheme Scheme => settings.Scheme;

        /// <summary>
        /// The active scheme in words, next to the three header buttons. The buttons are
        /// the actions and this is the readout, so the panel never has to restyle
        /// N.I.N.A.'s own button to show which one is already on.
        /// <para>
        /// The words are the settings object's, not this class's: the panel's buttons say
        /// "default"/"night"/"enhanced" because 86 DIP holds nothing longer, but this readout has
        /// room for the names the options page, the description and the README use, and taking them
        /// from <see cref="PluginSettings.SchemeName"/> is what stops the two spellings drifting
        /// apart.
        /// </para>
        /// </summary>
        public string SchemeLabel => "palette: " + settings.SchemeName;

        // Chart colour overrides, mirrored from the plugin options. Empty means "follow
        // the scheme", which for the sun and the moon means the live N.I.N.A. theme
        // brush. The names match the settings properties so the chart can re-read all
        // three whenever any one of them changes.
        public string SunColor => settings.SunColor;
        public string MoonColor => settings.MoonColor;
        public string HorizonColor => settings.HorizonColor;

        /// <summary>
        /// The night the user navigated to (prev/next/today). Changing it also
        /// re-picks the matching row, so the highlighted row, the header and the
        /// chart can never drift apart.
        /// </summary>
        public DateTimeOffset SelectedDate {
            get => selectedDate;
            set {
                var day = value.Date;
                if (selectedDate == day) return;
                SetSelectedDate(day);
                ApplySelection();
            }
        }

        private void SetSelectedDate(DateTime day) {
            selectedDate = day;
            RaisePropertyChanged(nameof(SelectedDate));
            RaisePropertyChanged(nameof(SelectedDateForPicker));
        }

        /// <summary>
        /// The face the header date picker binds to. WPF's DatePicker works in plain
        /// <see cref="DateTime"/> while the model keeps its offset-aware date, so this is
        /// the proxy between them. The picker keeps its date while the chart loads or
        /// when a night is unavailable.
        /// </summary>
        public DateTime? SelectedDateForPicker {
            get => selectedDate.Date;
            set {
                // The picker does not offer an empty selection, but a null would mean
                // "no idea which night", which the panel has no honest answer for.
                if (value == null) return;
                SelectedDate = new DateTimeOffset(value.Value);
            }
        }

        /// <summary>
        /// The highlighted night row. The chart draws this row and the header
        /// captions it, so selection is owned here instead of in view glue code.
        /// A row click (two-way binding) moves the anchor date to that night.
        /// </summary>
        public NightRowVM SelectedNight {
            get => selectedNight;
            set {
                if (value != null && selectedDate.Date != value.MorningDay)
                    SetSelectedDate(value.MorningDay);
                SetSelectedNight(value);
                if (value != null) UpdateWindowForSelection();
            }
        }

        private void SetSelectedNight(NightRowVM row) {
            if (ReferenceEquals(selectedNight, row)) return;
            selectedNight = row;
            RaisePropertyChanged(nameof(SelectedNight));
            RaisePropertyChanged(nameof(SelectedDateLabel));
            RaisePropertyChanged(nameof(ScrubLabel));
            RefreshOverlays();
            RefreshCustomHorizon();
        }

        /// <summary>Header caption; follows the selected row, not just the anchor date.</summary>
        public string SelectedDateLabel =>
            selectedNight?.NightSpanLabel ?? selectedDate.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);

        /// <summary>
        /// Highlight the night whose morning is the anchor date. Growing or re-anchoring the
        /// window happens here, because this is where a selection is found not to fit.
        /// </summary>
        private void ApplySelection() {
            var match = FindSelectedNight();
            if (match == null && selectedNight == null) RaisePropertyChanged(nameof(SelectedDateLabel));
            SetSelectedNight(match);
            UpdateWindowForSelection();
        }

        private NightRowVM FindSelectedNight() => Nights.FirstOrDefault(r => r.MorningDay == selectedDate.Date);

        private void UpdateWindowForSelection() {
            var day = selectedDate.Date;
            bool outsideWindow = windowKnown && (day < windowFirstDay || day > windowLastDay);
            bool needsWindow = !windowKnown && computedSite != null && selectedNight == null;
            if (outsideWindow || needsWindow) {
                RequestRecompute();
            } else if (selectedNight != null) {
                if (StatusText == UnavailableNightStatus) StatusText = string.Empty;
                if (!Busy) TryExtendWindow();
            } else if (windowKnown && !Busy) {
                StatusText = UnavailableNightStatus;
            }
        }

        public string StatusText {
            get => statusText;
            private set { statusText = value; RaisePropertyChanged(); }
        }

        public string SiteDetails => computedSite is { } site
            ? $"Observatory {site.Lat:F4}, {site.Lon:F4} · {Nights.Count} nights loaded"
            : string.Empty;

        public bool Busy {
            get => busy;
            private set { busy = value; RaisePropertyChanged(); }
        }

        public void PrevNight() => SelectedDate = selectedDate.AddDays(-1);
        public void NextNight() => SelectedDate = selectedDate.AddDays(1);
        public void Tonight() => SelectedDate = DateTimeOffset.Now.Date;

        // ---- Zoom -------------------------------------------------------------------------
        // How many nights the chart spans, 1 (one noon to the next, like the desktop
        // planner) up to a week. Deliberately not decided by the panel width any more:
        // it was showing two nights on a wide panel without being asked, and the honest
        // answer to "I want to see the week" is a control, not a threshold. Zooming
        // rebuilds the rows from the already-computed window, stitching one day per
        // night payload (see NightFrame), so nothing is recomputed and nothing guessed.
        private int zoomNights = 1;

        public int ZoomNights => zoomNights;

        /// <summary>
        /// Whether each zoom direction is available. The header buttons bind their
        /// IsEnabled to these rather than trusting a command's CanExecute to be
        /// re-queried: in a dockable pane a click on `+` did not reliably refresh the
        /// `-` button, so it stayed greyed out at seven nights and the frame could only
        /// ever grow. A property change always reaches the button.
        /// </summary>
        public bool CanZoomIn => zoomNights > 1;
        public bool CanZoomOut => zoomNights < NightChartVM.MaxZoomNights;

        /// <summary>Header readout for the zoom, e.g. "1 night" or "5 nights".</summary>
        public string ZoomLabel => zoomNights == 1 ? "1 night" : $"{zoomNights} nights";

        public void ZoomIn() => SetZoom(zoomNights - 1);
        public void ZoomOut() => SetZoom(zoomNights + 1);

        public void SetZoom(int nights) {
            int clamped = (int)System.Math.Clamp(nights, 1, NightChartVM.MaxZoomNights);
            if (clamped == zoomNights) return;
            zoomNights = clamped;
            RaisePropertyChanged(nameof(ZoomNights));
            RaisePropertyChanged(nameof(ZoomLabel));
            RaisePropertyChanged(nameof(CanZoomIn));
            RaisePropertyChanged(nameof(CanZoomOut));
            RebuildRows();
            // A wider frame can only keep the marker inside it by re-clamping it.
            if (scrubMinutes != null) MoveScrub(scrubMinutes.Value);
        }

        /// <summary>
        /// Re-make the rows from the raw window at the current zoom. Selection is
        /// re-matched by morning date so zooming never changes which night is highlighted.
        /// This is the synchronous path (zoom, and the test fixture that hands a window
        /// over directly); a computed window arrives with its rows already stitched.
        /// </summary>
        private void RebuildRows() => ApplyWindow(window, Stitch(window, zoomNights));

        /// <summary>
        /// Stitch the raw payloads into rows at a zoom, one day per row, each row's frame
        /// spanning <paramref name="zoom"/> nights from that day on. Rows are ordinary objects
        /// - formatted labels plus chart geometry, no dispatcher and no brush touched - so the
        /// compute thread makes them: making 31 (and now more) of them inside the interface
        /// thread's callback was the one place a hitch was felt.
        /// </summary>
        private static List<NightRowVM> Stitch(IReadOnlyList<NightPayload> nights, int zoom) {
            var rows = new List<NightRowVM>(nights.Count);
            double frameMinutes = NightChartVM.FrameMinutesAtZoom(zoom);
            for (int i = 0; i < nights.Count; i++) {
                int take = System.Math.Min(zoom, nights.Count - i);
                var slice = new List<NightPayload>(take);
                for (int d = 0; d < take; d++) slice.Add(nights[i + d]);
                rows.Add(new NightRowVM(NightFrame.For(slice, zoom), frameMinutes));
            }
            return rows;
        }

        /// <summary>
        /// Put a window on the interface thread and match its rows to the current morning date.
        /// </summary>
        private void ApplyWindow(List<NightPayload> nights, List<NightRowVM> rows) {
            window = nights;
            SetSelectedNight(null);
            Nights.Clear();
            foreach (var row in rows) Nights.Add(row);
            SetSelectedNight(FindSelectedNight());
        }

        // ---- The scrub marker -------------------------------------------------------------
        // The hour the chart is being read at, as a whole-hour offset from the frame
        // start (local noon). It lives here rather than on the chart control so the
        // header buttons, the keyboard, Shift + the mouse wheel and dragging across the chart
        // all move one value, and so it outlives a change of night: "+N hours from
        // noon" is the same clock hour in every frame, which is what makes comparing
        // two nights at one glance possible.
        private double? scrubMinutes;

        /// <summary>Current scrub position in minutes from frame noon, or null when the marker is off.</summary>
        public double? ScrubMinutes => scrubMinutes;

        /// <summary>How wide a span the chart is drawing: the zoomed frame, 1 night to a week.</summary>
        public double VisibleSpanMinutes => NightChartVM.FrameMinutesAtZoom(zoomNights);

        /// <summary>
        /// The TIME SCRUB cluster's status box: where the marker is, as a clock time, or "off".
        /// Round 11 shortened this from a sentence. The cluster is captioned TIME SCRUB above the
        /// buttons and the arrows' tooltips say how to move the marker, so the box only has to
        /// answer one question - and a 230 DIP box is where the old sentence kept losing its own
        /// ending. The altitudes at the marker are printed on the chart, at the scrub line.
        /// </summary>
        public string ScrubLabel =>
            scrubMinutes == null || selectedNight == null
                ? "off"
                : selectedNight.Chart.ScrubTime(scrubMinutes.Value);

        /// <summary>Put the marker at a minute offset from local noon, snapped to the nearest whole hour.</summary>
        public void SetScrub(double minutes) => MoveScrub(NightChartVM.SnapToHour(minutes));

        /// <summary>
        /// Step the marker by whole hours. With no marker on screen yet the first
        /// press lands just after sunset of the selected night, which is where a
        /// night is actually interesting.
        /// </summary>
        public void StepScrub(int hours) {
            double from = scrubMinutes ?? NightChartVM.SnapToHour(selectedNight == null
                ? 0
                : (selectedNight.Payload.Sunset - selectedNight.Payload.FrameNoon).TotalMinutes);
            MoveScrub(from + hours * 60);
        }

        /// <summary>Take the marker off the chart.</summary>
        public void ClearScrub() {
            if (scrubMinutes == null) return;
            scrubMinutes = null;
            RaisePropertyChanged(nameof(ScrubMinutes));
            RaisePropertyChanged(nameof(ScrubLabel));
            RaisePropertyChanged(nameof(OverlayLegend));
        }

        /// <summary>Apply a scrub position within the visible span.</summary>
        private void MoveScrub(double minutes) {
            double clamped = System.Math.Clamp(minutes, 0, VisibleSpanMinutes);
            if (scrubMinutes.HasValue && System.Math.Abs(scrubMinutes.Value - clamped) < 0.01) return;
            scrubMinutes = clamped;
            RaisePropertyChanged(nameof(ScrubMinutes));
            RaisePropertyChanged(nameof(ScrubLabel));
            RaisePropertyChanged(nameof(OverlayLegend));
        }

        /// <summary>
        /// Show a window of already-computed single-night payloads at the current
        /// zoom. The compute path calls this on the UI thread; tests use it to drive
        /// zoom and stitching without a N.I.N.A. profile.
        /// </summary>
        public void ShowWindow(IReadOnlyList<NightPayload> payloads) {
            window = payloads.ToList();
            RebuildRows();
        }

        /// <summary>
        /// Compute the first window if nothing has been computed yet. The panel calls this
        /// when it is first shown; every later request comes from the calendar or the site
        /// moving, because there is deliberately no Refresh button to ask for one.
        /// </summary>
        public void EnsureComputed() {
            if (computedSite == null && !Busy) RequestRecompute();
        }

        /// <summary>
        /// Rebuild the whole window around the selected date: what a site change needs, and
        /// what a date picked far outside the window needs. There is no command for it - the
        /// panel has no Refresh button - so it stays private.
        /// </summary>
        private void RequestRecompute() {
            var site = EffectiveSiteOrNull();
            if (site == null) {
                ++computeToken;
                windowKnown = false;
                Busy = false;
                return;
            }
            var anchor = selectedDate.Date;
            windowFirstDay = anchor.AddDays(-PastNights);
            windowLastDay = anchor.AddDays(FutureNights);
            windowKnown = true;
            StartCompute(site.Value);
        }

        /// <summary>
        /// Grow whichever end of the window the selection has come within <see
        /// cref="EdgeNights"/> of, by <see cref="ExtensionNights"/> nights, on the same
        /// background path as any other request. Only the nights beyond what is already held
        /// are calculated (see the cache), so walking a month sideways costs the same as the
        /// nights walked. Growing both ends at once - a re-anchored window that is already
        /// short on both sides - is two extensions in one pass rather than a second request.
        /// </summary>
        private bool TryExtendWindow() {
            if (!windowKnown || computedSite == null) return false;
            var day = selectedDate.Date;
            bool grew = false;
            if ((day - windowFirstDay).Days <= EdgeNights) {
                windowFirstDay = windowFirstDay.AddDays(-ExtensionNights);
                grew = true;
            }
            if ((windowLastDay - day).Days <= EdgeNights) {
                windowLastDay = windowLastDay.AddDays(ExtensionNights);
                grew = true;
            }
            if (!grew) return false;
            StartCompute(computedSite.Value);
            return true;
        }

        /// <summary>The site to compute for, or null - with the reason on the status line - when none is configured.</summary>
        private Site? EffectiveSiteOrNull() {
            var siteOpt = settings.EffectiveSite();
            if (siteOpt == null) {
                StatusText = "No site available: configure astrometry in the profile or set an override in the plugin options.";
                return null;
            }
            var site = siteOpt.Value;
            return new Site(site.Lat, site.Lon);
        }

        /// <summary>
        /// Work out the current window and stitch it into rows on a background thread, then
        /// hand the result over on the interface thread. A newer request supersedes an older
        /// one by token. The selected date is resolved when the result arrives.
        /// </summary>
        private void StartCompute(Site site, List<NightPayload> existingNights = null) {
            var firstDay = windowFirstDay;
            var lastDay = windowLastDay;
            int zoom = zoomNights;
            int token = ++computeToken;
            if (!string.IsNullOrEmpty(StatusText)) StatusText = string.Empty;
            Busy = true;
            Task.Run(() => {
                List<NightPayload> nights = null;
                List<NightRowVM> rows = null;
                string error = null;
                try {
                    nights = existingNights ?? NightsFor(site, firstDay, lastDay);
                    rows = Stitch(nights, zoom);
                } catch (NightException ex) {
                    error = ex.Message;
                } catch (Exception ex) {
                    Logger.Error("Unphased planner calculation failed", ex);
                    error = "Planner calculation failed: " + ex.Message;
                }
                OnUi(() => {
                    if (token != computeToken) return;
                    if (rows == null) {
                        windowKnown = false;
                        StatusText = error;
                        Busy = false;
                        return;
                    }
                    if (zoom != zoomNights) {
                        // Stitch the same payloads for the current zoom on the worker.
                        StartCompute(site, nights);
                        return;
                    }
                    computedSite = site;
                    ApplyWindow(nights, rows);
                    StatusText = selectedNight == null ? UnavailableNightStatus : string.Empty;
                    RaisePropertyChanged(nameof(SiteDetails));
                    if (selectedNight == null || !TryExtendWindow()) Busy = false;
                });
            });
        }

        /// <summary>
        /// The payloads for firstDay..lastDay inclusive, computing only what is not already
        /// cached. Runs on the compute thread. Nights the site cannot produce drop out, and
        /// are remembered as absent so a polar window does not re-derive them every pass.
        /// </summary>
        private List<NightPayload> NightsFor(Site site, DateTime firstDay, DateTime lastDay) {
            lock (cacheGate) {
                if (!cacheSite.HasValue || cacheSite.Value != site) {
                    cache.Clear();
                    cacheSite = site;
                }
                var calc = new NightCalculator(site);
                var result = new List<NightPayload>();
                for (var day = firstDay; day <= lastDay; day = day.AddDays(1)) {
                    var noon = FrameNoonFor(day);
                    if (!cache.TryGetValue(noon.UtcTicks, out var payload)) {
                        try {
                            payload = calc.Compute(noon);
                        } catch (NightException) {
                            payload = null;
                        }
                        cache[noon.UtcTicks] = payload;
                    }
                    if (payload != null) result.Add(payload);
                }
                return result;
            }
        }

        /// <summary>
        /// The night frame pivot follows the Unphased convention: local noon of (day - 1
        /// day), so the frame covers the night that starts on that day's evening.
        /// </summary>
        private static DateTimeOffset FrameNoonFor(DateTime day) {
            var tz = TimeZoneInfo.Local;
            var localNoon = new DateTime(day.Year, day.Month, day.Day, 12, 0, 0) - TimeSpan.FromDays(1);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localNoon, tz), TimeSpan.Zero);
        }

        /// <summary>Site the currently shown nights were computed for (null before first compute).</summary>
        public Site? ComputedSite => computedSite;
    }
}
