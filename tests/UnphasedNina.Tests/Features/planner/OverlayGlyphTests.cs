// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Astrometry;
using NUnit.Framework;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class OverlayGlyphTests {
        private static readonly NOVAS.Body[] Bodies = {
            NOVAS.Body.Mercury, NOVAS.Body.Venus, NOVAS.Body.Mars, NOVAS.Body.Jupiter,
            NOVAS.Body.Saturn, NOVAS.Body.Uranus, NOVAS.Body.Neptune, NOVAS.Body.Moon
        };
        private static readonly (string Code, string Label)[] TargetGlyphs = {
            ("GALXY", "Galaxy"), ("GALCL", "Galaxy group"), ("BRTNB", "Bright nebula"), ("DRKNB", "Dark nebula"),
            ("PLNNB", "Planetary nebula"), ("SNREM", "Supernova remnant"), ("OPNCL", "Open cluster"),
            ("GLOCL", "Globular cluster"), ("CL+NB", "Cluster + nebula"), ("1STAR", "Star"),
            ("2STAR", "Double star"), (null, "Manual target")
        };

        [Test]
        public void ScrubInterpolatesPhaseAcrossTheAngleWrapAndPlacesLabelsAroundOccupiedSpace() {
            var time = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
            var phases = new Dictionary<DateTimeOffset, BodyIllumination> {
                [time] = new(.3, 170),
                [time.AddHours(1)] = new(.6, -170)
            };
            var track = new OverlayTrack(null, new[] { (time, 0.0) }, string.Empty, phases);
            var midway = track.PhaseAt(time.AddMinutes(30)).Value;
            Assert.That(midway.Fraction, Is.EqualTo(.45).Within(.001));
            Assert.That(midway.BrightSideDegrees, Is.EqualTo(180));
            Assert.That(track.PhaseAt(time.AddMinutes(62)), Is.EqualTo(phases[time.AddHours(1)]));

            var first = NightChartControl.PlaceScrubName(Array.Empty<Rect>(), 100, 50, 200, 100, 35, 16, 17);
            var second = NightChartControl.PlaceScrubName(new[] { first }, 100, 50, 200, 100, 35, 16, 17);
            var atRight = NightChartControl.PlaceScrubName(Array.Empty<Rect>(), 190, 50, 200, 100, 35, 16, 17);
            Assert.That(first.X, Is.GreaterThan(100));
            Assert.That(second.Right, Is.LessThan(100));
            Assert.That(atRight.Right, Is.LessThan(190));
        }

        [Test]
        public void CatalogGlyphsHaveDistinctSilhouettesAtScrubSize() {
            static string Render(string code) {
                const int size = 24;
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                    DeepSkyGlyph.Draw(dc, code, new Point(12, 12), DeepSkyGlyph.Radius, Brushes.White, Brushes.Black);
                var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var pixels = new byte[size * size * 4];
                bitmap.CopyPixels(pixels, size * 4, 0);
                Assert.That(pixels.Where((_, index) => index % 4 == 0).Max(), Is.GreaterThan(180), code);
                return Convert.ToBase64String(pixels);
            }
            Assert.That(TargetGlyphs.Select(target => Render(target.Code)).Distinct().Count(), Is.EqualTo(TargetGlyphs.Length));
            Assert.That(Render(" smcdn "), Is.EqualTo(Render("BRTNB")));
            Assert.That(Render("NONEX"), Is.EqualTo(Render(null)));
        }

        [Test]
        public void TheThreeGlyphModesHaveDistinctSizesAndTheCurrentMoonIsTheDefault() {
            var planner = PlannerFixture.Planner();
            Assert.That(planner.GlyphStyle, Is.EqualTo(PlanetGlyphStyle.Phase));
            Assert.That(planner.GlyphStyles, Is.EqualTo(new[] { PlanetGlyphStyle.Basic, PlanetGlyphStyle.Phase, PlanetGlyphStyle.Detailed }));
            foreach (var body in Bodies) {
                var basic = NightChartControl.GlyphRadius(PlanetGlyphStyle.Basic, body);
                var phase = NightChartControl.GlyphRadius(PlanetGlyphStyle.Phase, body);
                var detail = NightChartControl.GlyphRadius(PlanetGlyphStyle.Detailed, body);
                Assert.Multiple(() => {
                    Assert.That(basic, Is.LessThan(phase), body.ToString());
                    Assert.That(phase, Is.EqualTo(9), body.ToString());
                    Assert.That(detail, Is.GreaterThan(phase), body.ToString());
                });
            }
        }

        [Test]
        public void APhaseMaskFollowsTheBrightSideAroundTheDisc() {
            var centre = new Point(16, 16);
            var right = NightChartControl.IlluminatedDisc(centre, 10, new BodyIllumination(.5, 0));
            var upper = NightChartControl.IlluminatedDisc(centre, 10, new BodyIllumination(.5, -90));
            Assert.Multiple(() => {
                Assert.That(right.FillContains(new Point(21, 16)), Is.True);
                Assert.That(right.FillContains(new Point(11, 16)), Is.False);
                Assert.That(upper.FillContains(new Point(16, 11)), Is.True);
                Assert.That(upper.FillContains(new Point(16, 21)), Is.False);
                Assert.That(NightChartControl.IlluminatedDisc(centre, 10, new BodyIllumination(0, 60)), Is.Null);
            });
        }

        [Test]
        public void InnerPlanetIlluminationVariesAndDetailedArtworkUsesTheNightPalette() {
            var site = new Site(50.08, 14.42);
            var time = new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero);
            foreach (var body in new[] { NOVAS.Body.Mercury, NOVAS.Body.Venus }) {
                var first = OverlayEphemeris.Phase(body, site, time).Value;
                var later = OverlayEphemeris.Phase(body, site, time.AddDays(18)).Value;
                Assert.Multiple(() => {
                    Assert.That(first.Fraction, Is.InRange(0, 1));
                    Assert.That(later.Fraction, Is.InRange(0, 1));
                    Assert.That(Math.Abs(first.Fraction - later.Fraction), Is.GreaterThan(.02));
                    Assert.That(first.BrightSideDegrees, Is.InRange(-180, 180));
                });
            }
            Assert.That(OverlayEphemeris.Phase(NOVAS.Body.Jupiter, site, time), Is.Null);
            var day = GlyphPixels(ChartScheme.Enhanced);
            var night = GlyphPixels(ChartScheme.Night);
            Assert.That(day.Count(pixel => pixel.G > 45 && pixel.B > 35), Is.GreaterThan(20));
            Assert.That(night.Count(pixel => pixel.R > 60 && pixel.G <= 25 && pixel.B <= 25), Is.GreaterThan(20));
            Assert.That(night.Count(pixel => pixel.G > .65 * pixel.R || pixel.B > .65 * pixel.R), Is.Zero);
        }

        [Test, Explicit("Writes the glyph styles to a PNG for visual review.")]
        public void RenderTheGlyphStyles() {
            const int width = 600, height = 310;
            var visual = new DrawingVisual();
            var chart = new NightChartControl();
            using (var dc = visual.RenderOpen()) {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(19, 31, 49)), null, new Rect(0, 0, width, height));
                for (int i = 0; i < Bodies.Length; i++) Write(dc, Bodies[i].ToString(), new Point(97 + 60 * i, 12));
                var styles = new[] { PlanetGlyphStyle.Basic, PlanetGlyphStyle.Phase, PlanetGlyphStyle.Detailed, PlanetGlyphStyle.Detailed };
                for (int row = 0; row < styles.Length; row++) {
                    var style = styles[row];
                    chart.SetScheme(row == 3 ? ChartScheme.Night : ChartScheme.Enhanced);
                    Write(dc, row == 3 ? "Night vision" : style.ToString(), new Point(12, 45 + 35 * row));
                    for (int i = 0; i < Bodies.Length; i++) {
                        var body = Bodies[i];
                        var phase = body is NOVAS.Body.Venus or NOVAS.Body.Mercury or NOVAS.Body.Moon
                            ? new BodyIllumination(body == NOVAS.Body.Venus ? .33 : .67, body == NOVAS.Body.Mercury ? -48 : 0)
                            : (BodyIllumination?)null;
                        var ink = OverlayStyle.BrushFor(i, row == 3 ? ChartScheme.Night : ChartScheme.Enhanced);
                        chart.DrawBodyGlyph(dc, body, new Point(118 + 60 * i, 52 + 35 * row),
                            NightChartControl.GlyphRadius(style, body), ink, style, phase);
                    }
                }
                for (int i = 0; i < TargetGlyphs.Length; i++) {
                    var (code, label) = TargetGlyphs[i];
                    var x = 50 + 100 * (i % 6);
                    var y = 210 + 55 * (i / 6);
                    DeepSkyGlyph.Draw(dc, code, new Point(x, y), DeepSkyGlyph.Radius, Brushes.White, Brushes.Transparent);
                    var caption = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 11, Brushes.White, 1) { TextAlignment = TextAlignment.Center };
                    dc.DrawText(caption, new Point(x, y + 15));
                }
            }
            var preview = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            preview.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(preview));
            using var file = File.Create(Path.Combine(TestEnvironment.Root, "overlay-glyphs.png"));
            encoder.Save(file);
            TestContext.Progress.WriteLine(file.Name);
        }

        private static Color[] GlyphPixels(ChartScheme scheme) {
            var chart = new NightChartControl();
            chart.SetScheme(scheme);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                chart.DrawBodyGlyph(dc, NOVAS.Body.Jupiter, new Point(16, 16), 11.5,
                    OverlayStyle.BrushFor(3, scheme), PlanetGlyphStyle.Detailed, null);
            }
            var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels = new byte[32 * 32 * 4];
            bitmap.CopyPixels(pixels, 32 * 4, 0);
            return pixels.Chunk(4).Select(channels => Color.FromArgb(channels[3], channels[2], channels[1], channels[0])).ToArray();
        }

        private static void Write(DrawingContext dc, string text, Point point) =>
            dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.White, 1), point);
    }
}
