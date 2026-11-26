using NUnit.Framework;
using System.Windows.Media;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// Chart geometry sanity for a real Prague night: the sun curve, the moon
    /// track and the twilight bands must all have drawable content inside the
    /// noon-to-noon+2 frame.
    /// </summary>
    [NonParallelizable]
    public class PlannerChartTests {
        private static NightPayload PragueNight(int year, int month, int day) {
            var calc = new NightCalculator(new Site(50.08804, 14.42076));
            var tz = TimeZoneInfo.Local;
            // Night convention: the frame is anchored at local noon of the day
            // the night starts on, so this is the night of (day) evening -> (day+1) morning.
            var noonUtc = TimeZoneInfo.ConvertTimeToUtc(
                new DateTime(year, month, day, 12, 0, 0), tz);
            return calc.Compute(new DateTimeOffset(noonUtc, TimeSpan.Zero));
        }

        /// <summary>
        /// The points a path would draw, in order, run separators (" | ") ignored. A
        /// curve that leaves the altitude window is several runs, so counting tokens is
        /// not enough to describe what the chart shows.
        /// </summary>
        private static (double X, double Y)[] Points(string path) {
            if (string.IsNullOrEmpty(path)) return Array.Empty<(double, double)>();
            return path.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Split(','))
                .Select(xy => (
                    X: double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                    Y: double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)))
                .ToArray();
        }

        [Test]
        public void PragueNightHasSunAndMoonContentInsideTheFrame() {
            var p = PragueNight(2026, 11, 24);
            var row = new NightRowVM(p);

            var inWindow = p.MoonSamples.Count(m => m.Time >= p.Sunset && m.Time <= p.Sunrise);
            Console.WriteLine($"frame noon {p.FrameNoon} | sunset {p.Sunset} | sunrise {p.Sunrise}");
            Console.WriteLine($"sun samples {p.SunSeries.Count} | moon samples {p.MoonSamples.Count} | in night window {inWindow}");
            Console.WriteLine($"sun polyline pts {Points(row.Chart.SunPolyline).Length} | moon polyline pts {Points(row.Chart.MoonPolyline).Length}");
            Console.WriteLine($"moon peak alt {p.MoonSamples.Where(m => m.Time >= p.Sunset && m.Time <= p.Sunrise).Select(m => m.Altitude).DefaultIfEmpty(double.NaN).Max():F2} illum {p.MidnightIllumFraction:F3}");
            Console.WriteLine($"sun alt range {p.SunSeries.Min(s => s.Altitude):F2} .. {p.SunSeries.Max(s => s.Altitude):F2}");
            Console.WriteLine($"moonrise {string.Join(", ", p.Moon.Rise)} moonset {string.Join(", ", p.Moon.Set)}");

            Assert.That(row.Chart.SunPolyline, Is.Not.Empty);
            Assert.That(inWindow, Is.GreaterThan(1), "moon track has no samples inside the night window");
            Assert.That(row.Chart.MoonPolyline, Is.Not.Empty, "moon polyline is empty: the chart cannot draw a moon track");
        }

        [Test]
        public void SunCurveStaysInsideTheFrameHorizontally() {
            var p = PragueNight(2026, 11, 24);
            var xs = p.SunSeries.Select(p2 => NightChartVM.XAt(p, p2.Time)).ToArray();
            Assert.That(xs.Min(), Is.GreaterThanOrEqualTo(0).And.LessThan(NightChartVM.Width * 0.05));
            Assert.That(xs.Max(), Is.LessThanOrEqualTo(NightChartVM.Width).And.GreaterThan(NightChartVM.Width * 0.95));
        }

        [Test]
        public void MoonTrackSpansTheWholeFrameLikeTheSun() {
            var p = PragueNight(2026, 11, 24);
            var row = new NightRowVM(p);

            // The track is no longer clipped to sunset..sunrise: it runs the full
            // frame, so the drawn points must reach both edges of the canvas.
            var drawn = Points(row.Chart.MoonPolyline).Select(q => q.X).ToArray();

            Assert.Multiple(() => {
                Assert.That(drawn, Has.Length.EqualTo(p.MoonSamples.Count), "moon samples are being dropped");
                Assert.That(drawn.Min(), Is.LessThan(NightChartVM.Width * 0.05), "moon track does not start at the frame start");
                Assert.That(drawn.Max(), Is.GreaterThan(NightChartVM.Width * 0.95), "moon track stops before the frame end");
            });
        }

        [Test]
        public void TwilightShadingIsOneRunPerBandStretchNotOnePerSample() {
            var p = PragueNight(2026, 11, 24);
            var runs = NightChartVM.ShadingRuns(p);

            // One hairline-width rectangle per 10-minute sample reads as a comb of
            // vertical lines, so consecutive samples of the same band must merge.
            Assert.Multiple(() => {
                Assert.That(runs.Count, Is.LessThan(p.SunSeries.Count / 4), "shading is still sliced per sample");
                Assert.That(runs[0].X0, Is.EqualTo(0).Within(0.01));
                Assert.That(runs[runs.Count - 1].X1, Is.EqualTo(NightChartVM.Width).Within(0.6));
                Assert.That(runs.Select(r => r.Band).Distinct(),
                    Is.EquivalentTo(new[] { -1, 0, 1, 2, 3 }), "a Prague night must pass through every band");
            });
            for (int i = 1; i < runs.Count; i++) {
                Assert.That(runs[i].X0, Is.EqualTo(runs[i - 1].X1).Within(0.01), $"run {i} leaves a gap");
                Assert.That(runs[i].Band, Is.Not.EqualTo(runs[i - 1].Band), $"run {i} should have merged with {i - 1}");
                Assert.That(runs[i].X1, Is.GreaterThan(runs[i].X0), $"run {i} is empty");
            }
        }

        [Test]
        public void TwilightBandBoundariesMatchTheStandardDepths() {
            Assert.Multiple(() => {
                Assert.That(PlannerPalette.TwilightBand(5), Is.EqualTo(0), "sun up");
                Assert.That(PlannerPalette.TwilightBand(0), Is.EqualTo(1), "sun on the horizon is civil twilight");
                Assert.That(PlannerPalette.TwilightBand(-6), Is.EqualTo(2), "nautical");
                Assert.That(PlannerPalette.TwilightBand(-12), Is.EqualTo(3), "astronomical");
                Assert.That(PlannerPalette.TwilightBand(-18), Is.EqualTo(-1), "at -18 the sky is fully dark");
                Assert.That(PlannerPalette.TwilightBand(-40), Is.EqualTo(-1));
            });
        }

        [Test]
        public void AltitudeWindowReachesTheZenithAndStopsJustBelowAstronomicalTwilight() {
            double gap = NightChartVM.YSun(-6) - NightChartVM.YSun(0);
            Assert.Multiple(() => {
                Assert.That(NightChartVM.YSun(NightChartVM.TopAltitude), Is.EqualTo(0), "top edge of the window");
                Assert.That(NightChartVM.YSun(NightChartVM.BottomAltitude), Is.EqualTo(NightChartVM.Height), "bottom edge");
                // The +90 half is a user requirement: a window that stopped lower cut
                // high moons off. The floor used to be -90 and the whole lower half of
                // the panel went to empty sky; -24 keeps the -6/-12/-18 twilight lines
                // and their chips on the canvas with a little room to spare.
                Assert.That(NightChartVM.TopAltitude, Is.EqualTo(90), "the zenith must be on the chart");
                Assert.That(NightChartVM.BottomAltitude, Is.EqualTo(-24));
                Assert.That(NightChartVM.YSun(-18), Is.LessThan(NightChartVM.YSun(NightChartVM.BottomAltitude)),
                    "astronomical twilight must not sit on the bottom edge");
                Assert.That(NightChartVM.YSun(0), Is.LessThan(NightChartVM.Height), "the horizon must be on the canvas");
                Assert.That(NightChartVM.YSun(0), Is.GreaterThan(NightChartVM.Height / 2),
                    "the window is deliberately asymmetric: more sky above the horizon than below it");
                Assert.That(gap, Is.GreaterThan(0), "twilight lines must be in order top to bottom");
            });
        }

        [Test]
        public void TheRulesStandEveryFifteenDegreesAcrossTheWholeWindow() {
            var ladder = NightChartVM.GridAltitudes;
            Assert.Multiple(() => {
                Assert.That(ladder[0], Is.EqualTo(NightChartVM.TopAltitude), "the zenith carries a rule");
                Assert.That(ladder[ladder.Count - 1], Is.GreaterThan(NightChartVM.BottomAltitude),
                    "the bottom edge is where a track gets cut, not an altitude anyone reads");
                Assert.That(ladder.All(a => a >= NightChartVM.BottomAltitude && a <= NightChartVM.TopAltitude),
                    Is.True, "no rule outside the window");
                for (int i = 1; i < ladder.Count; i++) {
                    Assert.That(ladder[i - 1] - ladder[i], Is.EqualTo(15), $"step {i} of the ladder");
                }
                Assert.That(ladder, Has.Member(0.0), "the horizon is on the ladder");
                // Above the horizon there used to be no rules at all, so a height up
                // there could only be guessed at.
                Assert.That(ladder.Count(a => a > 0), Is.EqualTo(6), "-15 .. +90 every 15 degrees");
            });
        }

        [Test]
        public void EveryThirtyDegreesAndEveryTwilightDepthCarriesADegreeOnTheEdges() {
            var labels = NightChartVM.LabelAltitudes;
            Assert.Multiple(() => {
                Assert.That(labels, Has.Member(90.0),
                    "the zenith is labelled too, so a high moon has a number to be read against");
                foreach (double alt in new[] { 90.0, 60.0, 30.0, 0.0 }) {
                    Assert.That(labels, Has.Member(alt), $"{alt} degrees is on the 30 degree ladder");
                }
                foreach (double depth in NightChartVM.TwilightDepths) {
                    Assert.That(labels, Has.Member(depth), "the twilight depths keep their own chip");
                }
                Assert.That(labels.All(a => a >= NightChartVM.BottomAltitude && a <= NightChartVM.TopAltitude),
                    Is.True, "a label off the canvas is not a label");
                Assert.That(labels, Is.Ordered.Descending, "top of the canvas first");
                // Coarser than the rules deliberately: a box every 15 degrees is a wall,
                // not a scale.
                Assert.That(labels.Intersect(NightChartVM.GridAltitudes).Count(), Is.EqualTo(4),
                    "only every other rule carries a number");
            });
        }

        [Test]
        public void DegreeChipsGetTheirOwnRoomAndAreDroppedRatherThanPiled() {
            const double labelHeight = 16;
            var full = NightChartVM.AltitudeChips(NightChartVM.Height, labelHeight);
            Assert.Multiple(() => {
                Assert.That(full.Select(c => c.Altitude), Is.EquivalentTo(NightChartVM.LabelAltitudes),
                    "at the height the chart asks for, every degree finds room");
                Assert.That(full[0].Top, Is.EqualTo(0), "the zenith chip is not clipped away");
                for (int i = 1; i < full.Count; i++) {
                    Assert.That(full[i].Top, Is.GreaterThanOrEqualTo(full[i - 1].Top + labelHeight),
                        "two chips on one edge never share a pixel");
                }
            });

            // A squeezed panel keeps numbers and drops the ones with no room, rather than
            // stacking boxes into one unreadable block.
            var squeezed = NightChartVM.AltitudeChips(150, labelHeight);
            Assert.Multiple(() => {
                Assert.That(squeezed.Count, Is.LessThan(NightChartVM.LabelAltitudes.Count),
                    "a short canvas runs out of room, and that is allowed");
                Assert.That(squeezed.All(c => c.Top >= 0 && c.Top + labelHeight <= 150), Is.True,
                    "every chip it does draw is fully on the canvas");
                for (int i = 1; i < squeezed.Count; i++) {
                    Assert.That(squeezed[i].Top, Is.GreaterThanOrEqualTo(squeezed[i - 1].Top + labelHeight));
                }
            });
        }

        [Test]
        public void SamplesInsideTheAltitudeWindowSurviveAndOnlyOutsideOnesAreCut() {
            var p = PragueNight(2026, 11, 24);
            var chart = new NightChartVM(p);
            int sunInside = p.SunSeries.Count(
                s => s.Altitude >= NightChartVM.BottomAltitude && s.Altitude <= NightChartVM.TopAltitude);
            int moonInside = p.MoonSamples.Count(
                m => m.Altitude >= NightChartVM.BottomAltitude && m.Altitude <= NightChartVM.TopAltitude);

            Assert.Multiple(() => {
                // At a -24 degree floor the Prague sun really does sink out of the window
                // around midnight, so the curve breaking into runs is now normal; what
                // must never happen again is the runs being joined by a line hugging the
                // bottom edge.
                Assert.That(chart.SunPolyline, Does.Contain("|"), "the sun leaves the -24 window, so it must break");
                Assert.That(Points(chart.SunPolyline).Length, Is.GreaterThanOrEqualTo(sunInside), "samples inside the window were dropped");
                Assert.That(Points(chart.SunPolyline).Length, Is.LessThan(p.SunSeries.Count), "samples outside the window were kept");
                Assert.That(Points(chart.MoonPolyline).Length, Is.GreaterThanOrEqualTo(moonInside), "moon samples inside the window were dropped");
                Assert.That(Points(chart.MoonPolyline).Length, Is.LessThanOrEqualTo(p.MoonSamples.Count), "moon samples outside the window were kept");
            });
        }

        [Test]
        public void CurvesStopAtTheWindowEdgeInsteadOfRunningAlongIt() {
            var p = PragueNight(2026, 11, 24);
            // A deliberately tight window (only +-20 degrees visible) so the real moon
            // track certainly leaves it, to prove the curve is cut rather than clamped:
            // clamping every sample to the edge is what drew a flat horizontal line
            // along the bottom of the panel.
            var path = NightChartVM.TrimmedPolyline(
                p,
                p.MoonSamples.Select(m => (m.Time, m.Altitude)).ToArray(),
                alt => (20 - alt) / 40 * NightChartVM.Height);
            Assert.Multiple(() => {
                Assert.That(path, Does.Contain("|"), "the track leaves this window, so it must break into runs");
                Assert.That(Points(path).Select(q => q.Y), Is.All.InRange(0.0, NightChartVM.Height), "a point lies off the canvas");
            });
            foreach (var run in path.Split('|')) {
                var q = Points(run);
                // Only a run's endpoints may touch an edge: an interior point pinned to
                // the edge is the flattened line this replaced.
                for (int i = 1; i < q.Length - 1; i++)
                    Assert.That(q[i].Y, Is.GreaterThan(0.01).And.LessThan(NightChartVM.Height - 0.01),
                        $"point {i} of a run sits on the window edge: the curve is flattened along it");
                for (int i = 1; i < q.Length; i++)
                    Assert.That(q[i].X, Is.GreaterThan(q[i - 1].X), "a run doubles back in time");
            }
        }

        [Test]
        public void MarkerChipsFlipToTheOtherSideOfTheLineRatherThanLeavingTheChart() {
            const double horizon = 160, label = 18, canvas = 260;
            Assert.Multiple(() => {
                // Sunset and sunrise hang under the horizon when the panel has room.
                Assert.That(NightChartVM.ChipBelow(horizon, label, canvas), Is.EqualTo(horizon + 4));
                Assert.That(NightChartVM.ChipBelow(horizon, label, canvas) + label, Is.LessThanOrEqualTo(canvas));
                // Too short a panel flips them above the line instead of off the chart.
                Assert.That(NightChartVM.ChipBelow(horizon, label, 180), Is.EqualTo(horizon - 4 - label));
                // Moonrise/moonset chips are the mirror image, above the line.
                Assert.That(NightChartVM.ChipAbove(horizon, label, canvas), Is.EqualTo(horizon - 4 - label));
                Assert.That(NightChartVM.ChipAbove(20, label, canvas), Is.EqualTo(24));
            });
        }

        [Test]
        public void ScrubbingLandsOnWholeHoursAndReportsWhereTheBodiesAre() {
            var p = PragueNight(2026, 11, 24);
            var chart = new NightChartVM(p);
            double asked = (p.Sunset - p.FrameNoon).TotalMinutes + 37;
            double snapped = NightChartVM.SnapToHour(asked);
            var instant = chart.TimeAtMinutes(snapped);
            var text = chart.ScrubReadout(snapped);

            Assert.Multiple(() => {
                Assert.That(snapped % 60, Is.EqualTo(0), "the marker only stops on whole hours");
                Assert.That(NightChartVM.SnapToHour(150), Is.EqualTo(180), "half hours round away from zero");
                Assert.That(NightChartVM.MinutesAtX(NightChartVM.XAtMinutes(500)), Is.EqualTo(500).Within(1e-9), "pointer mapping is not an inverse");
                Assert.That(text, Does.StartWith(instant.LocalDateTime.ToString("HH:mm")), "readout does not name the hour it points at");
                Assert.That(text, Does.Contain("sun"));
                Assert.That(text, Does.Contain("moon"));
                // The phase moved here when the static peak label was removed: the scrub
                // line is now the only place the chart writes the moon down.
                Assert.That(text, Does.Match(@"\d+% moon"), "the readout carries the phase of the hour it points at");
                // Altitudes are interpolated at that instant, not snapped to a sample.
                Assert.That(chart.SunAltitudeAt(instant), Is.Not.NaN);
                Assert.That(chart.SunAltitudeAt(instant), Is.EqualTo(NightChartVM.AltitudeAt(
                    p.SunSeries.Select(s => (s.Time, s.Altitude)).ToArray(), instant)).Within(1e-9));
                Assert.That(chart.MoonAltitudeAt(p.FrameNoon.AddHours(-1)), Is.NaN, "outside the frame has no altitude");
            });
        }

        [Test]
        public void HorizonAndChipBackingAreDistinctFromTheFaintGrid() {
            var palette = new PlannerPalette();
            var horizon = ((SolidColorBrush)palette.Horizon(ChartScheme.Night)).Color;
            var grid = ((SolidColorBrush)palette.Gridline(ChartScheme.Night)).Color;
            var chip = ((SolidColorBrush)palette.ChipFill(ChartScheme.Default)).Color;

            Assert.Multiple(() => {
                Assert.That(horizon, Is.Not.EqualTo(grid), "the horizon must not read as just another gridline");
                Assert.That(chip.A, Is.GreaterThanOrEqualTo((byte)0xC0), "chip backing has to block what is behind it");
                Assert.That(chip.R + chip.G + chip.B, Is.LessThan(0x60), "chip backing has to read as black");
            });
        }

        [Test]
        public void TheHorizonIsGreenExceptWhereAPaletteSaysOtherwise() {
            var theme = ((SolidColorBrush)PlannerPalette.HorizonDefault(ChartScheme.Default)).Color;
            var night = ((SolidColorBrush)PlannerPalette.HorizonDefault(ChartScheme.Night)).Color;
            var enhanced = ((SolidColorBrush)PlannerPalette.HorizonDefault(ChartScheme.Enhanced)).Color;

            Assert.Multiple(() => {
                // The horizon used to be the theme's own red, which sat on top of the red
                // moon track and read as one more curve. So the theme draws it green, and
                // only the night scheme - which is red everywhere - keeps it red. Enhanced
                // draws it teal on navy: still the one line that is not a curve.
                Assert.That(theme.G, Is.GreaterThan(theme.R), "the horizon is not red in the theme palette");
                Assert.That(night.R, Is.GreaterThan(night.G), "the night scheme stays red throughout");
                Assert.That(enhanced.G, Is.GreaterThan(enhanced.R), "Enhanced keeps the horizon out of the reds");
                Assert.That(enhanced, Is.Not.EqualTo(theme), "and it is the palette's own colour, not the theme's");
                Assert.That(((SolidColorBrush)new PlannerPalette().Horizon(ChartScheme.Default)).Color, Is.EqualTo(theme),
                    "an unpicked role follows the scheme");
            });
        }

        /// <summary>
        /// Round 8, T5: three palettes now paint one chart, so the palette has to say
        /// something different for each of them - and the point of Enhanced in particular is
        /// that its three curves are separated by hue, not only by brightness, which is the
        /// one thing a red-green viewer cannot recover from brightness alone.
        /// </summary>
        [Test]
        public void EachPalettePaintsItsOwnChart() {
            var palette = new PlannerPalette();
            var canvas = new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }
                .Select(s => ((SolidColorBrush)palette.ChartBackground(s)).Color).ToArray();
            var sun = new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }
                .Select(s => ((SolidColorBrush)palette.SunTrack(s)).Color).ToArray();
            var moon = new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }
                .Select(s => ((SolidColorBrush)palette.MoonTrack(s)).Color).ToArray();
            var horizon = new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }
                .Select(s => ((SolidColorBrush)palette.Horizon(s)).Color).ToArray();

            Assert.Multiple(() => {
                Assert.That(canvas[2], Is.Not.EqualTo(canvas[0]), "Enhanced has its own canvas");
                Assert.That(sun[2], Is.Not.EqualTo(moon[2]), "Enhanced paints the sun and the moon in different colours");
                Assert.That(horizon[2], Is.Not.EqualTo(sun[2]), "and the horizon is a third colour again");
                // The theme and night palettes draw the sun in the ink colour, which is why
                // they read as one bright line among many; Enhanced does not.
                Assert.That(sun[2], Is.Not.EqualTo(((SolidColorBrush)palette.Ink(ChartScheme.Enhanced)).Color),
                    "the sun is not simply the ink colour under Enhanced");
                // The two palettes the plugin owns have to give the ink something to sit on.
                // The theme palette is not asserted on: it defers to N.I.N.A.'s theme
                // dictionaries, and a bare test process has none loaded, where every theme
                // lookup falls back to the same grey.
                foreach (var scheme in new[] { ChartScheme.Night, ChartScheme.Enhanced }) {
                    Assert.That(((SolidColorBrush)palette.ChartBackground(scheme)).Color,
                        Is.Not.EqualTo(((SolidColorBrush)palette.Ink(scheme)).Color),
                        $"{scheme} has a background the ink can be read against");
                }
            });
        }

        [Test]
        public void APickedChartColourWinsInEverySchemeAndNonsenseIsIgnored() {
            var palette = new PlannerPalette();
            palette.SetOverrides("#FFD700", null, "#246");

            Assert.Multiple(() => {
                foreach (var scheme in new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }) {
                    Assert.That(((SolidColorBrush)palette.SunTrack(scheme)).Color, Is.EqualTo(Color.FromRgb(0xFF, 0xD7, 0x00)),
                        $"a picked colour is not swapped over by the {scheme} palette");
                }

                Assert.That(((SolidColorBrush)palette.Horizon(ChartScheme.Default)).Color, Is.EqualTo(Color.FromRgb(0x22, 0x44, 0x66)),
                    "#246 is the short form of #224466");
                Assert.That(((SolidColorBrush)palette.MoonTrack(ChartScheme.Night)).Color, Is.Not.EqualTo(Color.FromRgb(0xFF, 0xD7, 0x00)),
                    "only the role that was picked changes");
                Assert.That(PlannerPalette.ParseHex(string.Empty), Is.Null, "empty means follow the scheme");
                Assert.That(PlannerPalette.ParseHex("not a colour"), Is.Null, "a value that is not a colour is ignored, not guessed");
                Assert.That(PlannerPalette.EffectiveSun("nonsense", ChartScheme.Enhanced),
                    Is.SameAs(PlannerPalette.EffectiveSun("", ChartScheme.Enhanced)), "and it falls back to what the scheme says");
            });
        }

        [Test]
        public void ZoomFramesRunFromOneNightToAWeek() {
            Assert.Multiple(() => {
                Assert.That(NightChartVM.FrameMinutesAtZoom(1), Is.EqualTo(NightChartVM.OneNightMinutes), "one night, like the desktop planner");
                Assert.That(NightChartVM.FrameMinutesAtZoom(7), Is.EqualTo(7 * NightChartVM.OneNightMinutes), "a week");
                Assert.That(NightChartVM.FrameMinutesAtZoom(0), Is.EqualTo(NightChartVM.OneNightMinutes), "cannot zoom below one night");
                Assert.That(NightChartVM.FrameMinutesAtZoom(99), Is.EqualTo(NightChartVM.MaxZoomNights * NightChartVM.OneNightMinutes), "cannot zoom past a week");
                Assert.That(new NightChartVM(PragueNight(2026, 11, 24), NightChartVM.FrameMinutesAtZoom(3)).ZoomNights, Is.EqualTo(3));
                Assert.That(new NightChartVM(PragueNight(2026, 11, 24)).ZoomNights, Is.EqualTo(2),
                    "a bare payload chart shows the whole 48 hours it carries; the panel always passes an explicit frame");
            });
        }

        [Test]
        public void HourGridGetsSparserAsTheFrameWidens() {
            Assert.Multiple(() => {
                // The clock chips are ~40 px wide; four-hour lines only stay readable
                // over a couple of nights, so the step grows with the frame.
                Assert.That(NightChartVM.HourGridMinutes(NightChartVM.OneNightMinutes), Is.EqualTo(240));
                Assert.That(NightChartVM.HourGridMinutes(2 * NightChartVM.OneNightMinutes), Is.EqualTo(240));
                Assert.That(NightChartVM.HourGridMinutes(3 * NightChartVM.OneNightMinutes), Is.EqualTo(480));
                Assert.That(NightChartVM.HourGridMinutes(4 * NightChartVM.OneNightMinutes), Is.EqualTo(480));
                Assert.That(NightChartVM.HourGridMinutes(7 * NightChartVM.OneNightMinutes), Is.EqualTo(720));
            });
        }

        [Test]
        public void StitchedFrameSpansEveryNightOnceAndInOrder() {
            var nights = new[] { PragueNight(2026, 11, 22), PragueNight(2026, 11, 23), PragueNight(2026, 11, 24) };
            var frame = NightFrame.For(nights, 3);

            var sunTimes = frame.SunSeries.Select(s => s.Time).ToArray();
            var moonTimes = frame.MoonSamples.Select(m => m.Time).ToArray();
            Assert.Multiple(() => {
                Assert.That(frame.FrameNoon, Is.EqualTo(nights[0].FrameNoon), "the frame starts at the selected night's noon");
                Assert.That(sunTimes, Is.Unique, "the day seams repeated a sample");
                Assert.That(sunTimes.SequenceEqual(sunTimes.OrderBy(t => t)), Is.True, "stitching scrambled the time order");
                Assert.That(moonTimes, Is.Unique, "the day seams repeated a moon sample");
                Assert.That(moonTimes.SequenceEqual(moonTimes.OrderBy(t => t)), Is.True, "stitching scrambled the moon order");
                Assert.That(sunTimes[0], Is.EqualTo(nights[0].FrameNoon));
                Assert.That(sunTimes[^1], Is.GreaterThanOrEqualTo(nights[0].FrameNoon.AddMinutes(3 * NightChartVM.OneNightMinutes - 10)),
                    "the curve must reach the right edge of a 3-night frame");
                Assert.That(sunTimes[^1], Is.LessThan(nights[0].FrameNoon.AddMinutes(3 * NightChartVM.OneNightMinutes + 10)),
                    "the frame must not run past its own end");
                // Sample density is unchanged by stitching: the 10-minute grid simply
                // continues across the seams, so a 3-night frame holds 3 days of it.
                Assert.That(sunTimes.Length,
                    Is.EqualTo(NightChartVM.FrameMinutesAtZoom(3) / 10 + 1).Within(3),
                    "a day slice dropped or duplicated samples");
                // Twilight events of all three nights are present, not just the first night's.
                Assert.That(frame.Events.Count, Is.GreaterThan(nights[0].Events.Count));
                Assert.That(frame.Events.Select(e => e.Time), Is.All.InRange(nights[0].FrameNoon, nights[0].FrameNoon.AddMinutes(3 * NightChartVM.OneNightMinutes)));
                Assert.That(frame.Moon.Rise.Concat(frame.Moon.Set), Is.Unique, "a moon crossing counted twice at a seam");
                // Summary fields describe the first night, which is the one the row names.
                Assert.That(frame.Sunset, Is.EqualTo(nights[0].Sunset));
                Assert.That(frame.NightStart, Is.EqualTo(nights[0].NightStart));
            });
        }

        [Test]
        public void OneNightNeedsNoStitchingAtAll() {
            var p = PragueNight(2026, 11, 24);
            Assert.That(NightFrame.For(new[] { p }, 1), Is.SameAs(p), "zoom 1 must not copy or resample anything");
        }

        [Test]
        public void EveryNightInAStitchedFrameHasItsOwnSunsetAndSunrise() {
            var nights = new[] { PragueNight(2026, 11, 23), PragueNight(2026, 11, 24) };
            var one = new NightChartVM(nights[1], NightChartVM.OneNightMinutes);
            var frame = new NightChartVM(NightFrame.For(nights, 2), NightChartVM.FrameMinutesAtZoom(2));

            var crossings = frame.SunCrossings();
            Assert.Multiple(() => {
                // Two nights in, the chart labels both evenings and both mornings. They
                // are read off the plotted curve, so a zoomed frame cannot show fewer
                // crossings than it draws.
                Assert.That(crossings, Has.Count.EqualTo(4), "a 2-night frame must cross the horizon four times");
                Assert.That(crossings.Count(c => !c.Rising), Is.EqualTo(2), "two sunsets");
                Assert.That(crossings.Count(c => c.Rising), Is.EqualTo(2), "two sunrises");
                Assert.That(crossings.Select(c => c.Time).SequenceEqual(crossings.Select(c => c.Time).OrderBy(t => t)),
                    Is.True, "crossings out of time order");
                // The interpolated crossing sits on the real event, well inside a pixel.
                var second = crossings[1];  // the frame's first sunrise, night 2's morning
                Assert.That(Math.Abs((second.Time - nights[0].Sunrise).TotalMinutes), Is.LessThan(5),
                    "the interpolated crossing drifted from the bisected event");
                Assert.That(one.SunCrossings(), Has.Count.EqualTo(2), "one night is one sunset and one sunrise");
                Assert.That(Math.Abs((one.SunCrossings()[0].Time - nights[1].Sunset).TotalMinutes), Is.LessThan(5));
            });
        }

        [Test]
        public void NothingLeaksPastTheFrameEdge() {
            var p = PragueNight(2026, 11, 24);
            // A one-night frame still receives a payload holding 48 hours, so the
            // frame - not the payload - has to decide what is drawn.
            var chart = new NightChartVM(p, NightChartVM.OneNightMinutes);
            Assert.Multiple(() => {
                foreach (var curve in new[] { chart.SunPolyline, chart.MoonPolyline }) {
                    Assert.That(curve, Is.Not.Empty, "an empty curve at one-night zoom");
                    foreach (var point in curve.Split(" | ").SelectMany(run => run.Split(' '))) {
                        double x = double.Parse(point.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);
                        Assert.That(x, Is.InRange(0, NightChartVM.Width), $"curve point at x={x:F1} sits outside the canvas");
                    }
                }
                var runs = NightChartVM.ShadingRuns(p, NightChartVM.OneNightMinutes);
                Assert.That(runs, Is.Not.Empty);
                foreach (var run in runs) {
                    Assert.That(run.X0, Is.GreaterThanOrEqualTo(0), "shading started left of the canvas");
                    Assert.That(run.X1, Is.LessThanOrEqualTo(NightChartVM.Width), "shading ended right of the canvas");
                }
                Assert.That(chart.SunCrossings().Select(c => chart.XAt(c.Time)),
                    Is.All.InRange(0, NightChartVM.Width), "a chip was labelled for an off-screen crossing");
            });
        }

        [Test]
        public void ScrubMarkerCarriesThePhaseOfTheMoonItPointsAt() {
            var p = PragueNight(2026, 11, 24);
            var chart = new NightChartVM(p);
            var peak = chart.NightMoonPeak();

            var first = chart.ScrubMoonGlyph(0);
            var atPeak = chart.ScrubMoonGlyph((peak.Time - p.FrameNoon).TotalMinutes);
            var sample = p.MoonSamples.MinBy(m => Math.Abs((m.Time - peak.Time).TotalMinutes));
            Assert.Multiple(() => {
                Assert.That(first.Illum, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1), "illumination is a fraction");
                Assert.That(atPeak.Illum, Is.EqualTo(peak.IllumFraction).Within(0.02),
                    "the scrub glyph disagrees with the peak glyph at the same instant");
                Assert.That(atPeak.Illum, Is.EqualTo(sample.IllumFraction).Within(0.02));
                Assert.That(atPeak.LitOnRight, Is.Not.EqualTo(string.Equals(p.Limb, "left", StringComparison.OrdinalIgnoreCase)),
                    "the glyph must mirror for the observer's limb, as the peak glyph does");
                Assert.That(chart.ScrubMoonGlyph(-600).Illum, Is.NaN, "outside the frame has no phase");
            });
        }

        [Test]
        public void MoonGlyphSitsOnTheHighestMoonPointOfThatNight() {
            var p = PragueNight(2026, 11, 24);
            var peak = new NightChartVM(p).NightMoonPeak();
            var bestTonight = p.MoonSamples
                .Where(m => m.Time >= p.Sunset && m.Time <= p.Sunrise)
                .Max(m => m.Altitude);

            Assert.Multiple(() => {
                Assert.That(peak.Time, Is.InRange(p.Sunset, p.Sunrise), "glyph placed outside the night");
                Assert.That(peak.Altitude, Is.EqualTo(bestTonight).Within(1e-9));
            });
        }

        [Test]
        public void MoonGlyphShapeMatchesTheIlluminatedFraction() {
            // r = 10 disc centred on the origin, lit on the right (waxing, northern view).
            Geometry Waxing(double f) => NightChartControl.MoonPhaseGeometry(0, 0, 10, f, true);
            Geometry Waning(double f) => NightChartControl.MoonPhaseGeometry(0, 0, 10, f, false);

            Assert.Multiple(() => {
                Assert.That(Waxing(0.999).FillContains(new System.Windows.Point(9, 0)), Is.True, "full moon covers the disc");
                Assert.That(Waxing(0.999).FillContains(new System.Windows.Point(-9, 0)), Is.True);

                // Crescent: only a sliver by the right limb is lit.
                Assert.That(Waxing(0.25).FillContains(new System.Windows.Point(8, 0)), Is.True);
                Assert.That(Waxing(0.25).FillContains(new System.Windows.Point(2, 0)), Is.False, "terminator cuts the crescent at x=+5");
                Assert.That(Waxing(0.25).FillContains(new System.Windows.Point(-8, 0)), Is.False);

                // Gibbous: the terminator bulges into the dark side.
                Assert.That(Waxing(0.75).FillContains(new System.Windows.Point(-3, 0)), Is.True);
                Assert.That(Waxing(0.75).FillContains(new System.Windows.Point(-8, 0)), Is.False);

                // Quarter: straight terminator through the centre.
                Assert.That(Waxing(0.5).FillContains(new System.Windows.Point(1, 0)), Is.True);
                Assert.That(Waxing(0.5).FillContains(new System.Windows.Point(-1, 0)), Is.False);

                // Waning mirrors the lit side.
                Assert.That(Waning(0.25).FillContains(new System.Windows.Point(-8, 0)), Is.True);
                Assert.That(Waning(0.25).FillContains(new System.Windows.Point(8, 0)), Is.False);
            });
        }

        [Test]
        public void TwilightPaletteFollowsTheStandardAltitudeBands() {
            var palette = new PlannerPalette();
            foreach (var scheme in new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }) {
                Assert.Multiple(() => {
                    Assert.That(palette.Twilight(scheme, 12), Is.Not.Null, "daylight");
                    Assert.That(palette.Twilight(scheme, 0), Is.Not.Null, "civil just below the horizon");
                    Assert.That(palette.Twilight(scheme, -6), Is.Not.Null, "nautical");
                    Assert.That(palette.Twilight(scheme, -12), Is.Not.Null, "astronomical");
                    Assert.That(palette.Twilight(scheme, -18), Is.Null, "below -18 is full night, no overlay");
                    Assert.That(palette.Twilight(scheme, -60), Is.Null);
                });
                // Each band must be a distinct colour, otherwise the transition is invisible.
                var brushes = new[] { 12.0, -3.0, -9.0, -15.0 }
                    .Select(a => ((System.Windows.Media.SolidColorBrush)palette.Twilight(scheme, a)).Color)
                    .ToArray();
                Assert.That(brushes.Distinct().Count(), Is.EqualTo(4), $"{scheme}: twilight bands are not distinguishable");
            }

            // Enhanced paints its bands as solid navy steps rather than a translucent
            // overlay over the canvas, so they have to darken as the sun goes down.
            var steps = new[] { 12.0, -3.0, -9.0, -15.0 }
                .Select(a => ((SolidColorBrush)palette.Twilight(ChartScheme.Enhanced, a)).Color)
                .ToArray();
            var brightest = steps.Select(c => (double)c.R + c.G + c.B).ToArray();
            Assert.That(brightest.SequenceEqual(brightest.OrderByDescending(v => v)), Is.True,
                "the Enhanced bands get darker as the sky gets darker");
        }
    }
}
