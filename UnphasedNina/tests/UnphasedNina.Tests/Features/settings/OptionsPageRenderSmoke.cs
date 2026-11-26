// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;

namespace Zakfo.Unphased.Tests.Features.settings {
    /// <summary>
    /// Visual smoke test, not part of the automated run: renders the plugin's own options page - the
    /// real <c>OptionsTemplate</c>, with the real settings object behind it - to PNGs in %TEMP% so the
    /// page can be eyeballed without starting N.I.N.A.
    /// <para>
    /// The identity tests say the words are there and in the right order; a picture is the only way to
    /// see whether the page reads as intended: whether the pillars sit above the settings, whether the
    /// three drawings are legible at 48 DIP, and whether a wrapped paragraph has become a column three
    /// words wide. Those are the failures a wiring test cannot see.
    /// </para>
    /// <para>
    /// The third picture is the drawings alone at the specification's large size, and it is not only
    /// for looking at: the test counts the pixels they paint, so a drawing that resolves to nothing -
    /// a theme brush the host does not have, a path string the parser rejected - fails here with a
    /// number rather than showing up as quiet blank space in a screenshot.
    /// </para>
    /// </summary>
    [Explicit("Writes PNGs to %TEMP%; run by hand to eyeball the options page.")]
    [NonParallelizable]
    public class OptionsPageRenderSmoke {
        [Test]
        public void RenderTheOptionsPageAndTheThreeDrawings() {
            var paths = new List<string>();
            var worker = new System.Threading.Thread(() => paths.AddRange(RenderAll()));
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join();

            Assert.That(paths, Has.Count.EqualTo(3));
            foreach (var path in paths) Assert.That(new FileInfo(path).Length, Is.GreaterThan(2000), path);
            TestContext.Progress.WriteLine(string.Join(Environment.NewLine, paths));
        }

        private static IEnumerable<string> RenderAll() {
            // Stand-in theme. Inside N.I.N.A. these come from its own theme dictionaries; without
            // them every DynamicResource on the page resolves to nothing, which on a bare host means
            // black text on transparent and strokes that are not painted at all. The point of the
            // picture is the layout and the drawings, so the host supplies a dark theme in the
            // colours this plugin is seen in - not N.I.N.A.'s real chrome, which a test host cannot have.
            var navy = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x24));
            var ink = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6));
            var muted = new SolidColorBrush(Color.FromRgb(0x9C, 0xAC, 0xC2));
            var line = new SolidColorBrush(Color.FromArgb(0x80, 0x5A, 0x6E, 0x8C));

            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            var astro = new Mock<IAstrometrySettings>();
            astro.SetupGet(a => a.Latitude).Returns(50.08804);
            astro.SetupGet(a => a.Longitude).Returns(14.42076);
            profile.SetupGet(p => p.AstrometrySettings).Returns(astro.Object);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);

            // The real settings object, initialised: the readouts below the site fields and the
            // palette buttons are what a person actually sees, and they only have text in them when
            // the settings have been loaded.
            var settings = new Zakfo.Unphased.Features.settings.PluginSettings(profiles.Object);
            settings.Initialize();

            var dictionary = new Zakfo.Unphased.Features.settings.Resources();
            var key = new Plugin().Name + "_Options";
            var page = (FrameworkElement)((DataTemplate)dictionary[key]).LoadContent();
            page.DataContext = new { Settings = settings };

            foreach (var width in new[] { 860 }) {
                var host = new Grid { Background = navy, Width = width };
                host.Resources["PrimaryBrush"] = ink;
                host.Resources["SecondaryBrush"] = muted;
                host.Resources["BorderBrush"] = line;
                host.Resources["BackgroundBrush"] = navy;
                host.Resources["SecondaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x38));
                host.Children.Add(page);
                host.Measure(new Size(width, 2400));
                double height = Math.Ceiling(Math.Max(host.DesiredSize.Height, 400));
                host.Arrange(new Rect(0, 0, width, height));
                host.UpdateLayout();
                TestContext.Progress.WriteLine($"options page laid out at {width} x {height} DIP");

                yield return Save(host, width, (int)height, "options-page-at-860");

                // The pillars band on its own: the part the round is about, at the size it is read at.
                int band = Math.Min((int)height, 470);
                yield return Save(host, width, band, "options-page-pillars-at-860");
            }

            // The drawings at the specification's large size, 64 DIP, taken from the markup itself
            // rather than redrawn here - so what is measured is what the page draws.
            yield return RenderDrawings(ink, navy);
        }

        /// <summary>
        /// Pull each <c>Viewbox</c> out of the page's own markup, show it at 64 DIP, and count the
        /// pixels it paints. A stroke that resolves to nothing paints nothing, and that is exactly the
        /// mistake this catches.
        /// </summary>
        private static string RenderDrawings(SolidColorBrush ink, SolidColorBrush navy) {
            var xaml = File.ReadAllText(OptionsPageXaml());
            var blocks = System.Text.RegularExpressions.Regex.Matches(xaml, "<Viewbox.*?</Viewbox>",
                    System.Text.RegularExpressions.RegexOptions.Singleline)
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .ToList();
            Assert.That(blocks, Has.Count.EqualTo(3), "one drawing per pillar, in the order the page shows them");

            var host = new Canvas { Width = 260, Height = 96, Background = navy };
            host.Resources["PrimaryBrush"] = ink;

            int x = 14;
            foreach (var block in blocks) {
                // A Border has a content property, so the fragment parses with the Viewbox as its
                // child; the Viewbox itself is what the page draws, unchanged.
                var box = (Border)XamlReader.Parse(
                    "<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">"
                    + block + "</Border>");
                var icon = (Viewbox)box.Child;
                // Detach it from the throwaway Border, or WPF refuses to parent it twice.
                box.Child = null;
                icon.Width = 64;
                icon.Height = 64;
                Canvas.SetLeft(icon, x);
                Canvas.SetTop(icon, 16);
                host.Children.Add(icon);
                x += 82;
            }

            host.Measure(new Size(260, 96));
            host.Arrange(new Rect(0, 0, 260, 96));
            host.UpdateLayout();

            var path = Save(host, 260, 96, "pillar-icons-at-64");

            // Count what the strokes actually painted, against the background colour.
            var rtb = new RenderTargetBitmap(260, 96, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(host);
            var pixels = new byte[260 * 96 * 4];
            rtb.CopyPixels(pixels, 260 * 4, 0);
            int painted = 0;
            for (int i = 0; i < pixels.Length; i += 4) {
                // Pbgra32: blue first, then green, then red.
                if (Math.Abs(pixels[i] - navy.Color.B) > 24
                    || Math.Abs(pixels[i + 1] - navy.Color.G) > 24
                    || Math.Abs(pixels[i + 2] - navy.Color.R) > 24) painted++;
            }
            TestContext.Progress.WriteLine($"the three drawings painted {painted} pixels at 64 DIP (260 x 96 frame)");
            Assert.That(painted, Is.GreaterThan(1200),
                "the strokes painted nothing: a theme brush that does not resolve, or geometry the parser rejected");
            return path;
        }

        private static string Save(FrameworkElement host, int width, int height, string name) {
            var path = Path.Combine(Path.GetTempPath(), name + ".png");
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(host);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var stream = File.Create(path)) encoder.Save(stream);
            return path;
        }

        private static string OptionsPageXaml() {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }) {
                var dir = new DirectoryInfo(start);
                for (int hop = 0; dir != null && hop < 10; hop++, dir = dir.Parent) {
                    var candidate = Path.Combine(
                        dir.FullName, "UnphasedNina", "src", "UnphasedNina", "Features", "settings", "Resources.xaml");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            Assert.Fail("the page's own markup was not found beside this test binary");
            return null;
        }
    }
}
