using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// Chart geometry for one chart frame, in abstract canvas units. The X axis
    /// spans the frame the chart is drawing - one night, or up to a week of
    /// stitched nights when the user zooms out - and Y maps the visible altitude
    /// band to 0..Height. The view scales these by its own pixel size, which keeps
    /// this class testable and free of WPF types.
    /// </summary>
    public sealed class NightChartVM {
        public const double Width = 1000;
        public const double Height = 320;

        /// <summary>A single night payload covers 48 hours (noon to noon+2).</summary>
        public const double FullFrameMinutes = 48 * 60;

        /// <summary>One night: local noon to the next local noon.</summary>
        public const double OneNightMinutes = 24 * 60;

        /// <summary>
        /// How far the zoom reaches. A week is where the panel stops being readable:
        /// below roughly 150 pixels per night the hour clock and the twilight chips
        /// start landing on each other.
        /// </summary>
        public const int MaxZoomNights = 7;

        /// <summary>Minutes a frame spans at the given zoom level (1 = one night).</summary>
        public static double FrameMinutesAtZoom(int nights) =>
            System.Math.Clamp(nights, 1, MaxZoomNights) * OneNightMinutes;

        /// <summary>
        /// Gridline / hour-label spacing for a frame: four hours over one or two
        /// nights, eight over three or four, twelve beyond that. Below that the clock
        /// chips merge into a band of boxes.
        /// </summary>
        public static double HourGridMinutes(double frameMinutes) =>
            frameMinutes <= 2 * OneNightMinutes ? 240
            : frameMinutes <= 4 * OneNightMinutes ? 480 : 720;

        public NightPayload Payload { get; }
        private readonly double frameMinutes;

        /// <summary>
        /// What the canvas spans: one night by default, up to a week when the user
        /// zooms out. Every x mapping in this class is relative to it, so a zoomed-out
        /// frame needs no other change in the drawing code.
        /// </summary>
        public double FrameMinutes => frameMinutes;

        public int ZoomNights => (int)System.Math.Round(frameMinutes / OneNightMinutes);

        public NightChartVM(NightPayload payload, double frameMinutes = FullFrameMinutes) {
            Payload = payload;
            this.frameMinutes = frameMinutes > 0 ? frameMinutes : FullFrameMinutes;
        }

        /// <summary>
        /// Altitude window the chart shows, top edge first. The zenith is on the chart,
        /// so a high moon is never cut off; below the horizon the window stops just deep
        /// enough to keep the -6 / -12 / -18° twilight lines and their chips on the
        /// canvas. The full -90 domain (it was ±90 for a round, +80/-32 before that) put
        /// two thirds of the panel above an empty sky and pushed the twilight labels off
        /// the bottom. Anything deeper is cut at the edge by TrimmedPolyline rather than
        /// smeared along it.
        /// </summary>
        public const double TopAltitude = 90;
        public const double BottomAltitude = -24;

        /// <summary>Sun altitude band: TopAltitude at top, BottomAltitude at bottom.</summary>
        public static double YSun(double altitude) =>
            (TopAltitude - altitude) / (TopAltitude - BottomAltitude) * Height;

        /// <summary>Moon altitude band: same mapping as the sun curve.</summary>
        public static double YMoon(double altitude) => YSun(altitude);

        /// <summary>The three twilight depths, the same ones PlannerPalette bands the
        /// background on. They carry their own degree chips, because reading where the
        /// sky crossed from civil to nautical dark is half of what the chart is for.</summary>
        public static readonly double[] TwilightDepths = { -6.0, -12.0, -18.0 };

        /// <summary>
        /// Horizontal rules, top down: every 15 degrees that falls inside the altitude
        /// window. A fixed short list below the horizon was not enough once the zenith
        /// came onto the chart - above the horizon the eye had nothing to read a height
        /// against. The bottom edge stays unruled: that edge is where a track is cut,
        /// not an altitude anyone reads.
        /// </summary>
        public static IReadOnlyList<double> GridAltitudes { get; } = BuildGrid();

        private static IReadOnlyList<double> BuildGrid() {
            var ladder = new System.Collections.Generic.List<double>();
            for (double alt = TopAltitude; alt >= BottomAltitude; alt -= 15.0) ladder.Add(alt);
            return ladder;
        }

        /// <summary>
        /// The altitudes whose degrees get printed on the edges: the 30 degree ladder
        /// inside the window, plus the twilight depths. Coarser than the rules, because
        /// a chip every 15 degrees is a column of boxes rather than a scale.
        /// </summary>
        public static IReadOnlyList<double> LabelAltitudes { get; } = BuildLabels();

        private static IReadOnlyList<double> BuildLabels() {
            var labels = new System.Collections.Generic.List<double>();
            foreach (double alt in GridAltitudes) if (alt % 30.0 == 0.0) labels.Add(alt);
            foreach (double depth in TwilightDepths) labels.Add(depth);
            labels.Sort((a, b) => b.CompareTo(a));   // top of the canvas first
            return labels;
        }

        /// <summary>
        /// Where the degree chips sit on an edge, in real pixels for a canvas of the
        /// given height, walked top down. A chip is nudged down when the one above it
        /// has not finished, and left out entirely when the canvas has run out - a
        /// short panel gets fewer numbers, never a pile of overlapping ones. Both edges
        /// get the same plan, so they can never disagree about what they claim.
        /// </summary>
        public static IReadOnlyList<(double Altitude, double Top)> AltitudeChips(
            double height, double labelHeight) {
            var chips = new System.Collections.Generic.List<(double, double)>();
            double floor = double.MinValue;
            foreach (double alt in LabelAltitudes) {
                double centre = YSun(alt) / Height * height;
                double top = System.Math.Clamp(centre - labelHeight / 2, 0,
                    System.Math.Max(0, height - labelHeight));
                // One pixel of air, no more. Below the horizon the twilight depths are six
                // degrees apart, which at the height the chart asks for is a hair closer
                // than a degree chip is tall, so every extra pixel of pushing spends
                // itself and the deepest number falls off the bottom of the panel.
                if (top < floor + 1) top = floor + 1;
                if (top + labelHeight > height) break;
                chips.Add((alt, top));
                floor = top + labelHeight;
            }
            return chips;
        }

        /// <summary>X position for an instant on the abstract canvas (the frame spans 0..Width).</summary>
        public static double XAt(NightPayload payload, DateTimeOffset t, double frameMinutes = FullFrameMinutes) =>
            (t - payload.FrameNoon).TotalMinutes / frameMinutes * Width;

        public double XAt(DateTimeOffset t) => XAt(Payload, t, frameMinutes);

        /// <summary>Canvas x for a minute offset from the frame start (local noon).</summary>
        public static double XAtMinutes(double minutes, double frameMinutes = FullFrameMinutes) =>
            minutes / frameMinutes * Width;

        /// <summary>Minute offset from the frame start for a canvas x - the inverse of XAtMinutes.</summary>
        public static double MinutesAtX(double x, double frameMinutes = FullFrameMinutes) =>
            x / Width * frameMinutes;

        /// <summary>The instant a minute offset from the frame start points at.</summary>
        public DateTimeOffset TimeAtMinutes(double minutes) => Payload.FrameNoon.AddMinutes(minutes);

        /// <summary>
        /// Snap a minute offset to the nearest whole hour. The frame starts at local
        /// noon, so whole hours from the frame start are whole clock hours.
        /// </summary>
        public static double SnapToHour(double minutes) =>
            System.Math.Round(minutes / 60, System.MidpointRounding.AwayFromZero) * 60;

        private System.Collections.Generic.List<(System.DateTimeOffset Time, double Altitude)> sunPoints, moonPoints;

        private System.Collections.Generic.List<(DateTimeOffset Time, double Altitude)> SunPoints =>
            sunPoints ??= WithinFrame(ToPoints(Payload.SunSeries));

        private System.Collections.Generic.List<(DateTimeOffset Time, double Altitude)> MoonPoints =>
            moonPoints ??= WithinFrame(ToPoints(Payload.MoonSamples));

        private static System.Collections.Generic.List<(DateTimeOffset Time, double Altitude)> ToPoints(
            System.Collections.Generic.IReadOnlyList<SunSample> series) {
            var pts = new System.Collections.Generic.List<(DateTimeOffset, double)>(series.Count);
            foreach (var s in series) pts.Add((s.Time, s.Altitude));
            return pts;
        }

        private static System.Collections.Generic.List<(DateTimeOffset Time, double Altitude)> ToPoints(
            System.Collections.Generic.IReadOnlyList<MoonSample> series) {
            var pts = new System.Collections.Generic.List<(DateTimeOffset, double)>(series.Count);
            foreach (var m in series) pts.Add((m.Time, m.Altitude));
            return pts;
        }

        /// <summary>
        /// Cut a series to the frame being drawn, inserting an interpolated sample at
        /// each frame edge the curve crosses. A one-night chart still receives a
        /// payload carrying 48 hours, and drawing all of it pushed the curve, the
        /// twilight shading and the sunset chips past the right edge of the panel -
        /// a week-wide frame then had labels for nights that were not on screen.
        /// </summary>
        private System.Collections.Generic.List<(DateTimeOffset Time, double Altitude)> WithinFrame(
            System.Collections.Generic.List<(DateTimeOffset Time, double Altitude)> series) {
            var from = Payload.FrameNoon;
            var to = Payload.FrameNoon.AddMinutes(frameMinutes);
            var kept = new System.Collections.Generic.List<(DateTimeOffset, double)>(series.Count);
            for (int i = 0; i < series.Count; i++) {
                var t = series[i].Time;
                if (t < from) continue;
                if (t > to) {
                    // The curve was still on screen at the previous sample, so end it
                    // exactly at the frame edge rather than at the last whole sample.
                    if (kept.Count > 0) kept.Add((to, Interpolate(series[i - 1], series[i], to)));
                    break;
                }
                if (kept.Count == 0 && i > 0 && t > from)
                    kept.Add((from, Interpolate(series[i - 1], series[i], from)));
                kept.Add(series[i]);
            }
            return kept;
        }

        private static double Interpolate(
            (DateTimeOffset Time, double Altitude) a,
            (DateTimeOffset Time, double Altitude) b,
            DateTimeOffset t) {
            double span = (b.Time - a.Time).TotalMinutes;
            double f = span <= 0 ? 0 : (t - a.Time).TotalMinutes / span;
            return a.Altitude + f * (b.Altitude - a.Altitude);
        }

        /// <summary>
        /// Sun curve over the whole frame, cut off where it leaves the altitude window.
        /// </summary>
        public string SunPolyline => TrimmedPolyline(Payload, SunPoints, YSun, frameMinutes);

        /// <summary>
        /// Moon track over the whole frame, like the sun curve: the moon is worth
        /// watching before sunset and after sunrise too, and a track that stops at the
        /// horizon reads as if the moon were missing.
        /// </summary>
        public string MoonPolyline => TrimmedPolyline(Payload, MoonPoints, YMoon, frameMinutes);

        /// <summary>
        /// The path data for a curve: points joined by spaces, and separate drawable
        /// runs separated by " | ". A curve that leaves the altitude window **stops at
        /// the edge** and the next run starts where it comes back - clamping each
        /// sample to the edge instead used to draw a flat horizontal line along the
        /// bottom of the panel, which reads as an event that never happened.
        /// </summary>
        public static string TrimmedPolyline(
            NightPayload payload,
            System.Collections.Generic.IReadOnlyList<(DateTimeOffset Time, double Altitude)> series,
            System.Func<double, double> yOf,
            double frameMinutes = FullFrameMinutes) {
            var sb = new System.Text.StringBuilder();
            bool open = false;          // a run is currently being written
            double px = 0, py = 0;
            bool pInside = false, pAbove = false;

            for (int i = 0; i < series.Count; i++) {
                double x = XAt(payload, series[i].Time, frameMinutes);
                double y = yOf(series[i].Altitude);
                bool inside = y >= 0 && y <= Height;
                bool above = y < 0;

                if (i == 0) {
                    if (inside) { AppendPoint(sb, x, y, ref open); open = true; }
                } else if (inside && pInside) {
                    AppendPoint(sb, x, y, ref open);
                    open = true;
                } else if (inside && !pInside) {
                    // Coming back in: start a fresh run at the edge, then the sample.
                    var (ex, ey) = EdgeCrossing(px, py, x, y, pAbove);
                    AppendPoint(sb, ex, ey, ref open);
                    AppendPoint(sb, x, y, ref open);
                    open = true;
                } else if (!inside && pInside) {
                    // Leaving: end the run exactly at the edge.
                    var (ex, ey) = EdgeCrossing(px, py, x, y, above);
                    AppendPoint(sb, ex, ey, ref open);
                    open = false;
                } else if (above != pAbove) {
                    // Outside at both ends but on opposite sides, so the segment
                    // crossed the whole window: show the part inside it.
                    var (ax, ay) = EdgeCrossing(px, py, x, y, pAbove);
                    var (bx, by) = EdgeCrossing(px, py, x, y, above);
                    AppendPoint(sb, ax, ay, ref open);
                    AppendPoint(sb, bx, by, ref open);
                    open = false;
                }

                px = x; py = y; pInside = inside; pAbove = above;
            }
            return sb.ToString();
        }

        /// <summary>Where the straight segment from an inside point to an outside point meets the window edge.</summary>
        private static (double X, double Y) EdgeCrossing(
            double px, double py, double x, double y, bool outsideIsAbove) {
            double edge = outsideIsAbove ? 0 : Height;
            double t = (edge - py) / (y - py);
            return (px + t * (x - px), edge);
        }

        private static void AppendPoint(System.Text.StringBuilder sb, double x, double y, ref bool open) {
            if (open) sb.Append(' ');
            else if (sb.Length > 0) sb.Append(" | ");
            sb.Append(x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
              .Append(',')
              .Append(y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Altitude of a series at an instant, interpolated between the two samples
        /// around it (NaN outside the frame), so a scrub readout is not tied to the
        /// sampling grid.
        /// </summary>
        public static double AltitudeAt(
            System.Collections.Generic.IReadOnlyList<(DateTimeOffset Time, double Altitude)> series,
            DateTimeOffset t) {
            if (series.Count == 0 || t < series[0].Time || t > series[series.Count - 1].Time) return double.NaN;
            for (int i = 0; i + 1 < series.Count; i++) {
                if (t < series[i + 1].Time) {
                    double span = (series[i + 1].Time - series[i].Time).TotalMinutes;
                    double f = span <= 0 ? 0 : (t - series[i].Time).TotalMinutes / span;
                    return series[i].Altitude + f * (series[i + 1].Altitude - series[i].Altitude);
                }
            }
            return series[series.Count - 1].Altitude;
        }

        public double SunAltitudeAt(DateTimeOffset t) => AltitudeAt(SunPoints, t);
        public double MoonAltitudeAt(DateTimeOffset t) => AltitudeAt(MoonPoints, t);

        /// <summary>
        /// The readout shown at a scrub position: clock time plus where the sun and
        /// the moon actually are at that instant.
        /// </summary>
        public string ScrubReadout(double minutes) {
            var t = TimeAtMinutes(minutes);
            // The illuminated fraction rides along because the moon's phase is now read
            // from the scrub glyph and nowhere else on the chart: the fixed glyph at the
            // peak of the night, which used to carry the number, is gone.
            double illum = MoonIllumAt(t);
            string phase = double.IsNaN(illum) ? string.Empty : $"   {illum * 100:F0}% moon";
            return $"{t.LocalDateTime:HH:mm}  sun {Signed(SunAltitudeAt(t))}  moon {Signed(MoonAltitudeAt(t))}{phase}";
        }

        private static string Signed(double altitude) =>
            double.IsNaN(altitude) ? "n/a"
            : altitude >= 0 ? $"+{altitude:F0}\u00b0" : $"{altitude:F0}\u00b0";

        /// <summary>
        /// Highest moon sample of the night itself (sunset to sunrise), which is
        /// where the phase glyph belongs - the useful altitude for an imaging run.
        /// </summary>
        public MoonSample NightMoonPeak() {
            MoonSample best = null;
            foreach (var m in Payload.MoonSamples) {
                if (m.Time < Payload.Sunset || m.Time > Payload.Sunrise) continue;
                if (best == null || m.Altitude > best.Altitude) best = m;
            }
            return best ?? Payload.MoonSamples[0];
        }

        /// <summary>
        /// Chip top for a label hung **under** a horizontal line. Falls back to
        /// hanging it above when the panel is too short to fit it below, so a label
        /// never leaves the canvas.
        /// </summary>
        public static double ChipBelow(double lineY, double labelHeight, double canvasHeight) =>
            lineY + 4 + labelHeight <= canvasHeight ? lineY + 4 : lineY - 4 - labelHeight;

        /// <summary>Chip top for a label hung **above** a horizontal line, mirrored fallback.</summary>
        public static double ChipAbove(double lineY, double labelHeight, double canvasHeight) =>
            lineY - 4 - labelHeight >= 0 ? lineY - 4 - labelHeight : lineY + 4;

        /// <summary>
        /// One continuous stretch of background shading: canvas x range plus the
        /// twilight band (see PlannerPalette.TwilightBand, -1 = full night).
        /// </summary>
        public readonly struct ShadingRun {
            public ShadingRun(double x0, double x1, int band) { X0 = x0; X1 = x1; Band = band; }
            public double X0 { get; }
            public double X1 { get; }
            public int Band { get; }
        }

        /// <summary>
        /// The sun series collapsed into one shading run per stretch of band, not one
        /// rectangle per sample. At canvas scale a 10-minute sample is about 3 canvas
        /// units wide, and a row of hairline-width antialiased rectangles in the same
        /// colour reads as a comb of vertical lines - the "lines that mean nothing"
        /// complaint. Neighbouring runs here share an exact edge, so no overlap
        /// fudge factor is needed either.
        /// </summary>
        public static System.Collections.Generic.List<ShadingRun> ShadingRuns(
            NightPayload payload, double frameMinutes = FullFrameMinutes) {
            var series = payload.SunSeries;
            var runs = new System.Collections.Generic.List<ShadingRun>();
            for (int i = 0; i + 1 < series.Count; i++) {
                double x0 = XAt(payload, series[i].Time, frameMinutes);
                double x1 = XAt(payload, series[i + 1].Time, frameMinutes);
                // Samples outside the frame belong to nights the chart is not showing.
                if (x1 <= 0 || x0 >= Width) continue;
                x0 = System.Math.Max(0, x0);
                x1 = System.Math.Min(Width, x1);
                int band = PlannerPalette.TwilightBand((series[i].Altitude + series[i + 1].Altitude) / 2);
                if (runs.Count > 0 && runs[runs.Count - 1].Band == band) {
                    var prev = runs[runs.Count - 1];
                    runs[runs.Count - 1] = new ShadingRun(prev.X0, x1, band);
                    continue;
                }
                runs.Add(new ShadingRun(x0, x1, band));
            }
            return runs;
        }

        /// <summary>
        /// Every crossing of the sun through the true horizon in the frame, with the
        /// direction: down is sunset, up is sunrise. The payload names the selected
        /// night's pair precisely (bisected); a zoomed-out frame needs the crossings of
        /// every night in it, and the crossing between two 10-minute samples interpolated
        /// to the minute is well inside a pixel of canvas.
        /// </summary>
        public System.Collections.Generic.List<(DateTimeOffset Time, bool Rising)> SunCrossings() {
            var found = new System.Collections.Generic.List<(DateTimeOffset, bool)>();
            var series = SunPoints;
            for (int i = 0; i + 1 < series.Count; i++) {
                double a0 = series[i].Altitude, a1 = series[i + 1].Altitude;
                if (a0 >= 0 == a1 >= 0) continue;
                double span = (series[i + 1].Time - series[i].Time).TotalMinutes;
                double f = a1 == a0 ? 0 : (0 - a0) / (a1 - a0);
                found.Add((series[i].Time.AddMinutes(f * span), a1 > a0));
            }
            return found;
        }

        private System.Collections.Generic.List<(DateTimeOffset Time, double Value)> moonIllum;

        private System.Collections.Generic.List<(DateTimeOffset Time, double Value)> MoonIllumPoints =>
            moonIllum ??= Payload.MoonSamples
                .Select(m => (m.Time, Value: m.IllumFraction))
                .ToList();

        /// <summary>
        /// What the moon looks like at a minute offset from the frame start: its
        /// illuminated fraction (interpolated like the altitude) and which limb is lit
        /// for this observer. That is all the scrub marker needs to draw the same phase
        /// glyph as the night summary instead of a plain dot, so the moon's phase can be
        /// read at the hour being scrubbed to.
        /// </summary>
        public (double Illum, bool LitOnRight) ScrubMoonGlyph(double minutes) {
            double illum = MoonIllumAt(TimeAtMinutes(minutes));
            return (illum, !string.Equals(Payload.Limb, "left", System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The moon's illuminated fraction (0..1) at an instant inside this frame.</summary>
        public double MoonIllumAt(DateTimeOffset t) => AltitudeAt(MoonIllumPoints, t);
    }
}
