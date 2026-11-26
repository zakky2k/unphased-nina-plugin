// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// One row in the night strip: pre-formatted summary labels for the list
    /// above the chart, plus the chart geometry itself.
    /// </summary>
    public sealed class NightRowVM {
        public NightRowVM(NightPayload payload, double frameMinutes = NightChartVM.FullFrameMinutes) {
            Payload = payload;
            Chart = new NightChartVM(payload, frameMinutes);
            DateLabel = payload.NightStart.ToLocalTime().ToString("ddd d MMM", CultureInfo.CurrentCulture);
            DarkLabel = payload.TrueDarkness ? $"{payload.DarkMinutes} min dark" : "no true darkness";
            MoonLabel = payload.MidnightIllumFraction >= 0.995
                ? "full moon"
                : $"{Math.Round(payload.MidnightIllumFraction * 100)}% moon";
            Summary = string.Join("  -  ", new[] { DateLabel, DarkLabel, MoonLabel });

            SunsetChip = $"☉ ↓ {payload.Sunset.ToLocalTime():HH:mm}";
            SunriseChip = $"☉ ↑ {payload.Sunrise.ToLocalTime():HH:mm}";
            DarkIntervals = AstronomicalDarkIntervals(payload);
            DarkChip = $"Total dark · {FormatDuration(payload.DarkMinutes)}";
            MoonlessDarkIntervals = MoonlessIntervals(payload, DarkIntervals);
            int moonlessMinutes = WholeMinutes(MoonlessDarkIntervals.Sum(interval => interval.End - interval.Start));
            MoonlessChip = $"Moonless dark · {FormatDuration(moonlessMinutes)}";
            MoonChip = BestMoonLabel(payload);
        }

        /// <summary>"11 h 31 m" for a night's dark minutes; short nights drop the hours.</summary>
        internal static string FormatDuration(int minutes) =>
            minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} m" : $"{minutes} m";

        internal static int WholeMinutes(double minutes) => (int)Math.Floor(minutes + 1e-9);

        internal IReadOnlyList<(double Start, double End)> DarkIntervals { get; }
        internal IReadOnlyList<(double Start, double End)> MoonlessDarkIntervals { get; }

        private static List<(double Start, double End)> AstronomicalDarkIntervals(NightPayload payload) {
            var intervals = new List<(double, double)>();
            double? start = null;
            foreach (var transition in payload.Events) {
                if (transition.Time < payload.NightStart || transition.Time > payload.NightEnd || transition.Band != "astronomical")
                    continue;
                double minutes = (transition.Time - payload.FrameNoon).TotalMinutes;
                if (transition.Edge == "start") start = minutes;
                else if (transition.Edge == "end" && start.HasValue && minutes > start.Value) {
                    intervals.Add((start.Value, minutes));
                    start = null;
                }
            }
            return intervals;
        }

        private static List<(double Start, double End)> MoonlessIntervals(NightPayload payload,
            IReadOnlyList<(double Start, double End)> dark) {
            var intervals = new List<(double, double)>();
            if (dark.Count == 0) return intervals;

            var crossings = payload.Moon.Rise.Select(time => (Time: time, MoonUp: true))
                .Concat(payload.Moon.Set.Select(time => (Time: time, MoonUp: false)))
                .OrderBy(crossing => crossing.Time).ToArray();
            foreach (var (darkStart, darkEnd) in dark) {
                var startTime = payload.FrameNoon.AddMinutes(darkStart);
                int first = Array.FindIndex(crossings, crossing => crossing.Time > startTime);
                if (first < 0) first = crossings.Length;
                // A rise or set identifies the state on both sides of the crossing.
                // With no crossings the Moon stays on the same side of the horizon.
                bool down = first < crossings.Length ? crossings[first].MoonUp
                    : crossings.Length > 0 ? !crossings[^1].MoonUp
                    : payload.MoonSamples.FirstOrDefault(sample => sample.Altitude != 0)?.Altitude < 0;
                double from = darkStart;
                for (int i = first; i < crossings.Length; i++) {
                    double next = (crossings[i].Time - payload.FrameNoon).TotalMinutes;
                    if (next >= darkEnd) break;
                    if (down && next > from) intervals.Add((from, next));
                    down = !crossings[i].MoonUp;
                    from = next;
                }
                if (down) intervals.Add((from, darkEnd));
            }
            return intervals;
        }

        /// <summary>
        /// When the moon is worth paying attention to: the highest it gets between sunset and
        /// sunrise of this row's own night. MoonSamples covers more than that - the payload's
        /// whole noon-to-noon-plus-two frame, and for a zoomed row the samples of several
        /// stitched nights - so the night window has to be cut out here. All three chips
        /// describe the night the row was picked for, not the wider frame the chart happens to
        /// be drawing, which is what makes them comparable while zooming.
        /// </summary>
        private static string BestMoonLabel(NightPayload payload) {
            MoonSample best = null;
            foreach (var sample in payload.MoonSamples) {
                if (sample.Time < payload.NightStart || sample.Time > payload.NightEnd) continue;
                if (best == null || sample.Altitude > best.Altitude) best = sample;
            }
            if (best == null || best.Altitude <= 0) return "moon down all night";
            return $"moon highest {Math.Round(best.Altitude)}\u00b0 at {best.Time.ToLocalTime():HH:mm}";
        }

        public NightPayload Payload { get; }
        public NightChartVM Chart { get; }

        /// <summary>
        /// Local calendar day the night starts on (sunset side). This is what
        /// DateLabel shows, so it is also the day a row click is matched on.
        /// </summary>
        public DateTime EveningDay => Payload.NightStart.ToLocalTime().Date;

        /// <summary>
        /// Local calendar day the night ends on (sunrise side). The planner
        /// navigates by this date, so "25 Nov" means the night of 24/25 Nov.
        /// </summary>
        public DateTime MorningDay => EveningDay.AddDays(1);

        /// <summary>Header caption for the selected night, e.g. "Tue 24 Nov -> Wed 25 Nov 2026".</summary>
        public string NightSpanLabel =>
            $"{Payload.Sunset.ToLocalTime():ddd d MMM} \u2192 {Payload.Sunrise.ToLocalTime():ddd d MMM yyyy}";
        public string DateLabel { get; }
        public string DarkLabel { get; }
        public string MoonLabel { get; }
        public string Summary { get; }

        public string SunsetChip { get; }
        public string SunriseChip { get; }

        /// <summary>Time between astronomical dusk and dawn.</summary>
        public string DarkChip { get; }
        public string MoonlessChip { get; }
        public string MoonlessDetails => $"Time during astronomical darkness when the Moon is below the 0° horizon.\n{MoonChip}";

        /// <summary>Chip: the moon's best moment in the night, e.g. "moon highest 41° at 22:30".</summary>
        public string MoonChip { get; }

        /// <summary>Minutes from frame start (local noon) to sunset, for the marker line.</summary>
        public double SunsetX => Chart.XAt(Payload.Sunset);
        public double SunriseX => Chart.XAt(Payload.Sunrise);
    }
}
