using NINA.Core.Utility;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// Draws the night chart (sun curve, twilight bands, moon track) by code
    /// in a plain FrameworkElement. This keeps the plugin low-impact: no
    /// custom controls, no styles that could leak into other N.I.N.A. views.
    /// Colors come from PlannerPalette, asked by the active ChartScheme: the live
    /// N.I.N.A. theme by default, red night vision under NIGHT, and the plugin's own
    /// navy Enhanced chart - which changes the chart only, not the panel around it.
    /// </summary>
    public class NightChartControl : FrameworkElement {
        // The height the chart asks for. It matches the canvas in NightChartVM so the
        // vertical scale is 1:1: a 260 DIP element drawing a 320 unit canvas squeezed
        // everything by 0.81, and thirteen degrees of altitude - or seven of label -
        // in that little room turned the scale into a smear.
        private const double ChartHeight = 320;

        // Label sizes in device-independent pixels, sized for how wide the panel
        // actually gets on a modern screen: the previous 7-8 px was unreadable.
        private const double LabelFs = 10;    // altitude degrees, hour clock
        private const double MarkerFs = 11;   // sunset / sunrise / moonrise / moonset
        private const double NoteFs = 12;     // the scrub line's time / altitude / phase readout
        private const double LegendFs = 11;
        private const double TipFs = 13;

        // The two legend rows, pushed down from the very top so the altitude chips,
        // which now also run down the left edge, have their corner to themselves.
        private const double LegendRow1 = 30, LegendRow2 = 49;

        // The moon glyph rides the scrub line: it is the chart's only moon marker now
        // (the fixed one at the peak of the night is gone). The radius is deliberately
        // not scaled by the vertical stretch, or the moon turns into an ellipse, and
        // 9 px is the size at which the terminator still reads at panel height.
        private const double MoonGlyphRadius = 9.0;

        private const double ChipPadX = 3, ChipPadY = 2;

        private readonly PlannerPalette palette = new();
        private NightRowVM row;
        private ChartScheme scheme = ChartScheme.Default;

        // Mirror of the planner's scrub position (minutes from frame noon, null = off).
        // The planner owns it, this is only what OnRender draws.
        private double? scrubMinutes;

        public NightChartControl() {
            // Focusable so the arrow keys can step the scrub hour by hour.
            Focusable = true;
            // WPF's tab-stop switch is the attached KeyboardNavigation.IsTabStop, and it
            // defaults to true - but say it here rather than rely on the default, because
            // Focusable alone is what a bare FrameworkElement gives you and the chart has
            // to be reachable from the keyboard once focus has walked away from it.
            KeyboardNavigation.SetIsTabStop(this, true);
            // Arrows are the scrub control here, not a way to walk out of it.
            KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.None);
            Loaded += (_, _) => RequestRerender();
            DataContextChanged += (_, _) => HookPlanner();
        }

        private NightPlannerVM hookedPlanner;

        private void HookPlanner() {
            if (hookedPlanner != null) hookedPlanner.PropertyChanged -= PlannerChanged;
            hookedPlanner = FindPlanner(DataContext);
            if (hookedPlanner != null) {
                hookedPlanner.PropertyChanged += PlannerChanged;
                SetScheme(hookedPlanner.Scheme);
                ApplyChartColors();
                SetScrub(hookedPlanner.ScrubMinutes);
            } else {
                // A chart rendered on its own (a test, a preview) has no planner to take
                // picked colours from, so it draws the scheme as configured.
                palette.SetOverrides(null, null, null);
                SetScrub(null);
            }
        }

        /// <summary>
        /// Take the three pickable chart colours from the planner, which mirrors the
        /// plugin options. Empty means "follow the scheme"; the palette sorts that out,
        /// including a colour that cannot be parsed.
        /// </summary>
        private void ApplyChartColors() {
            palette.SetOverrides(hookedPlanner?.SunColor, hookedPlanner?.MoonColor, hookedPlanner?.HorizonColor);
        }

        private static NightPlannerVM FindPlanner(object dc) => dc switch {
            NightPlannerVM p => p,
            Features.planner.PlannerDockableVM d => d.Planner,
            _ => null
        };

        private void PlannerChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(NightPlannerVM.Scheme)) SetScheme(hookedPlanner.Scheme);
            if (e.PropertyName == nameof(NightPlannerVM.ScrubMinutes)) SetScrub(hookedPlanner.ScrubMinutes);
            if (e.PropertyName is nameof(NightPlannerVM.SunColor) or nameof(NightPlannerVM.MoonColor)
                or nameof(NightPlannerVM.HorizonColor)) {
                // All three are re-read together: the palette resolves every role against
                // the whole set, and one repaint is enough for a pick.
                ApplyChartColors();
                RequestRerender();
            }
        }

        /// <summary>Attach the row whose payload should be drawn.</summary>
        public void SetRow(NightRowVM row) {
            this.row = row;
            RequestRerender();
        }

        /// <summary>
        /// The row currently attached, null before the panel glue hands one over. Read-only
        /// on purpose: the glue is the only writer, and a test that loads the real panel needs
        /// to see the hand-over happened - the chart painting a blank panel is exactly the
        /// failure the calendar replacing the night list could have caused.
        /// </summary>
        public NightRowVM Row => row;

        /// <summary>
        /// Which palette to draw with. Under the panel this comes from the planner, which
        /// mirrors the plugin settings; setting it here is how a chart rendered on its own
        /// (a preview, a test) picks a scheme.
        /// </summary>
        public void SetScheme(ChartScheme chosen) {
            if (scheme == chosen) return;
            scheme = chosen;
            RequestRerender();
        }

        /// <summary>
        /// Move the scrub marker (null takes it off). The planner view model owns the
        /// value; setting it here lets the chart be rendered on its own in tests, and
        /// is how a planner change reaches the drawing.
        /// </summary>
        public void SetScrub(double? minutes) {
            if (Nullable.Equals(scrubMinutes, minutes)) return;
            scrubMinutes = minutes;
            RequestRerender();
        }

        protected override Size MeasureOverride(Size availableSize) =>
            new(double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width, ChartHeight);

        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 10 || h <= 10) return;
            dc.DrawRectangle(palette.ChartBackground(scheme), null, new Rect(0, 0, w, h));
            if (row == null) {
                var s = LabelSize("no night selected", TipFs);
                DrawChip(dc, "no night selected", TipFs, (w - s.Width) / 2, h / 2 - s.Height / 2,
                    palette.Ink(scheme), w, h);
                return;
            }

            // The row carries the frame: one night, or as many as the zoom reaches.
            // The canvas always spans exactly that frame, so the pixel scale is a
            // plain width ratio - the zoom changes the frame, not the mapping.
            double spanMinutes = row.Chart.FrameMinutes;
            double sx = w / NightChartVM.Width;
            double sy = h / NightChartVM.Height;

            DrawTwilightShading(dc, h, sx);
            DrawGrid(dc, w, h, sx, sy, spanMinutes);
            DrawCurve(dc, row.Chart.SunPolyline, palette.SunTrack(scheme), 1.6, sx, sy);
            DrawCurve(dc, row.Chart.MoonPolyline, palette.MoonTrack(scheme), 1.2, sx, sy);
            DrawSunMarkers(dc, w, h, sx, sy);
            DrawMoonMarkers(dc, w, h, sx, sy);
            DrawScrub(dc, w, h, sx, sy, spanMinutes);
            DrawLegend(dc);
        }

        /// <summary>
        /// Boxes the axis labels (hour clock, degrees) already occupy. They are drawn
        /// before the event chips, so the chips can step around them instead of
        /// overprinting a number the chart is read by.
        /// </summary>
        private readonly List<Rect> axisChips = new();

        private void DrawGrid(DrawingContext dc, double w, double h, double sx, double sy, double spanMinutes) {
            var faint = new Pen(palette.Gridline(scheme), 0.6);
            var horizon = new Pen(palette.Horizon(scheme), 1.6);
            var ink = palette.Ink(scheme);
            axisChips.Clear();

            // The horizon is the reference line every altitude is read against, so it
            // gets a heavier pen and is drawn last, over the faint twilight lines.
            // Above the horizon the same faint pen is used: the rules are a scale, not
            // an event, and a heavier one up there would compete with the sun track.
            foreach (double alt in NightChartVM.GridAltitudes) {
                double y = NightChartVM.YSun(alt) * sy;
                dc.DrawLine(alt == 0.0 ? horizon : faint, new Point(0, y), new Point(w, y));
            }
            // Hour lines on the ladder from NightChartVM: 4 h over one or two nights,
            // sparser as the zoom widens, or the clock chips merge into a solid band.
            int step = (int)NightChartVM.HourGridMinutes(spanMinutes);
            for (int min = 0; min <= spanMinutes; min += step) {
                double x = min / spanMinutes * NightChartVM.Width * sx;
                dc.DrawLine(faint, new Point(x, 0), new Point(x, h));
            }
            // Every rule that carries a number prints it on both edges, boxed so the
            // band behind it cannot swallow the digit. A chart this wide has far more
            // sky than edges, and reading a height near the middle meant walking the
            // eye all the way right; now the number is never further away than the
            // nearer side. Walked top to bottom, a chip pushed down when the panel is
            // too short for the gap and dropped when there is none left, so the two
            // columns never pile into each other. The degrees are laid out before the
            // clock, because they own both side columns.
            double labelHeight = LabelSize("-18\u00b0", LabelFs).Height;
            double degreesLeft = w, clockStart = 0;
            foreach (var (alt, top) in NightChartVM.AltitudeChips(h, labelHeight)) {
                string degrees = $"{alt:F0}\u00b0";
                var s = LabelSize(degrees, LabelFs);
                var chipInk = alt == 0.0 ? palette.Horizon(scheme) : ink;
                degreesLeft = Math.Min(degreesLeft, w - s.Width - 3);
                // The clock labels belong to their own hour line, but the line at the
                // very start of the frame has no room between the edge and the left
                // column, so that column's width becomes the floor for the clock row.
                clockStart = Math.Max(clockStart, 3 + s.Width + 4);
                axisChips.Add(new Rect(3, top, s.Width, s.Height));
                axisChips.Add(new Rect(w - s.Width - 3, top, s.Width, s.Height));
                DrawChip(dc, degrees, LabelFs, 3, top, chipInk, w, h);
                DrawChip(dc, degrees, LabelFs, w - s.Width - 3, top, chipInk, w, h);
            }
            for (int min = 0; min < spanMinutes; min += step) {
                double x = min / spanMinutes * NightChartVM.Width * sx;
                string clock = $"{(12 + min / 60) % 24:00}:00";
                var s = LabelSize(clock, LabelFs);
                // A clock label belongs to its gridline, so it is never slid sideways
                // past the column that owns the edge: where the line runs out of room
                // before a degrees column, the label is left out and the line still
                // says what it means.
                double left = Math.Max(x + 3, clockStart);
                if (left + s.Width > degreesLeft - 4) continue;
                double clockTop = Math.Clamp(h - s.Height - 3, 0, Math.Max(0, h - s.Height));
                var box = new Rect(left, clockTop, s.Width, s.Height);
                // A week on a narrow panel has more gridlines than room for their times:
                // every line stays, a time label only appears where there is space for
                // it, so the clock row never turns into one grey bar.
                if (axisChips.Any(taken => taken.IntersectsWith(box))) continue;
                axisChips.Add(box);
                DrawChip(dc, clock, LabelFs, left, clockTop, ink, w, h);
            }
        }

        /// <summary>
        /// Paint the day / civil / nautical / astronomical twilight transition as
        /// background colour over time, so sunset to sunrise reads at a glance
        /// instead of relying on the horizontal altitude gridlines alone. One
        /// rectangle per stretch of band (see NightChartVM.ShadingRuns), which is
        /// what keeps the band edges from looking like stray vertical lines.
        /// </summary>
        private void DrawTwilightShading(DrawingContext dc, double h, double sx) {
            foreach (var run in NightChartVM.ShadingRuns(row.Payload, row.Chart.FrameMinutes)) {
                var brush = palette.TwilightBandBrush(scheme, run.Band);
                if (brush == null) continue;
                double x0 = run.X0 * sx, x1 = run.X1 * sx;
                if (x1 <= x0) continue;
                dc.DrawRectangle(brush, null, new Rect(x0, 0, x1 - x0, h));
            }
        }

        /// <summary>
        /// Draw a curve. Its path data can carry several runs separated by " | ": a
        /// curve that leaves the altitude window stops dead at the edge and the next
        /// run starts where it comes back (see NightChartVM.TrimmedPolyline), so each
        /// run begins a fresh figure. Joined into one figure instead, the gap would be
        /// bridged by a line hugging the edge - the flat horizontal line that looked
        /// like a feature of the sky.
        /// </summary>
        private void DrawCurve(DrawingContext dc, string polyline, Brush brush, double thickness, double sx, double sy) {
            if (string.IsNullOrEmpty(polyline)) return;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open()) {
                foreach (var run in polyline.Split('|')) {
                    // A fresh figure per run, not per curve: with one figure the gap where
                    // the track is below the floor gets bridged by a straight LineTo
                    // hugging the bottom edge, which reads as the sun or moon sitting on
                    // the floor for hours. Regression-tested on pixels by
                    // PlannerChartPixelTests, which measures the widest flat run of sun
                    // colour along the bottom edge: 260 px with one figure, a handful now.
                    bool first = true;
                    foreach (var pair in run.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
                        var xy = pair.Split(',');
                        if (xy.Length != 2) continue;
                        var pt = new Point(
                            double.Parse(xy[0], CultureInfo.InvariantCulture) * sx,
                            double.Parse(xy[1], CultureInfo.InvariantCulture) * sy);
                        if (first) { ctx.BeginFigure(pt, false, false); first = false; }
                        else ctx.LineTo(pt, true, false);
                    }
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(brush, thickness), geo);
        }

        /// <summary>
        /// Label sunset and sunrise where they happen, at the horizon, with the chip
        /// hung **under** the line so it does not collide with the moonrise/moonset
        /// chips, which hang above it. No full-height marker lines: they competed with
        /// the scrub line and read as verticals that mean nothing.
        /// </summary>
        private void DrawSunMarkers(DrawingContext dc, double w, double h, double sx, double sy) {
            var accent = palette.Accent(scheme);
            double horizonY = NightChartVM.YSun(0) * sy;
            var taken = new List<Rect>(axisChips);
            // Every horizon crossing in the frame, not just the selected night's pair:
            // zoomed out, each night in the frame gets its own sunset and sunrise chip.
            // Read off the plotted sun curve itself, so the label always sits where the
            // curve it describes actually crosses.
            foreach (var crossing in row.Chart.SunCrossings()) {
                double x = row.Chart.XAt(crossing.Time) * sx;
                if (x < 2 || x > w - 2) continue;
                string name = crossing.Rising ? "sunrise" : "sunset";
                var s = LabelSize(name, MarkerFs);
                double top = NightChartVM.ChipBelow(horizonY, s.Height, h);
                if (!Fits(taken, name, MarkerFs, BoxLeft(x, name, MarkerFs, w), top, w, h)) continue;
                DrawChip(dc, name, MarkerFs, BoxLeft(x, name, MarkerFs, w), top, accent, w, h);
            }
        }

        /// <summary>
        /// Chips hung along one band - the sunset/sunrise row and the moonrise/moonset
        /// row - remember what is already there, and a chip that would land on top of
        /// another is left out. A week of nights on a narrow panel has room for about
        /// half of its labels; overlapping text reads as noise, while a missing label
        /// still leaves the curve and its crossing to point at.
        /// </summary>
        private bool Fits(List<Rect> taken, string text, double size, double x, double y, double w, double h) {
            var t = DrawText(text, size, Brushes.Black);
            double left = Math.Clamp(x, 0, Math.Max(0, w - t.Width - 2 * ChipPadX));
            double top = Math.Clamp(y, 0, Math.Max(0, h - t.Height - 2 * ChipPadY));
            var box = new Rect(left, top, t.Width + 2 * ChipPadX, t.Height + 2 * ChipPadY);
            if (taken.Any(taken0 => taken0.IntersectsWith(box))) return false;
            taken.Add(box);
            return true;
        }

        /// <summary>
        /// Left edge of a label box hung just to the right of a marker, flipped to the
        /// left of it when the box would otherwise run off the canvas.
        /// </summary>
        private double BoxLeft(double x, string text, double size, double w) {
            double left = x + 4;
            if (left + LabelSize(text, size).Width > w) left = x - 4 - LabelSize(text, size).Width;
            return Math.Clamp(left, 0, Math.Max(0, w - LabelSize(text, size).Width));
        }

        /// <summary>
        /// Make the moon readable: moonrise/moonset crossings within the visible
        /// window, labelled where the track meets the horizon. The phase glyph used to
        /// sit here too, welded to the moon's highest point of the night with a "NN%
        /// moon at NN deg" chip beside it; that has moved onto the scrub line, where it
        /// answers the question people actually ask ("what was the moon like at 02:00")
        /// instead of a question nobody asked. One glyph, and it moves.
        /// </summary>
        private void DrawMoonMarkers(DrawingContext dc, double w, double h, double sx, double sy) {
            var p = row.Payload;
            var moon = palette.MoonTrack(scheme);
            double horizonY = NightChartVM.YSun(0) * sy;
            var taken = new List<Rect>(axisChips);

            foreach (var ev in p.Moon.Rise.Select(t => (time: t, name: "moonrise"))
                       .Concat(p.Moon.Set.Select(t => (time: t, name: "moonset")))) {
                double x = row.Chart.XAt(ev.time) * sx;
                if (x < 2 || x > w - 2) continue;
                var s = LabelSize(ev.name, MarkerFs);
                // The word sits where the moon crosses the horizon, not in the clock
                // row, and no vertical is drawn through it: the crossing is the label's
                // own position, and a full-height dash was the stray line the scrub
                // line has now replaced.
                double top = NightChartVM.ChipAbove(horizonY, s.Height, h);
                if (!Fits(taken, ev.name, MarkerFs, BoxLeft(x, ev.name, MarkerFs, w), top, w, h)) continue;
                DrawChip(dc, ev.name, MarkerFs, BoxLeft(x, ev.name, MarkerFs, w), top, moon, w, h);
            }
        }

        /// <summary>
        /// The scrub marker: one vertical the user moves hour by hour - by dragging
        /// across the chart, with the wheel, with the arrow keys or with the buttons in
        /// the panel header. It snaps to whole hours, carries a handle on the horizon so
        /// it looks like the control it is, puts a dot on each curve at that instant and
        /// prints where the sun and the moon actually are. That is the question the old
        /// fixed marker lines never answered.
        /// </summary>
        private void DrawScrub(DrawingContext dc, double w, double h, double sx, double sy, double spanMinutes) {
            if (scrubMinutes == null) return;
            double minutes = scrubMinutes.Value;
            if (minutes < 0 || minutes > spanMinutes) return;
            double x = NightChartVM.XAtMinutes(minutes, spanMinutes) * sx;
            var ink = palette.Scrub(scheme);
            var pen = new Pen(ink, 1.2) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) };
            dc.DrawLine(pen, new Point(x, 0), new Point(x, h));

            double horizonY = NightChartVM.YSun(0) * sy;
            dc.DrawEllipse(ink, new Pen(palette.ChipFill(scheme), 1.0), new Point(x, horizonY), 4.5, 4.5);

            var chart = row.Chart;
            var instant = chart.TimeAtMinutes(minutes);
            DrawScrubDot(dc, x, NightChartVM.YSun(chart.SunAltitudeAt(instant)) * sy, palette.SunTrack(scheme), h);
            double moonY = NightChartVM.YMoon(chart.MoonAltitudeAt(instant)) * sy;
            if (!double.IsNaN(moonY) && moonY >= 0 && moonY <= h) {
                // The chart's moon glyph, sitting on the track at the scrubbed hour: the
                // dot said "the moon is here", this also says what it looked like at that
                // hour. The disc behind it is what keeps the phase readable when a
                // moonrise label happens to sit under the marker. Limb is which side is
                // lit as THIS observer sees it, so southern hemispheres get a mirrored
                // glyph; absent data falls back to waxing.
                var moon = palette.MoonTrack(scheme);
                dc.DrawEllipse(palette.ChipFill(scheme), new Pen(moon, 0.8),
                    new Point(x, moonY), MoonGlyphRadius + 2.0, MoonGlyphRadius + 2.0);
                var (illum, litOnRight) = chart.ScrubMoonGlyph(minutes);
                DrawMoonGlyph(dc, x, moonY, MoonGlyphRadius, illum, litOnRight);
            }

            string readout = chart.ScrubReadout(minutes);
            var s = LabelSize(readout, NoteFs);
            double left = x + 6;
            if (left + s.Width > w) left = x - 6 - s.Width;
            double top = 3;
            // The legend owns the top-left corner, so a scrub line parked at the start
            // of the frame hangs its readout under the legend rather than over it.
            double legendRight = 32 + Math.Max(
                LabelSize("sun altitude", LegendFs).Width,
                LabelSize("moon altitude", LegendFs).Width);
            if (left < legendRight) top = LegendRow2 + LabelSize("moon altitude", LegendFs).Height / 2 + 4;
            // The degrees now stand on the left edge as well as the right, and the legend
            // sits below the top of that column, so the readout steps down past whatever
            // it would otherwise paint across rather than doubling up a number.
            foreach (var taken in axisChips) {
                var box = new Rect(left, top, s.Width, s.Height);
                if (taken.IntersectsWith(box)) top = Math.Max(top, taken.Bottom + 2);
            }
            DrawChip(dc, readout, NoteFs, left, top, ink, w, h);
        }

        /// <summary>Where the scrubbed instant meets a curve; skipped when that body is off the plotted window.</summary>
        private static void DrawScrubDot(DrawingContext dc, double x, double y, Brush brush, double h) {
            if (double.IsNaN(y) || y < 0 || y > h) return;
            dc.DrawEllipse(brush, null, new Point(x, y), 3, 3);
        }

        // ---- Scrub interaction -------------------------------------------------------------

        /// <summary>The frame the chart is drawing: one night, or as many as the zoom reaches.</summary>
        private double SpanMinutes => row?.Chart.FrameMinutes ?? NightChartVM.OneNightMinutes;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnMouseLeftButtonDown(e);
            if (row == null) return;
            Focus();
            CaptureMouse();
            ScrubToPixel(e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (IsMouseCaptured) ScrubToPixel(e.GetPosition(this).X);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            base.OnMouseLeftButtonUp(e);
            if (IsMouseCaptured) ReleaseMouseCapture();
        }

        /// <summary>Wheel over the chart: one hour at a time, the fastest way to walk a night.</summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e) {
            base.OnMouseWheel(e);
            if (row == null) return;
            StepScrub(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (row == null) return;
            switch (e.Key) {
                case Key.Left: StepScrub(-1); e.Handled = true; break;
                case Key.Right: StepScrub(1); e.Handled = true; break;
                case Key.Home: SetScrubDirect(0); e.Handled = true; break;
                case Key.End: SetScrubDirect(SpanMinutes); e.Handled = true; break;
                case Key.Escape:
                case Key.Delete: ClearScrub(); e.Handled = true; break;
            }
        }

        /// <summary>
        /// Scrub to the whole hour under the pointer. The pointer is deliberately not
        /// snapped to a sample of the curve: the hour is what a night plan is written in.
        /// </summary>
        private void ScrubToPixel(double pixelX) {
            double canvasX = pixelX / (ActualWidth / NightChartVM.Width);
            double minutes = NightChartVM.SnapToHour(NightChartVM.MinutesAtX(canvasX, SpanMinutes));
            SetScrubDirect(minutes);
        }

        private void SetScrubDirect(double minutes) {
            if (hookedPlanner != null) hookedPlanner.SetScrub(minutes);
            else SetScrub(System.Math.Clamp(minutes, 0, SpanMinutes));
        }

        private void StepScrub(int hours) {
            if (hookedPlanner != null) hookedPlanner.StepScrub(hours);
            else SetScrubDirect((scrubMinutes ?? (row.Payload.Sunset - row.Payload.FrameNoon).TotalMinutes) + hours * 60);
        }

        private void ClearScrub() {
            if (hookedPlanner != null) hookedPlanner.ClearScrub();
            else SetScrub(null);
        }

        /// <summary>
        /// Draw the moon as a phase glyph, not a dot: the lit part is bounded by
        /// the outer limb semicircle on the lit side and by the terminator, which
        /// is the half ellipse of half-width r*(1-2f) implied by illuminated
        /// fraction f - bulging toward the dark side when gibbous, toward the lit
        /// side when crescent.
        /// </summary>
        private void DrawMoonGlyph(DrawingContext dc, double cx, double cy, double r, double illum, bool litOnRight) {
            var brush = palette.MoonTrack(scheme);
            var outline = new Pen(brush, 1.0);
            if (illum <= 0.01) {
                // New moon: nothing is lit, so show the unlit outline only.
                dc.DrawEllipse(null, outline, new Point(cx, cy), r, r);
                return;
            }
            dc.DrawGeometry(brush, outline, MoonPhaseGeometry(cx, cy, r, illum, litOnRight));
        }

        /// <summary>
        /// The lit part of a phase glyph as a geometry. Public so the shape
        /// logic is unit-testable without a rendering pass.
        /// </summary>
        public static Geometry MoonPhaseGeometry(double cx, double cy, double r, double illum, bool litOnRight) {
            double f = Math.Clamp(illum, 0.0, 1.0);
            if (f >= 0.999) return new EllipseGeometry(new Point(cx, cy), r, r);
            double halfWidth = r * Math.Abs(1 - 2 * f);
            var top = new Point(cx, cy - r);
            var bottom = new Point(cx, cy + r);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open()) {
                ctx.BeginFigure(top, true, true);
                ctx.ArcTo(bottom, new Size(r, r), 0, false, Sweep(litOnRight), true, false);
                bool gibbous = f > 0.5;
                ctx.ArcTo(top, new Size(halfWidth, r), 0, false, Sweep(gibbous ? litOnRight : !litOnRight), true, false);
            }
            geo.Freeze();
            return geo;
        }

        /// <summary>Clockwise in WPF's y-down space, which is what the screen shows.</summary>
        private static SweepDirection Sweep(bool clockwise) =>
            clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

        /// <summary>Name the two curves; a bare arc is otherwise ambiguous.</summary>
        private void DrawLegend(DrawingContext dc) {
            DrawKey(dc, LegendRow1, palette.SunTrack(scheme), "sun altitude");
            DrawKey(dc, LegendRow2, palette.MoonTrack(scheme), "moon altitude");
        }

        private void DrawKey(DrawingContext dc, double y, Brush brush, string text) {
            dc.DrawLine(new Pen(brush, 1.8), new Point(8, y), new Point(28, y));
            DrawChip(dc, text, LegendFs, 32, y - LabelSize(text, LegendFs).Height / 2,
                brush, ActualWidth, ActualHeight);
        }

        /// <summary>Box size a label will occupy, for centring and right-aligning it.</summary>
        private Size LabelSize(string text, double size) {
            var t = DrawText(text, size, Brushes.Black);
            return new Size(t.Width + 2 * ChipPadX, t.Height + 2 * ChipPadY);
        }

        /// <summary>
        /// Draw a label as a chip: the text on a tight, near-black box edged with a
        /// hairline in the label's own colour. Without the backing, labels vanish over
        /// the bright daylight band and over the twilight overlays. x,y is the box
        /// top-left; it is clamped so a chip can never hang off the chart.
        /// </summary>
        private void DrawChip(DrawingContext dc, string text, double size, double x, double y, Brush ink, double w, double h) {
            var t = DrawText(text, size, ink);
            x = Math.Clamp(x, 0, Math.Max(0, w - t.Width - 2 * ChipPadX));
            y = Math.Clamp(y, 0, Math.Max(0, h - t.Height - 2 * ChipPadY));
            var box = new Rect(x, y, t.Width + 2 * ChipPadX, t.Height + 2 * ChipPadY);
            dc.DrawRoundedRectangle(palette.ChipFill(scheme), new Pen(ink, 0.6), box, 2.5, 2.5);
            dc.DrawText(t, new Point(x + ChipPadX, y + ChipPadY));
        }

        private FormattedText DrawText(string text, double size, Brush brush) =>
            new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        public void RequestRerender() => InvalidateVisual();
    }
}
