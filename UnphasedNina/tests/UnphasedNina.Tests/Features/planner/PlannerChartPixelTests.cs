using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// What the renderer paints, checked on pixels rather than on the polyline string.
    /// The string cuts a track where it drops below the floor of the window, but the
    /// control used to join those runs into one figure, and the pen then drew a straight
    /// line along the bottom edge for every hour the sun was under it - which read as a
    /// feature of the sky rather than as a clipped curve.
    /// </summary>
    [NonParallelizable]
    public class PlannerChartPixelTests {
        // Chosen as a picked colour, which is how a real user gets a distinctive sun
        // track: magenta is not used by the horizon (green), the moon (cream) or the
        // accents (orange), and it needs no theme resources at all.
        private const string SunPick = "#FF00FF";

        [Test]
        public void ATrackThatDropsBelowTheFloorStopsThereInsteadOfRunningAlongIt() {
            int longest = 0;
            var worker = new Thread(() => longest = LongestSunRunOnTheBottomEdge());
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join();

            // The sun sits below -24 degrees for roughly a quarter of this frame, so a
            // bridged gap is a couple of hundred pixels wide. Where the track genuinely
            // crosses the floor it covers a handful of pixels at most.
            Assert.That(longest, Is.LessThan(100),
                "a flat run of sun colour along the bottom edge is the cut being bridged");
        }

        /// <summary>
        /// The degrees used to stand on the right edge only, so reading a height near the
        /// middle of a wide chart meant walking your eye to the far side of it. Checked on
        /// the chart's own drawing: the claim is that the two columns are the same boxes at
        /// the same heights, and that is a statement about geometry, not about colour - the
        /// palette is free to repaint itself and this must still hold.
        /// </summary>
        [Test]
        public void TheDegreesAreDrawnOnBothEdgesAtTheSameHeights() {
            List<Rect>[] edges = null;
            var worker = new Thread(() => edges = DegreeChipsOnBothEdges());
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join();

            var left = edges[0];
            var right = edges[1];
            Assert.Multiple(() => {
                Assert.That(left, Has.Count.EqualTo(NightChartVM.LabelAltitudes.Count),
                    "every degree on the ladder is printed down the left edge as well");
                Assert.That(right, Has.Count.EqualTo(NightChartVM.LabelAltitudes.Count));
                for (int i = 0; i < left.Count; i++) {
                    Assert.That(left[i].Left, Is.LessThan(6), "hard against the left edge");
                    Assert.That(right[i].Right, Is.GreaterThan(600 - 6), "hard against the right edge");
                    Assert.That(left[i].Top, Is.EqualTo(right[i].Top).Within(0.5),
                        "the two columns say the same thing at the same height");
                }
                Assert.That(left[0].Top, Is.LessThan(1), "the zenith degree is not clipped off the top");
            });
        }

        /// <summary>Chip boxes on the left edge and on the right edge, each top down.</summary>
        private static List<Rect>[] DegreeChipsOnBothEdges() {
            const int width = 600, height = 320;
            var calc = new NightCalculator(new Site(50.08804, 14.42076));
            var noon = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 24, 12, 0, 0), TimeZoneInfo.Local);
            var payload = calc.Compute(new DateTimeOffset(noon, TimeSpan.Zero));
            var frame = NightFrame.For(new[] { payload }, 1);

            var ctl = new Probe();
            ctl.SetRow(new NightRowVM(frame, NightChartVM.FrameMinutesAtZoom(1)));
            ctl.Measure(new Size(width, height));
            ctl.Arrange(new Rect(0, 0, width, height));
            ctl.UpdateLayout();

            var boxes = new List<Rect>();
            CollectChipBoxes(ctl.Paint(), boxes);
            return new[] {
                boxes.Where(b => b.Left < 6).OrderBy(b => b.Top).ToList(),
                boxes.Where(b => b.Right > width - 6).OrderBy(b => b.Top).ToList(),
            };
        }

        /// <summary>
        /// Every label the chart backed with a box and edged it with a hairline in the
        /// label's own colour: that pair is what separates a label from the chart, since a
        /// curve or a twilight band carries a brush or a pen, never both. Two chips that
        /// share a pen and are drawn one after the other can be folded into a single
        /// drawing by the renderer - the horizon degree does exactly that, on both edges -
        /// and such a box starts at one edge and ends at the other, so it is picked up by
        /// both columns, which is what it is.
        /// </summary>
        private static void CollectChipBoxes(DrawingGroup group, List<Rect> boxes) {
            foreach (var child in group.Children) {
                if (child is DrawingGroup nested) {
                    CollectChipBoxes(nested, boxes);
                    continue;
                }
                if (child is not GeometryDrawing drawn || drawn.Pen == null || drawn.Brush == null) continue;
                var b = drawn.Bounds;
                if (b.Width >= 12 && b.Height is >= 14 and <= 20) boxes.Add(b);
            }
        }

        /// <summary>Hands out the chart's own drawing, so what it paints can be counted.</summary>
        private class Probe : NightChartControl {
            public DrawingGroup Paint() {
                var group = new DrawingGroup();
                using (var dc = group.Open()) OnRender(dc);
                return group;
            }
        }

        /// <summary>Widest run of sun-coloured pixels found on the bottom rows of the rendered chart.</summary>
        private static int LongestSunRunOnTheBottomEdge() {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            // A picked colour, so the sun track is a colour nothing else on the chart wears.
            var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            settings.SunColor = SunPick;
            try {
                return Measure(settings);
            } finally {
                settings.ResetColors();
                settings.Dispose();
            }
        }

        private static int Measure(PluginSettings settings) {
            const int width = 600, height = 320;
            var calc = new NightCalculator(new Site(50.08804, 14.42076));
            // Prague, 24 November: the sun sinks to -60 degrees, far below the -24 floor
            // of the chart, so its track leaves the window for hours and comes back.
            var noon = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 24, 12, 0, 0), TimeZoneInfo.Local);
            var payload = calc.Compute(new DateTimeOffset(noon, TimeSpan.Zero));
            var frame = NightFrame.For(new[] { payload }, 1);

            var planner = new NightPlannerVM(settings);
            var ctl = new NightChartControl();
            ctl.DataContext = planner;   // this is how the control takes its picked colours
            ctl.SetRow(new NightRowVM(frame, NightChartVM.FrameMinutesAtZoom(1)));
            ctl.Measure(new Size(width, height));
            ctl.Arrange(new Rect(0, 0, width, height));
            ctl.UpdateLayout();

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(ctl);
            var stride = width * 4;
            var pixels = new byte[stride * height];
            rtb.CopyPixels(pixels, stride, 0);

            int longest = 0;
            for (int y = height - 4; y < height; y++) {
                int run = 0;
                for (int x = 0; x < width; x++) {
                    int i = y * stride + x * 4;
                    // Pbgra32 straight out of the render target, so the order is B, G, R.
                    bool sun = pixels[i] > 0xB0 && pixels[i + 1] < 0x50 && pixels[i + 2] > 0xB0;
                    run = sun ? run + 1 : 0;
                    if (run > longest) longest = run;
                }
            }
            return longest;
        }
    }
}
