using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// Builds the chart frame for a zoom level. Each night payload carries its own
    /// sun and moon series over 48 hours plus its own night's events, so a longer
    /// frame is made by taking **one day from each payload in turn** and stitching
    /// the pieces together: samples from local noon to the next local noon, that
    /// night's twilight transitions, that night's moonrise and moonset.
    /// Nothing is interpolated or invented at the seams - the boundary sample of one
    /// day is the first sample of the next, so the curves stay continuous and the
    /// 10-minute grid is undisturbed across the joins.
    /// The night-specific summary fields (sunset, sunrise, midnight moon) stay those
    /// of the first payload, which is the selected night: the list row, the header and
    /// the phase glyph all describe the night the user picked, not the whole frame.
    /// </summary>
    public static class NightFrame {
        /// <summary>
        /// The payload the chart draws at the given zoom: the selected night itself
        /// when zoomed to one night, otherwise that night plus the following
        /// <c>nights - 1</c> payloads. Extra payloads beyond the frame are ignored,
        /// and a window that runs short simply produces a shorter frame.
        /// </summary>
        public static NightPayload For(System.Collections.Generic.IReadOnlyList<NightPayload> fromSelected, int nights) {
            if (fromSelected == null || fromSelected.Count == 0) return null;
            if (nights <= 1 || fromSelected.Count == 1) return fromSelected[0];

            int take = System.Math.Min(nights, fromSelected.Count);
            var first = fromSelected[0];
            var frameEnd = first.FrameNoon.AddMinutes(NightChartVM.FrameMinutesAtZoom(nights));

            var sun = new System.Collections.Generic.List<SunSample>();
            var moon = new System.Collections.Generic.List<MoonSample>();
            var events = new System.Collections.Generic.List<TwilightEvent>();
            var rises = new System.Collections.Generic.List<DateTimeOffset>();
            var sets = new System.Collections.Generic.List<DateTimeOffset>();

            for (int i = 0; i < take; i++) {
                var p = fromSelected[i];
                // Day slice [this payload's noon, the next payload's noon). Using each
                // payload's own noon rather than noon + i*24 h keeps the seams exact
                // across a summer-time change, where consecutive local noons are 23 or
                // 25 hours apart. The last slice keeps its closing sample.
                if (i > 0) {
                    double gap = (p.FrameNoon - fromSelected[i - 1].FrameNoon).TotalHours;
                    if (gap < 12 || gap > 36) break;  // a night dropped out (polar): do not bridge the gap
                }
                var from = p.FrameNoon;
                bool last = i + 1 >= take;
                var sliceEnd = from.AddMinutes(2 * NightChartVM.OneNightMinutes);
                var to = last ? (frameEnd < sliceEnd ? frameEnd : sliceEnd) : fromSelected[i + 1].FrameNoon;

                foreach (var s in p.SunSeries) {
                    if (s.Time < from) continue;
                    if (last ? s.Time > to : s.Time >= to) break;
                    sun.Add(s);
                }
                foreach (var m in p.MoonSamples) {
                    if (m.Time < from) continue;
                    if (last ? m.Time > to : m.Time >= to) break;
                    moon.Add(m);
                }
                // Twilight events belong to the night that starts at this payload's
                // noon, so they are inside its own slice by construction.
                foreach (var e in p.Events) {
                    if (e.Time < from) continue;
                    if (e.Time > to) break;
                    events.Add(e);
                }
                // Moonrise/set lists cover the payload's whole 48 h, so they overlap
                // with the next payload's; the day slice keeps each crossing once, and a
                // crossing exactly on a seam belongs to the slice that starts there.
                foreach (var t in p.Moon.Rise) { if (t >= from && (last ? t <= to : t < to)) rises.Add(t); }
                foreach (var t in p.Moon.Set) { if (t >= from && (last ? t <= to : t < to)) sets.Add(t); }
            }

            return new NightPayload {
                Location = first.Location,
                FrameNoon = first.FrameNoon,
                NightStart = first.NightStart,
                NightEnd = first.NightEnd,
                Sunset = first.Sunset,
                Sunrise = first.Sunrise,
                Events = events,
                TrueDarkness = first.TrueDarkness,
                DeepestSunAltitude = sun.Count > 0 ? System.Math.Round(sun.Min(s => s.Altitude), 2) : first.DeepestSunAltitude,
                SunSeries = sun,
                MoonSamples = moon,
                Moon = new MoonEvents(rises, sets),
                Waxing = first.Waxing,
                Limb = first.Limb,
                MidnightTime = first.MidnightTime,
                MidnightIllumFraction = first.MidnightIllumFraction,
                MidnightMoonAltitude = first.MidnightMoonAltitude,
                MidnightMoonAzimuth = first.MidnightMoonAzimuth,
                DarkMinutes = first.DarkMinutes,
                MoonUpDuringDark = first.MoonUpDuringDark,
            };
        }
    }
}
