// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// Visual smoke test, not part of the automated run: renders the whole panel - the real
    /// data template, with the real view model behind it - to PNGs in %TEMP% so the header
    /// can be eyeballed without starting N.I.N.A. The chart-only smoke covers the picture;
    /// this one covers the row above it, which is where the calendar and the night's three
    /// figures live and where a binding that resolves to nothing shows up as blank space
    /// rather than as a failure. Run it by hand after touching planner/Resources.xaml.
    /// </summary>
    [Explicit("Writes PNGs to %TEMP%; run by hand to eyeball the panel header.")]
    [NonParallelizable]
    public class PlannerPanelRenderSmoke {
        [Test]
        public void RenderThePanelAtTwoWidths() {
            var paths = new List<string>();
            var worker = new System.Threading.Thread(() => {
                // Stand-in theme, as the chart smoke uses: inside N.I.N.A. these brushes come
                // from its own theme dictionaries, and without them everything the panel paints
                // with DynamicResource turns transparent.
                var app = Application.Current ?? new Application();
                app.Resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x24));
                app.Resources["SecondaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x38));
                app.Resources["TertiaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x16, 0x22));
                app.Resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6));
                app.Resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9C, 0xAC, 0xC2));
                app.Resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0x5A, 0x6E, 0x8C));
                // N.I.N.A.'s own button chrome is not available to a test host, so the buttons
                // here are painted by WPF's default style: light grey boxes. Their text therefore
                // has to be dark to be legible in the picture, even though inside N.I.N.A. the same
                // key resolves to a light colour on the navy chrome. The picture shows the layout,
                // not the live colours.
                app.Resources["ButtonForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x24));

                var profiles = new Mock<IProfileService>();
                var profile = new Mock<IProfile>();
                profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
                // A site on the profile, so the planner can work a window out for itself. The
                // busy render below needs the real compute path, and with no site it never starts.
                var astro = new Mock<IAstrometrySettings>();
                astro.SetupGet(a => a.Latitude).Returns(50.08804);
                astro.SetupGet(a => a.Longitude).Returns(14.42076);
                profile.SetupGet(p => p.AstrometrySettings).Returns(astro.Object);
                profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
                var panel = new PlannerDockableVM(
                    profiles.Object,
                    new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);

                // The same window the panel prefetches, computed for the real Prague site,
                // so the chips quote numbers a real night produces rather than fixture ones.
                var calc = new NightCalculator(new Site(50.08804, 14.42076));
                var tz = TimeZoneInfo.Local;
                var nights = new List<NightPayload>();
                for (int day = 16; day <= 25; day++) {
                    var noon = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, day, 12, 0, 0), tz);
                    nights.Add(calc.Compute(new DateTimeOffset(noon, TimeSpan.Zero)));
                }
                panel.Planner.ShowWindow(nights);
                panel.Planner.SelectedDate = new DateTime(2026, 11, 25);

                // 1200 shows the four clusters on one line; 900 and 620 are where the WrapPanel
                // earns its keep, because the clusters ask for about 1140 DIP between them and a
                // narrow dock has to drop whole clusters to a second line rather than clip a button.
                foreach (var width in new[] { 1200, 900, 620 }) paths.Add(Render(panel, width, "planner-panel"));
                paths.Add(RenderWhilePagingGrowsTheWindow(profiles.Object));
            });
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join();

            Assert.That(paths, Has.Count.EqualTo(4));
            foreach (var path in paths) Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(2000));
            TestContext.Progress.WriteLine(string.Join(Environment.NewLine, paths));
        }

        /// <summary>
        /// The one round-8 question the live panel never answered: when the calendar walks off
        /// the end of the computed window, does the "computing..." readout ever actually appear,
        /// and for how long? Nothing is faked here - a second panel asks for its own first window,
        /// then pages nineteen nights along it, which is the walk that triggers the extension.
        /// Busy goes up on this thread before the compute task leaves, so the render catches it;
        /// the pump is what a real interface thread does while the answer is on its way, and the
        /// elapsed figure is the honest answer to "did I see it".
        /// </summary>
        private static string RenderWhilePagingGrowsTheWindow(IProfileService profiles) {
            var paging = new PlannerDockableVM(
                profiles, new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            paging.Planner.SelectedDate = new DateTime(2026, 11, 25);
            var cold = System.Diagnostics.Stopwatch.StartNew();
            paging.Planner.EnsureComputed();
            PumpUntil(() => !paging.Planner.Busy && paging.Planner.Nights.Count > 0, "the first window arrived");
            cold.Stop();
            TestContext.Progress.WriteLine(
                $"first window, {paging.Planner.Nights.Count} nights from nothing: Busy was up for {cold.ElapsedMilliseconds} ms");

            int before = paging.Planner.Nights.Count;
            paging.Planner.SelectedDate = new DateTime(2026, 11, 25).AddDays(NightPlannerVM.FutureNights - 1);
            var snapshot = Render(paging, 620, "planner-panel-computing");
            Assert.That(paging.Planner.Busy, Is.True, "the walk off the edge left the panel working");

            var clock = System.Diagnostics.Stopwatch.StartNew();
            PumpUntil(() => !paging.Planner.Busy, "the wider window arrived");
            clock.Stop();
            TestContext.Progress.WriteLine(
                $"paging past the edge: {before} nights -> {paging.Planner.Nights.Count} nights, "
                + $"Busy was up for {clock.ElapsedMilliseconds} ms");
            return snapshot;
        }

        /// <summary>Pump this thread's dispatcher queue the way N.I.N.A.'s interface thread does.</summary>
        private static void PumpUntil(Func<bool> ready, string what, int budgetMs = 30000) {
            var ui = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            var deadline = System.Environment.TickCount64 + budgetMs;
            while (System.Environment.TickCount64 < deadline && !ready()) {
                var frame = new System.Windows.Threading.DispatcherFrame();
                ui.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            Assert.That(ready(), Is.True, $"timed out: {what} did not happen");
        }

        private static string Render(PlannerDockableVM panel, int width, string name) {
            const int height = 520;
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                name == "planner-panel" ? $"planner-panel-at-{width}.png" : $"{name}-at-{width}.png");

            var dictionary = new Zakfo.Unphased.Features.planner.Resources();
            var key = typeof(PlannerDockableVM).FullName + "_Dockable";
            var root = (FrameworkElement)((DataTemplate)dictionary[key]).LoadContent();
            root.DataContext = panel;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(path)) enc.Save(fs);
            return path;
        }
    }
}
