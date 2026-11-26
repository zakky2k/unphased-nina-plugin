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
                app.Resources["ButtonForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6));

                var profiles = new Mock<IProfileService>();
                var profile = new Mock<IProfile>();
                profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
                profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
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

                foreach (var width in new[] { 1200, 620 }) paths.Add(Render(panel, width));
            });
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join();

            Assert.That(paths, Has.Count.EqualTo(2));
            foreach (var path in paths) Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(2000));
            TestContext.Progress.WriteLine(string.Join(Environment.NewLine, paths));
        }

        private static string Render(PlannerDockableVM panel, int width) {
            const int height = 520;
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"planner-panel-at-{width}.png");

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
