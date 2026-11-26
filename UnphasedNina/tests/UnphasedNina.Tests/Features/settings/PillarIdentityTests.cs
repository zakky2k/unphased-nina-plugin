// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShapePath = System.Windows.Shapes.Path;

namespace Zakfo.Unphased.Tests.Features.settings {
    /// <summary>
    /// The three pillars are how this plugin describes itself, and they are meant to be the
    /// same three on every surface: the plugin's own page in N.I.N.A., the store listing built
    /// from <c>Plugin.props</c>, the README, and whatever documentation comes later.
    /// <para>
    /// Nothing here checks that a person likes the wording. What it checks is the thing that
    /// quietly rots without a test: that the page, the listing and the README still say the same
    /// sentences, that the page still leads with its real settings and puts the pillars last, above
    /// About - N.I.N.A. owns the strip above its own <em>Options</em> caption and a plugin template
    /// cannot reach above it, so the manifest description is the one opening line that sits there -
    /// and that the three drawings are the same drawings in WPF and in the
    /// <c>assets/pillars/*.svg</c> the README links. Wording drift is invisible - nobody notices the
    /// day the panel starts calling the Moon section something else - so it is pinned here rather
    /// than left to discipline.
    /// </para>
    /// <para>
    /// The drawings are checked against the agreed specification: a 64 x 64 canvas, stroke width
    /// 2, rounded caps and joins, displayed at 48 DIP, drawn with the theme's own foreground so
    /// they follow whatever palette N.I.N.A. is using, and drawn from geometry rather than a font
    /// glyph, an embedded image or a third-party icon package.
    /// </para>
    /// </summary>
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class PillarIdentityTests {
        /// <summary>The slogan round 12 put in the manifest line. Round 13 retired it: the one line a
        /// plugin can place above N.I.N.A.'s Options caption now says what the plugin does.</summary>
        private const string RetiredTagline = "Visualise the night. Plan your imaging.";

        /// <summary>The plugin's opening sentence. Since round 13 it lives in the manifest, where
        /// N.I.N.A. draws it in italic above its own Options caption, and is deliberately not
        /// repeated at the top of our options page.</summary>
        private const string Lede =
            "Visualise darkness, twilight and moonlight across consecutive nights in a single interactive astronomical chart.";
        private const string PillarsHeading = "Three perspectives. One view of the night.";

        /// <summary>The pillar copy, in the order the page shows it. This is the one place these
        /// sentences are written in this repository; everything else is asserted against it.</summary>
        private static readonly (string Title, string Body, string Tagline, string Svg)[] Pillars = {
            // The order is a decision, not an accident: the Moon is what decides whether a night is
            // worth setting up for, so it leads. Round 13 put it first and renamed two of the three.
            (
                "Lunar and planetary phases",
                "Explore the dynamic phases of the Moon, Mercury and Venus alongside their altitude curves. Identify periods when the Moon is below the horizon, assess potential moonlight interference and discover moon-free imaging opportunities.",
                "Understand the Moon and inner planets.",
                "lunar-phases.svg"),
            (
                "Astronomical darkness",
                "Visualise the transition from daylight through civil, nautical and astronomical twilight into astronomical darkness. Easily identify when darkness begins, when it ends and how much dark imaging time is available throughout each night.",
                "Understand the darkness.",
                "astronomical-darkness.svg"),
            (
                "Multi-night visualisation",
                "Look beyond tonight and explore how astronomical darkness and the Moon evolve across consecutive nights. Compare upcoming nights and identify favourable windows for future astrophotography sessions.",
                "Plan the nights ahead.",
                "multi-night-visualisation.svg"),
        };

        /// <summary>The words the settings half of the page uses, from the design: what each control
        /// is called is part of the identity as much as the pillars are, and the panel's compact
        /// palette buttons point at these names in their tooltips.</summary>
        private static readonly string[] RequiredControlWords = {
            "Observatory location",
            "Apply location",
            "Use N.I.N.A. profile location",
            "Display palette",
            "N.I.N.A. theme",
            "Night vision",
            "High contrast",
            "Custom chart colours",
            "Solar altitude",
            "Lunar altitude",
            "Horizon",
            "Restore default colours",
            "About Unphased Night Planner",
        };

        /// <summary>The words this page used before the identity pass. They are not wrong, but two
        /// names for one thing is: the panel and the listing both say "Night vision", so a page
        /// saying "Panel night mode" would leave a person hunting for the setting they were told about.</summary>
        private static readonly string[] RetiredWords = {
            "Apply site",
            "Use this profile's site",
            "Panel night mode",
            "Chart palette",
            "Sun path",
            "Moon path",
            "Reset colours to defaults",
            // Round 13 renamed these two pillars; the old titles must not come back on any surface.
            "Lunar conditions",
            "Multi-night planning",
            // Round 13 moved the plugin's opening sentence into the manifest line above N.I.N.A.'s
            // Options caption and retired the slogan it used to quote.
            RetiredTagline,
            Lede,
        };

        [Test]
        public void ThePageOpensWithItsSettingsAndClosesWithTheThreePerspectives() {
            var flat = Flatten(OptionsPage());

            var seen = Texts(flat).ToList();
            Assert.Multiple(() => {
                Assert.That(seen, Does.Contain(PillarsHeading), "the pillars get their own heading");
                foreach (var pillar in Pillars) {
                    Assert.That(seen, Does.Contain(pillar.Title), $"pillar title: {pillar.Title}");
                    Assert.That(seen, Does.Contain(pillar.Body), $"pillar body: {pillar.Title}");
                    Assert.That(seen, Does.Contain(pillar.Tagline), $"pillar tagline: {pillar.Title}");
                }
            });

            // Order is the actual design decision, not decoration. N.I.N.A. draws the plugin's name,
            // author, version, featured image and manifest description above its own "Options"
            // caption, and a plugin template cannot reach above that caption - so whatever the caption
            // hands us has to start with the real settings, and the three perspectives follow them,
            // closing the page above About. The plugin's opening sentence therefore lives in the
            // manifest, where it is the last thing read before the caption, and is kept off this page
            // by RetiredWords so the page does not say it a second time.
            int heading = IndexOfText(flat, PillarsHeading);
            int firstSettingHeading = IndexOfText(flat, "Observatory location");
            int firstOptionButton = IndexOf(flat, b => b is Button button && b is not null && button.Content is string);
            int about = IndexOfText(flat, "About Unphased Night Planner");
            Assert.Multiple(() => {
                Assert.That(firstSettingHeading, Is.GreaterThanOrEqualTo(0), "the settings lead the page");
                Assert.That(firstOptionButton, Is.GreaterThanOrEqualTo(0), "a real control is on the page");
                Assert.That(firstOptionButton, Is.LessThan(heading), "a real control comes before the pillars");
                Assert.That(heading, Is.GreaterThan(firstSettingHeading), "the pillars come after the settings");
                Assert.That(about, Is.GreaterThan(heading), "About closes the page");
                foreach (var pillar in Pillars) {
                    Assert.That(IndexOfText(flat, pillar.Title), Is.GreaterThan(heading), $"{pillar.Title} sits under the pillars heading");
                    Assert.That(IndexOfText(flat, pillar.Title), Is.LessThan(about), $"{pillar.Title} comes before About");
                }
                // And in the order the array above lists them, because that array is also what the
                // README and the store listing are checked against - a page that silently reorders
                // the pillars now has to say so here.
                for (int i = 1; i < Pillars.Length; i++) {
                    Assert.That(IndexOfText(flat, Pillars[i].Title),
                        Is.GreaterThan(IndexOfText(flat, Pillars[i - 1].Title)),
                        $"{Pillars[i].Title} is read after {Pillars[i - 1].Title}");
                }
            });
        }

        [Test]
        public void EachPillarHasItsOwnDrawingAtTheAgreedDisplaySize() {
            var flat = Flatten(OptionsPage());
            var icons = flat.OfType<Viewbox>().ToList();

            Assert.That(icons, Has.Count.EqualTo(Pillars.Length), "one drawing per pillar");
            foreach (var icon in icons) {
                Assert.Multiple(() => {
                    // 48 DIP is the standard display size from the specification; the canvas behind
                    // it is 64 units, which is why the stroke stays 2 units and still lands at
                    // 1.5 device pixels wide on a 96 DPI screen.
                    Assert.That(icon.Width, Is.EqualTo(48.0), "the drawing is shown at 48 DIP");
                    Assert.That(icon.Height, Is.EqualTo(48.0));
                    var canvas = icon.Child as Canvas;
                    Assert.That(canvas, Is.Not.Null, "the drawing is a canvas of geometry, not an image or a glyph");
                    Assert.That(canvas.Width, Is.EqualTo(64.0), "64 unit canvas");
                    Assert.That(canvas.Height, Is.EqualTo(64.0));
                    Assert.That(canvas.Children.OfType<ShapePath>().Count(), Is.InRange(3, 4), "three or four strokes tell the story");
                });
            }
        }

        [Test]
        public void EveryStrokeIsTwoUnitsWithRoundedCapsAndJoins() {
            var paths = Flatten(OptionsPage()).OfType<Viewbox>()
                .SelectMany(icon => ((Canvas)icon.Child).Children.OfType<ShapePath>()).ToList();
            Assert.That(paths, Has.Count.EqualTo(11), "four strokes for darkness, four for the Moon, three for the calendar");

            foreach (var path in paths) {
                Assert.Multiple(() => {
                    Assert.That(path.Data, Is.Not.Null);
                    Assert.That(path.Data.IsEmpty, Is.False, "every drawing is path geometry held in the plugin, not an asset load");
                    Assert.That(path.StrokeThickness, Is.EqualTo(2.0), "stroke width 2 units, as specified");
                    Assert.That(path.StrokeStartLineCap, Is.EqualTo(PenLineCap.Round));
                    Assert.That(path.StrokeEndLineCap, Is.EqualTo(PenLineCap.Round));
                    Assert.That(path.StrokeLineJoin, Is.EqualTo(PenLineJoin.Round));
                    Assert.That(path.Fill, Is.Null, "line art, so nothing is filled in");
                });
            }
        }

        /// <summary>
        /// The drawings have to follow the palette the host is using - that is the night-vision
        /// requirement in the specification - which in WPF means the stroke is a theme resource,
        /// never a literal colour. A bare test host has no theme, so the resolved brush is null
        /// here; what can be checked honestly is what the markup asks for, hence reading the file.
        /// </summary>
        [Test]
        public void TheDrawingsAskForTheThemeForegroundAndNeverALiteralColour() {
            var xaml = File.ReadAllText(OptionsPageXaml());

            var strokes = Regex.Matches(xaml, "Stroke=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.That(strokes, Has.Count.EqualTo(11), "every stroke declares its colour the same way");
            Assert.That(strokes, Is.All.EqualTo("{DynamicResource PrimaryBrush}"),
                "the drawings inherit the active theme foreground, so a night palette recolours them");

            // Nothing that could quietly turn into an icon font or a bundled picture later.
            var borrowed = new[] { "FontAwesome", "MaterialDesign", "Segoe MDL2", "Segoe Icons", "Fluent Icons" }
                .Where(name => xaml.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            Assert.Multiple(() => {
                Assert.That(xaml, Does.Not.Contain("<Image"), "no raster icon on this page");
                Assert.That(xaml, Does.Not.Contain("pack://"), "no packaged image on this page");
                Assert.That(borrowed, Is.Empty,
                    "the drawings are ours, drawn from geometry, with no icon package and no licence to audit");
            });
        }

        /// <summary>
        /// The README and any later documentation link the SVGs under <c>assets/pillars/</c>, so the
        /// pictures on the plugin's page and the pictures outside N.I.N.A. are the same pictures.
        /// One geometry, two renderings - and a test so that editing one of them without the other
        /// is a failure rather than a slow drift nobody notices.
        /// </summary>
        [Test]
        public void TheSvgDrawingsOutsideTheApplicationAreTheSameDrawings() {
            var root = RepositoryRoot();
            Assert.That(root, Is.Not.Null, "this test needs the checkout, not only the test binary");

            var xaml = File.ReadAllText(OptionsPageXaml());
            var blocks = Regex.Matches(xaml, "<Viewbox.*?</Viewbox>", RegexOptions.Singleline)
                .Select(m => GeometryIn(m.Value, "Data=\"([^\"]+)\""))
                .ToList();
            Assert.That(blocks, Has.Count.EqualTo(Pillars.Length), "one Viewbox per pillar, in the order the page shows them");

            foreach (var (pillar, block) in Pillars.Zip(blocks)) {
                var svgPath = Path.Combine(root, "UnphasedNina", "assets", "pillars", pillar.Svg);
                Assert.That(File.Exists(svgPath), Is.True, $"the README links {pillar.Svg}");
                var svg = File.ReadAllText(svgPath);

                Assert.Multiple(() => {
                    Assert.That(GeometryIn(svg, "d=\"([^\"]+)\""), Is.EquivalentTo(block),
                        $"{pillar.Svg} and the drawing on the page must be the same geometry");
                    Assert.That(svg, Does.Contain("viewBox=\"0 0 64 64\""), "same 64 unit canvas");
                    Assert.That(svg, Does.Contain("stroke-width=\"2\""), "same 2 unit stroke");
                    Assert.That(svg, Does.Contain("stroke-linecap=\"round\""));
                    Assert.That(svg, Does.Contain("stroke-linejoin=\"round\""));
                    // currentColor, not a colour: the page decides what the drawing looks like, so a
                    // light or dark documentation theme is not a second set of icons.
                    Assert.That(svg, Does.Contain("stroke=\"currentColor\""));
                });
            }
        }

        [Test]
        public void TheReadmeTellsTheSameStoryWithTheSameDrawings() {
            var root = RepositoryRoot();
            if (root == null) {
                Assert.Ignore("the README is not beside this test binary, so there is nothing to compare");
            }

            // Markdown is written wrapped at a readable width, so a sentence in the README has a
            // newline in the middle of it. Compare with the wrapping collapsed, otherwise this
            // becomes a test of where the lines are broken.
            var readme = Collapse(File.ReadAllText(Path.Combine(root, "UnphasedNina", "README.md")));
            Assert.Multiple(() => {
                Assert.That(readme, Does.Contain(Collapse(Lede)), "the README opens with the manifest line");
                Assert.That(readme, Does.Not.Contain(Collapse(RetiredTagline)), "the retired slogan stays off the README");
                Assert.That(readme, Does.Contain(PillarsHeading));
                foreach (var pillar in Pillars) {
                    Assert.That(readme, Does.Contain(pillar.Title), $"README pillar title: {pillar.Title}");
                    Assert.That(readme, Does.Contain(Collapse(pillar.Body)), $"README pillar body: {pillar.Title}");
                    Assert.That(readme, Does.Contain(pillar.Tagline), $"README pillar tagline: {pillar.Title}");
                    Assert.That(readme, Does.Contain(pillar.Svg), $"README shows the pillar drawing: {pillar.Svg}");
                }
            });
        }

        [Test]
        public void TheManifestLineCarriesTheOpeningSentenceNinaDrawsAboveItsCaption() {
            var descriptions = new Plugin().Descriptions;

            Assert.Multiple(() => {
                // N.I.N.A. draws this in italic under the plugin name above its own Options caption,
                // and quotes it in the store list. It is the only prose a plugin can place above that
                // caption, so it carries the plugin's opening sentence rather than a slogan - and the
                // options page below the caption starts with its settings instead of repeating it.
                Assert.That(descriptions.ShortDescription, Is.EqualTo(Lede));
                Assert.That(descriptions.ShortDescription.Length, Is.LessThanOrEqualTo(256));
                Assert.That(descriptions.LongDescription, Does.StartWith(Lede),
                    "the long description leads with the same line, because the store page has no header of its own");
                Assert.That(descriptions.LongDescription, Does.Not.Contain(RetiredTagline),
                    "the retired slogan stays out of the listing");
                Assert.That(descriptions.LongDescription, Does.Contain(PillarsHeading));
                foreach (var pillar in Pillars) {
                    Assert.That(descriptions.LongDescription, Does.Contain(pillar.Title), $"listing pillar title: {pillar.Title}");
                    Assert.That(descriptions.LongDescription, Does.Contain(pillar.Body), $"listing pillar body: {pillar.Title}");
                    Assert.That(descriptions.LongDescription, Does.Contain(pillar.Tagline), $"listing pillar tagline: {pillar.Title}");
                }
                Assert.That(descriptions.LongDescription.Length, Is.LessThanOrEqualTo(10000));
            });
        }

        [Test]
        public void TheSettingsAreCalledWhatTheRestOfThePluginCallsThem() {
            var words = AllWords(OptionsPage());

            Assert.Multiple(() => {
                foreach (var required in RequiredControlWords) {
                    Assert.That(words, Does.Contain(required), $"the page names its controls '{required}'");
                }
                foreach (var retired in RetiredWords) {
                    Assert.That(words, Does.Not.Contain(retired), $"'{retired}' is retired - one name per thing, everywhere");
                }
            });
        }

        /// <summary>The options page, loaded the way N.I.N.A. loads it and laid out, so the visual
        /// tree is real: the template is keyed by the manifest name and there is no application
        /// theme behind it, which is also why the markup cannot rely on implicit styles.</summary>
        private static FrameworkElement OptionsPage() {
            var dictionary = new Zakfo.Unphased.Features.settings.Resources();
            var key = new Plugin().Name + "_Options";
            Assert.That(dictionary.Contains(key), Is.True, $"the options template is keyed '{key}'");

            var root = (FrameworkElement)((DataTemplate)dictionary[key]).LoadContent();
            root.Measure(new Size(900, 1600));
            root.Arrange(new Rect(0, 0, 900, 1600));
            root.UpdateLayout();
            return root;
        }

        private static string OptionsPageXaml() {
            var root = RepositoryRoot();
            Assert.That(root, Is.Not.Null, "this test reads the markup, so it needs the checkout");
            var path = Path.Combine(
                root, "UnphasedNina", "src", "UnphasedNina", "Features", "settings", "Resources.xaml");
            Assert.That(File.Exists(path), Is.True, path);
            return path;
        }

        private static List<string> GeometryIn(string text, string pattern) =>
            Regex.Matches(text, pattern).Select(m => m.Groups[1].Value.Trim()).ToList();

        /// <summary>Run any whitespace, including a wrapped line, to a single space.</summary>
        private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();

        /// <summary>Depth-first, in the order the markup declares things, which is the order a
        /// person reads the page top to bottom. Deliberately recursive rather than a queue: a queue
        /// walks a level at a time, and the ordering this file asserts is the reading order.</summary>
        private static List<DependencyObject> Flatten(FrameworkElement root) {
            var all = new List<DependencyObject>();
            AddTree(root, all);
            return all;
        }

        private static void AddTree(DependencyObject node, List<DependencyObject> all) {
            all.Add(node);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) {
                AddTree(VisualTreeHelper.GetChild(node, i), all);
            }
        }

        private static IEnumerable<string> Texts(IEnumerable<DependencyObject> flat) =>
            flat.OfType<TextBox>().Where(t => t.IsReadOnly)
                .Select(t => (t.Text ?? string.Empty).Trim())
                .Where(t => t.Length > 0);

        /// <summary>Every word-ish string the page shows: static text plus the content of the
        /// buttons, which is where the naming either matches the rest of the plugin or does not.</summary>
        private static List<string> AllWords(FrameworkElement root) {
            var flat = Flatten(root);
            var words = Texts(flat).ToList();
            words.AddRange(flat.OfType<Button>()
                .Select(b => (b.Content as string) ?? string.Empty)
                .Where(c => c.Length > 0));
            return words;
        }

        private static int IndexOfText(List<DependencyObject> flat, string text) {
            for (int i = 0; i < flat.Count; i++) {
                if (flat[i] is TextBox { IsReadOnly: true } field && (field.Text ?? string.Empty).Trim() == text) return i;
            }
            return -1;
        }

        private static int IndexOf(List<DependencyObject> flat, Func<DependencyObject, bool> match) {
            for (int i = 0; i < flat.Count; i++) {
                if (match(flat[i])) return i;
            }
            return -1;
        }

        /// <summary>Same walk as <see cref="LicenceHeaderTests"/>: this test reads files from the
        /// checkout rather than trusting the build output, which is what makes the copy comparison
        /// mean anything.</summary>
        private static string RepositoryRoot() {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }) {
                var dir = new DirectoryInfo(start);
                for (int hop = 0; dir != null && hop < 10; hop++, dir = dir.Parent) {
                    if (File.Exists(Path.Combine(dir.FullName, "UnphasedNina", "CHANGELOG.md"))
                        && File.Exists(Path.Combine(dir.FullName, "UnphasedCore", "NightCalculator.cs"))) {
                        return dir.FullName;
                    }
                }
            }
            return null;
        }
    }
}
