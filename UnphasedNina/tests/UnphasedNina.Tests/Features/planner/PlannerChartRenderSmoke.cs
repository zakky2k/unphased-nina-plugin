using NUnit.Framework;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// Visual smoke test, not part of the automated run: renders the chart to PNG
    /// files in %TEMP% so a layout change can be eyeballed without starting
    /// N.I.N.A. Run it explicitly after touching NightChartControl.
    /// </summary>
    [Explicit("Writes PNGs to %TEMP%; run by hand to eyeball chart layout.")]
    [NonParallelizable]
    public class PlannerChartRenderSmoke {
        // One STA thread and one Application for the whole fixture. Only one Application may
        // exist in a process, and every visual painted here belongs to the thread that made
        // it, so each test method cannot open its own worker: the first worker to finish would
        // take its dispatcher with it and the next test would hang instead of painting.
        // The thread therefore stays open for the fixture and the tests hand their work to it.
        private System.Threading.Thread studio;
        private System.Windows.Threading.Dispatcher pump;

        [OneTimeSetUp]
        public void OpenTheStudio() {
            var ready = new System.Threading.ManualResetEventSlim(false);
            studio = new System.Threading.Thread(() => {
                pump = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                // Stand-in theme: inside N.I.N.A. these brushes come from its own theme
                // dictionaries, and without them the palette falls back to flat grey, which
                // hides the very thing being checked. An Application that another fixture
                // already owns is reused rather than replaced, because replacing is not legal.
                var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources["TertiaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x16, 0x22));
                app.Resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x6F, 0xC8, 0xE8));
                app.Resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xE8, 0xC8));
                app.Resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0x5A, 0x6E, 0x8C));
                app.Resources["ButtonBackgroundSelectedBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3C));
                ready.Set();
                // The static pump for whatever dispatcher this thread owns. Without it the
                // thread ends, and with it the render thread for everything made on it.
                System.Windows.Threading.Dispatcher.Run();
            });
            studio.SetApartmentState(ApartmentState.STA);
            studio.Start();
            ready.Wait();
        }

        [OneTimeTearDown]
        public void CloseTheStudio() {
            pump.InvokeShutdown();
            studio.Join();
        }

        [Test]
        public void RenderZoomsAndScrubPreviews() {
            var paths = new List<string>();
            pump.Invoke(() => {
                var calc = new NightCalculator(new Site(50.08804, 14.42076));
                var tz = TimeZoneInfo.Local;
                // Nov 14..24: the same span the panel prefetches, so a 7-night zoom
                // out of the 18th really does have the following nights to stitch.
                var nights = new List<NightPayload>();
                for (int day = 14; day <= 24; day++) {
                    var noon = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, day, 12, 0, 0), tz);
                    nights.Add(calc.Compute(new DateTimeOffset(noon, TimeSpan.Zero)));
                }
                NightPayload Night(int day) => nights[day - 14];

                // (first night's evening day, nights in the frame, panel width,
                //  scrub hours after local noon, panel height). One night first, then
                // the zoom ladder, then a deliberately narrow panel at the widest zoom,
                // then scrubs: one at the moon's high hour, one inside a zoomed frame.
                foreach (var (day, zoom, width, scrub, height) in new[] {
                        (24, 1, 1200, -1, 360),
                        (24, 1, 720, -1, 360),
                        (22, 2, 1200, -1, 360),
                        (22, 3, 1200, -1, 360),
                        (18, 7, 1200, -1, 360),
                        (18, 7, 620, -1, 260),
                        (24, 1, 1200, 13, 360),
                        // 13:00 on the 14th: the moon is up, a quarter lit, and well to
                        // the left of its peak, so the phase riding on the scrub marker
                        // is unmistakable rather than hidden behind the peak glyph.
                        (14, 1, 1200, 1, 360),
                        (22, 3, 1200, 36, 360),
                        // Round eight: the height the chart now asks for, so the altitude
                        // ladder is drawn at 1:1 rather than squeezed by 0.81, then the
                        // same at a week wide, and a panel squeezed to its minimum height
                        // to see which degrees get dropped instead of piled up.
                        (24, 1, 1200, -1, 320),
                        (18, 7, 1200, -1, 320),
                        (24, 1, 420, -1, 160),
                    }) {
                    var slice = new List<NightPayload>();
                    for (int d = day; d < day + zoom && d <= 24; d++) slice.Add(Night(d));
                    paths.Add(Paint(slice, width, scrub, height));
                }
            });

            Assert.That(paths, Has.Count.EqualTo(12));
            foreach (var path in paths) Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(2000));
            TestContext.Progress.WriteLine(string.Join(System.Environment.NewLine, paths));
        }

        /// <summary>
        /// Round 8, T5: the same night in each of the three palettes, one run, so the answer
        /// to the only question that matters about the Enhanced chart - do the sun, the moon
        /// and the horizon still read as three separate lines - can be seen next to the two
        /// palettes it has to live beside.
        /// </summary>
        [Test]
        public void RenderTheThreePalettes() {
            var paths = new List<string>();
            pump.Invoke(() => {
                var calc = new NightCalculator(new Site(50.08804, 14.42076));
                var tz = TimeZoneInfo.Local;
                var slice = new List<NightPayload>();
                for (int day = 22; day <= 24; day++) {
                    var noon = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, day, 12, 0, 0), tz);
                    slice.Add(calc.Compute(new DateTimeOffset(noon, TimeSpan.Zero)));
                }

                // Two nights wide and scrubbed at 01:00: the sun is at its lowest, the moon
                // is up and high, and the altitude chips are all out at once, so a palette
                // that collapses two of the lines into one shows up here.
                foreach (var scheme in new[] { ChartScheme.Default, ChartScheme.Night, ChartScheme.Enhanced }) {
                    paths.Add(Paint(slice, 1200, 13, 320, scheme));
                }
            });

            Assert.That(paths, Has.Count.EqualTo(3));
            foreach (var path in paths) Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(2000));
            TestContext.Progress.WriteLine(string.Join(System.Environment.NewLine, paths));
        }

        private string Paint(IReadOnlyList<NightPayload> slice,
            int width, int scrubHoursAfterNoon, int height, ChartScheme scheme = ChartScheme.Default) {
            var frame = NightFrame.For(slice, slice.Count);
            var first = slice[0];

            string suffix = scrubHoursAfterNoon < 0 ? "" : $"-scrub{(12 + scrubHoursAfterNoon) % 24:00}h";
            // The height is part of the name: the round eight cases differ from the older
            // ones only by how tall the element is, and sharing a file name would hide one
            // behind the other. The palette is named the same way, but only when it is not
            // the default, so the previews taken before round eight keep their names.
            string palette = scheme == ChartScheme.Default ? "" : $"-{scheme.ToString().ToLowerInvariant()}";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"planner-preview-{first.FrameNoon.LocalDateTime:dd}-z{slice.Count}-at-{width}x{height}{suffix}{palette}.png");
            var ctl = new NightChartControl();
            ctl.SetRow(new NightRowVM(frame, NightChartVM.FrameMinutesAtZoom(slice.Count)));
            ctl.SetScheme(scheme);
            if (scrubHoursAfterNoon >= 0) ctl.SetScrub(scrubHoursAfterNoon * 60.0);
            ctl.Measure(new Size(width, height));
            ctl.Arrange(new Rect(0, 0, width, height));
            ctl.UpdateLayout();
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(ctl);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(path)) enc.Save(fs);
            return path;
        }
    }
}
