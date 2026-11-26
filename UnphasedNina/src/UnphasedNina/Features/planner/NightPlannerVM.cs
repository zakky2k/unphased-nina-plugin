using NINA.Core.Utility;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
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
    public sealed class NightPlannerVM : BaseINPC {
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

        private readonly PluginSettings settings;

        private DateTimeOffset selectedDate;
        private NightRowVM selectedNight;
        private string statusText = "Waiting for a site - set latitude/longitude in the plugin options or configure astrometry in the profile.";
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
        private DateTimeOffset windowFirstDay, windowLastDay;

        /// <summary>Whether the window has ever been laid out, so the first compute sets its extent.</summary>
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

        /// <summary>The UI thread dispatcher (a plain dispatcher outside WPF, so the view model stays unit-testable).</summary>
        private static Dispatcher Ui => Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        /// <summary>
        /// Run work on the interface thread, where the chart and the options page live.
        /// <para>
        /// The call has to bind to <c>Invoke(Action)</c> and to nothing else. WPF has no
        /// <c>Invoke(Action, priority)</c> overload, so mentioning a priority - or a timeout,
        /// or a return value - re-binds it to <c>Invoke(Delegate, priority, params object[])</c>,
        /// a reflection path with an argument list, and a delegate that takes no arguments
        /// comes back as <c>TargetParameterCountException</c>: raised on the interface thread
        /// inside WPF's ExceptionWrapper, with no plugin frame anywhere in the stack. Both the
        /// timeout form (shipped in 0.6.0.0, which crashed N.I.N.A. eight seconds after
        /// start-up) and the priority form (which crashed the test host) have now been
        /// reproduced; PlannerThreadMarshallingTests covers the shape by changing a setting
        /// from a worker thread while a dispatcher runs its real message loop.
        /// </para>
        /// <para>
        /// The one real hazard left is a dispatcher that has stopped processing - a settings
        /// change can arrive while N.I.N.A. is closing, and waiting on a thread that will
        /// never run again hangs the application. That case runs the work on the calling
        /// thread instead: untidy, but these are property announcements nobody is looking
        /// at any more.
        /// </para>
        /// </summary>
        private static void OnUi(Action action) {
            var ui = Ui;
            try {
                if (ui.HasShutdownStarted || ui.HasShutdownFinished) {
                    action();
                    return;
                }
                // No priority argument, on purpose. WPF has no Invoke(Action, priority)
                // overload, so adding one re-binds the call to Invoke(Delegate, priority,
                // params object[]) - the reflection path, with an argument list, which is
                // what threw TargetParameterCountException on the interface thread and took
                // 0.6.0.0 down on start-up. Normal priority is what the queue uses anyway.
                ui.Invoke(action);
            } catch (InvalidOperationException) {
                // Finished processing between the check and the call.
                action();
            }
        }

        public NightPlannerVM(PluginSettings settings) {
            this.settings = settings;
            selectedDate = DateTimeOffset.Now.Date;
            Nights = new ObservableCollection<NightRowVM>();
            settings.PropertyChanged += (_, e) => {
                if (e.PropertyName is nameof(PluginSettings.SiteLatOverride) or nameof(PluginSettings.SiteLonOverride))
                    RequestRecompute();
                if (e.PropertyName == nameof(PluginSettings.Scheme))
                    OnUi(() => {
                        // The chart watches Scheme, the header watches SchemeLabel: both
                        // have to move together, or the buttons say one thing and the
                        // chart paints another.
                        RaisePropertyChanged(nameof(Scheme));
                        RaisePropertyChanged(nameof(SchemeLabel));
                    });
                if (e.PropertyName is nameof(PluginSettings.SunColor) or nameof(PluginSettings.MoonColor)
                    or nameof(PluginSettings.HorizonColor))
                    OnUi(() => RaisePropertyChanged(e.PropertyName));
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
        /// </summary>
        public string SchemeLabel => Scheme switch {
            ChartScheme.Night => "palette: night",
            ChartScheme.Enhanced => "palette: enhanced",
            _ => "palette: N.I.N.A.",
        };

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
                selectedDate = day;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectedDateForPicker));
                ApplySelection();
            }
        }

        /// <summary>
        /// The face the header date picker binds to. WPF's DatePicker works in plain
        /// <see cref="DateTime"/> while the model keeps its offset-aware date, so this is
        /// the one-line proxy between them; it raises alongside SelectedDate, so picking a
        /// date, pressing the arrows and clicking Tonight all leave the picker showing the
        /// night that is actually highlighted.
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
                if (ReferenceEquals(selectedNight, value)) return;
                selectedNight = value;
                RaisePropertyChanged();
                if (value != null && selectedDate.Date != value.MorningDay) {
                    selectedDate = value.MorningDay;
                    RaisePropertyChanged(nameof(SelectedDate));
                    RaisePropertyChanged(nameof(SelectedDateForPicker));
                }
                RaisePropertyChanged(nameof(SelectedDateLabel));
                // The scrub readout quotes the selected night, so it follows it too.
                RaisePropertyChanged(nameof(ScrubLabel));
            }
        }

        /// <summary>Header caption; follows the selected row, not just the anchor date.</summary>
        public string SelectedDateLabel =>
            selectedNight?.NightSpanLabel ?? selectedDate.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);

        /// <summary>
        /// Highlight the night whose morning is the anchor date. Growing or re-anchoring the
        /// window happens here, because this is where a selection is found not to fit.
        /// </summary>
        private void ApplySelection() {
            var match = Nights.FirstOrDefault(r => r.MorningDay == selectedDate.Date);
            if (match != null) {
                SelectedNight = match;
                ExtendWindowIfNeeded();
                return;
            }
            if (computedSite == null) {
                // Nothing has been computed for a site, so there is no wider window to ask
                // for: the middle of whatever is on screen stands in, which in a panel with
                // no site configured means nothing at all.
                SelectedNight = Nights.Count > 0 ? Nights[Nights.Count / 2] : null;
                return;
            }
            // A date picked out of the calendar can land outside the window altogether - a
            // typed-in date, or Tonight pressed after weeks of paging sideways. Growing one
            // side by ten nights would not reach that far, so the whole window is re-anchored
            // on the date that was asked for, on the same background path. The middle of the
            // old window stands in meanwhile; the new window is matched on the wanted date,
            // which pulls the header back to what was picked instead of leaving it stranded
            // on the stand-in.
            var wanted = selectedDate.Date;
            RequestRecompute(wanted);
            SelectedNight = Nights.Count > 0 ? Nights[Nights.Count / 2] : null;
        }

        public string StatusText {
            get => statusText;
            private set { statusText = value; RaisePropertyChanged(); }
        }

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
        private void RebuildRows() =>
            ApplyWindow(window, Stitch(window, zoomNights), selectedNight?.MorningDay ?? selectedDate.Date);

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
        /// Put a window and its stitched rows on the interface thread, keeping the selection
        /// pointed at the night it was pointed at (<paramref name="keep"/> by morning date),
        /// and at the middle of the window when that night has gone.
        /// </summary>
        private void ApplyWindow(List<NightPayload> nights, List<NightRowVM> rows, DateTime keep) {
            window = nights;
            SelectedNight = null;
            Nights.Clear();
            foreach (var row in rows) Nights.Add(row);
            var match = Nights.FirstOrDefault(r => r.MorningDay == keep);
            SelectedNight = match ?? (Nights.Count > 0 ? Nights[Nights.Count / 2] : null);
        }

        // ---- The scrub marker -------------------------------------------------------------
        // The hour the chart is being read at, as a whole-hour offset from the frame
        // start (local noon). It lives here rather than on the chart control so the
        // header buttons, the keyboard, the mouse wheel and dragging across the chart
        // all move one value, and so it outlives a change of night: "+N hours from
        // noon" is the same clock hour in every frame, which is what makes comparing
        // two nights at one glance possible.
        private double? scrubMinutes;

        /// <summary>Current scrub position in minutes from frame noon, or null when the marker is off.</summary>
        public double? ScrubMinutes => scrubMinutes;

        /// <summary>How wide a span the chart is drawing: the zoomed frame, 1 night to a week.</summary>
        public double VisibleSpanMinutes => NightChartVM.FrameMinutesAtZoom(zoomNights);

        /// <summary>Header readout: what the scrub is pointing at, or how to start one.</summary>
        public string ScrubLabel =>
            scrubMinutes == null || selectedNight == null
                ? "Scrub off - click the chart or use the hour buttons"
                : "Scrub " + selectedNight.Chart.ScrubReadout(scrubMinutes.Value);

        /// <summary>Put the marker at a minute offset from local noon, snapped to the nearest whole hour.</summary>
        public void SetScrub(double minutes) => MoveScrub(NightChartVM.SnapToHour(minutes));

        /// <summary>
        /// Step the marker by whole hours. With no marker on screen yet the first
        /// press lands just after sunset of the selected night, which is where a
        /// night is actually interesting.
        /// </summary>
        public void StepScrub(int hours) {
            double from = scrubMinutes ?? (selectedNight == null
                ? 0
                : (selectedNight.Payload.Sunset - selectedNight.Payload.FrameNoon).TotalMinutes);
            MoveScrub(NightChartVM.SnapToHour(from) + hours * 60);
        }

        /// <summary>Take the marker off the chart.</summary>
        public void ClearScrub() {
            if (scrubMinutes == null) return;
            scrubMinutes = null;
            RaisePropertyChanged(nameof(ScrubMinutes));
            RaisePropertyChanged(nameof(ScrubLabel));
        }

        /// <summary>The one place the scrub value changes: snapped to the hour, clamped to the visible span.</summary>
        private void MoveScrub(double minutes) {
            double clamped = System.Math.Clamp(minutes, 0, VisibleSpanMinutes);
            if (scrubMinutes.HasValue && System.Math.Abs(scrubMinutes.Value - clamped) < 0.01) return;
            scrubMinutes = clamped;
            RaisePropertyChanged(nameof(ScrubMinutes));
            RaisePropertyChanged(nameof(ScrubLabel));
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
            if (computedSite == null) RequestRecompute();
        }

        /// <summary>
        /// Rebuild the whole window around the selected date: what a site change needs, and
        /// what a date picked far outside the window needs. There is no command for it - the
        /// panel has no Refresh button - so it stays private.
        /// </summary>
        private void RequestRecompute() => RequestRecompute(selectedDate.Date);

        /// <summary>
        /// The same, anchored on a date that is not necessarily what the header is showing:
        /// <see cref="ApplySelection"/> asks for a window around the date that was picked,
        /// while a stand-in night is on screen.
        /// </summary>
        private void RequestRecompute(DateTime anchor) {
            var site = EffectiveSiteOrNull();
            if (site == null) return;
            windowFirstDay = new DateTimeOffset(anchor, selectedDate.Offset).AddDays(-PastNights);
            windowLastDay = new DateTimeOffset(anchor, selectedDate.Offset).AddDays(FutureNights);
            windowKnown = true;
            StartCompute(site.Value, anchor);
        }

        /// <summary>
        /// Grow whichever end of the window the selection has come within <see
        /// cref="EdgeNights"/> of, by <see cref="ExtensionNights"/> nights, on the same
        /// background path as any other request. Only the nights beyond what is already held
        /// are calculated (see the cache), so walking a month sideways costs the same as the
        /// nights walked. Growing both ends at once - a re-anchored window that is already
        /// short on both sides - is two extensions in one pass rather than a second request.
        /// </summary>
        private void ExtendWindowIfNeeded() {
            if (!windowKnown || computedSite == null) return;
            var day = selectedDate.Date;
            bool grew = false;
            if ((day - windowFirstDay).TotalDays <= EdgeNights) {
                windowFirstDay = windowFirstDay.AddDays(-ExtensionNights);
                grew = true;
            }
            if ((windowLastDay - day).TotalDays <= EdgeNights) {
                windowLastDay = windowLastDay.AddDays(ExtensionNights);
                grew = true;
            }
            if (!grew) return;
            StartCompute(computedSite.Value, selectedNight?.MorningDay ?? selectedDate.Date);
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
        /// hand the result over on the interface thread. <paramref name="keep"/> is the night
        /// the header should end up pointing at once it lands, decided by the caller rather
        /// than read here: the date that was asked for, or the night currently highlighted.
        /// A newer request supersedes an older one by token: the older one is not stopped,
        /// which is why its outcome is discarded rather than applied and why the night cache
        /// is guarded.
        /// </summary>
        private void StartCompute(Site site, DateTime keep) {
            var firstDay = windowFirstDay;
            var lastDay = windowLastDay;
            int zoom = zoomNights;
            int token = ++computeToken;
            Busy = true;
            Task.Run(() => {
                List<NightPayload> nights = null;
                List<NightRowVM> rows = null;
                string error = null;
                try {
                    nights = NightsFor(site, firstDay, lastDay);
                    rows = Stitch(nights, zoom);
                } catch (NightException ex) {
                    error = ex.Message;
                } catch (Exception ex) {
                    Logger.Error("Unphased planner calculation failed", ex);
                    error = "Planner calculation failed: " + ex.Message;
                }
                if (token != computeToken) return; // a newer request superseded this one
                OnUi(() => {
                    Busy = false;
                    if (nights == null) {
                        StatusText = error;
                        return;
                    }
                    computedSite = site;
                    ApplyWindow(nights, rows, keep);
                    StatusText = $"Site {site.Lat:F4}, {site.Lon:F4} - window {Nights.Count} nights";
                });
            });
        }

        /// <summary>
        /// The payloads for firstDay..lastDay inclusive, computing only what is not already
        /// cached. Runs on the compute thread. Nights the site cannot produce drop out, and
        /// are remembered as absent so a polar window does not re-derive them every pass.
        /// </summary>
        private List<NightPayload> NightsFor(Site site, DateTimeOffset firstDay, DateTimeOffset lastDay) {
            lock (cacheGate) {
                if (!cacheSite.HasValue || cacheSite.Value != site) {
                    cache.Clear();
                    cacheSite = site;
                }
                var calc = new NightCalculator(site);
                var result = new List<NightPayload>();
                for (var day = firstDay; day <= lastDay; day = day.AddDays(1)) {
                    var noon = FrameNoonFor(day.Date);
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
