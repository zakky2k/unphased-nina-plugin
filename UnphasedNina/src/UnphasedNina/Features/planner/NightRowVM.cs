using System.Globalization;
using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// One row in the night strip: pre-formatted summary labels for the list
    /// above the chart, plus the chart geometry itself.
    /// </summary>
    public sealed class NightRowVM {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public NightRowVM(NightPayload payload, double frameMinutes = NightChartVM.FullFrameMinutes) {
            Payload = payload;
            Chart = new NightChartVM(payload, frameMinutes);
            DateLabel = payload.NightStart.ToLocalTime().ToString("ddd d MMM", CultureInfo.CurrentCulture);
            DarkLabel = payload.TrueDarkness ? $"{payload.DarkMinutes} min dark" : "no true darkness";
            MoonLabel = payload.MidnightIllumFraction >= 0.995
                ? "full moon"
                : $"{Math.Round(payload.MidnightIllumFraction * 100)}% moon";
            Summary = string.Join("  -  ", new[] { DateLabel, DarkLabel, MoonLabel });

            // The three header chips. The night list used to carry these figures one row
            // per night; with a calendar picking the night there is one night on screen, so
            // its own numbers sit under it, spelled out rather than abbreviated the way a
            // scrolling list needed them to be.
            SunChip = $"{payload.Sunset.ToLocalTime():HH:mm} \u2192 {payload.Sunrise.ToLocalTime():HH:mm}";
            DarkChip = payload.TrueDarkness ? $"{FormatDuration(payload.DarkMinutes)} dark" : "no true darkness";
            MoonChip = BestMoonLabel(payload);
        }

        /// <summary>"11 h 31 m" for a night's dark minutes; short nights drop the hours.</summary>
        private static string FormatDuration(int minutes) =>
            minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} m" : $"{minutes} m";

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

        /// <summary>Chip: the night's clock span, e.g. "16:02 -&gt; 07:58".</summary>
        public string SunChip { get; }

        /// <summary>Chip: how much of it reaches true dark, e.g. "11 h 31 m dark".</summary>
        public string DarkChip { get; }

        /// <summary>Chip: the moon's best moment in the night, e.g. "moon highest 41° at 22:30".</summary>
        public string MoonChip { get; }

        /// <summary>Minutes from frame start (local noon) to sunset, for the marker line.</summary>
        public double SunsetX => Chart.XAt(Payload.Sunset);
        public double SunriseX => Chart.XAt(Payload.Sunrise);
    }
}
