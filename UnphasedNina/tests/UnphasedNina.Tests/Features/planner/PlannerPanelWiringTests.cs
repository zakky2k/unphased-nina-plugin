// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// The panel as N.I.N.A. will actually instantiate it: the real data template, loaded,
    /// given the real view model, and laid out in a window. The night list used to be the
    /// thing that handed the chart its row and the thing that kicked off the first
    /// computation; with a calendar in its place, those wires are now attached properties on
    /// a different element, and a broken ElementName binding fails silently in the live app -
    /// the chart simply paints an empty rectangle. So this loads the markup and looks.
    /// </summary>
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class PlannerPanelWiringTests {
        private Window host;

        [SetUp]
        public void SetUp() {
            WpfHost.Ensure();
            // An off-screen window, because template bindings, the visual tree and layout all
            // need the content to be hosted; 8x8 would squeeze the panel's star-sized rows.
            host = new Window {
                Left = -32000, Top = -32000, Width = 900, Height = 700,
                ShowInTaskbar = false, Opacity = 0, ShowActivated = false,
            };
            host.Show();
            // The stand-in theme, the same keys the render smoke seeds: inside N.I.N.A. these
            // brushes come from the user's theme, and a bare test window has none of them. Left
            // unset, every DynamicResource in the panel comes back unresolved - which is how a
            // trigger that recolours the live panel looks here like a trigger that never fired.
            host.Resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x24));
            host.Resources["SecondaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x38));
            host.Resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6));
            host.Resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9C, 0xAC, 0xC2));
            host.Resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0x5A, 0x6E, 0x8C));
            host.Resources["ButtonForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6));
        }

        [TearDown]
        public void TearDown() {
            host.Close();
            host = null;
        }

        [Test]
        public void TheCalendarHandsTheChartTheSelectedNight() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] {
                PlannerFixture.Night(2026, 11, 24, 691, 0.99),
                PlannerFixture.Night(2026, 11, 25, 692, 0.97),
                PlannerFixture.Night(2026, 11, 26, 694, 0.92),
            });
            panel.Planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            var root = Panel(panel);
            var chart = Only<NightChartControl>(root);

            Assert.That(chart.Row, Is.SameAs(panel.Planner.SelectedNight),
                "the chart must be drawing the night the calendar has selected");

            // And it has to keep following, not just start in the right place.
            panel.Planner.NextNight();
            host.UpdateLayout();
            Assert.That(chart.Row, Is.SameAs(panel.Planner.SelectedNight),
                "changing the night from any direction has to redraw the chart");
        }

        [Test]
        public void ThePanelCarriesACalendarAndNoScrollingNightList() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] { PlannerFixture.Night(2026, 11, 24, 691, 0.99) });
            panel.Planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            var root = Panel(panel);
            var picker = Only<DatePicker>(root);

            Assert.Multiple(() => {
                Assert.That(picker.SelectedDate, Is.EqualTo(panel.Planner.SelectedDateForPicker),
                    "the calendar has to show the night that is highlighted, not the day it opened on");
                Assert.That(Visual<ListBox>(root), Is.Empty,
                    "the night list is gone: the calendar and the chips replaced it");
                Assert.That(PlannerViewGlue.GetChart(picker), Is.SameAs(Only<NightChartControl>(root)),
                    "the picker is what carries the chart wire now the list is gone");
            });
        }

        [Test]
        public void TheHeaderShowsTheSelectedNightsThreeFigures() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] {
                PlannerFixture.Night(2026, 11, 24, 691, 0.99),
                PlannerFixture.Night(2026, 11, 25, 692, 0.97),
            });
            panel.Planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            var root = Panel(panel);
            var row = panel.Planner.SelectedNight;
            var texts = Visual<TextBlock>(root).Select(t => t.Text).ToList();

            Assert.Multiple(() => {
                Assert.That(texts, Does.Contain(row.SunChip), "the sunset-to-sunrise chip is not on the panel");
                Assert.That(texts, Does.Contain(row.DarkChip), "the dark-hours chip is not on the panel");
                Assert.That(texts, Does.Contain(row.MoonChip), "the moon chip is not on the panel");
            });
        }

        /// <summary>
        /// Round 8, T5: the palette is three buttons sitting on one setting. Both halves of
        /// that matter and neither is visible in a unit test of the drawing code - three
        /// buttons that do not reach the shared commands are decoration, and a NightPalette
        /// property that recoloured the panel under Enhanced would leave this panel wearing a
        /// look no other tab shares, which is the reason Enhanced is chart-only.
        /// </summary>
        [Test]
        public void ThePanelOffersThreePalettesAndOnlyNightRecoloursTheChrome() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] { PlannerFixture.Night(2026, 11, 24, 691, 0.99) });

            var root = Panel(panel);
            var paletteButtons = Visual<Button>(root)
                .Where(b => (b.GetBindingExpression(System.Windows.Controls.Primitives.ButtonBase.CommandProperty)
                        ?.ParentBinding?.Path?.Path ?? string.Empty)
                    .EndsWith("SchemeCommand", StringComparison.Ordinal))
                .ToList();

            Assert.Multiple(() => {
                Assert.That(paletteButtons, Has.Count.EqualTo(3), "one button per palette");
                Assert.That(Visual<CheckBox>(Group(root, "DISPLAY PALETTE")), Is.Empty, "a palette has no off");
                Assert.That(root.GetValue(PlannerViewGlue.NightPaletteProperty), Is.EqualTo(ChartScheme.Default),
                    "the panel opens on the theme palette");
                Assert.That(root.Resources.Contains("BackgroundBrush"), Is.False,
                    "and it installs no chrome of its own while it is there");
                Assert.That(Group(root, "DISPLAY PALETTE").Status, Is.EqualTo("palette: N.I.N.A. theme"),
                    "the cluster's own status box spells out the scheme in effect in the words the "
                    + "options page and the description use, so the outline below is a shortcut rather "
                    + "than the only answer");
                Assert.That(paletteButtons.Select(b => b.Content), Is.EquivalentTo(new[] { "default", "night", "enhanced" }),
                    "the buttons themselves keep the short forms - 86 DIP does not hold \"N.I.N.A. theme\" - "
                    + "and their tooltips give the full names, so there is one name per palette with the "
                    + "abbreviation pointing back at it");
                Assert.That(paletteButtons.Select(OutlineOf).Count(Outlined), Is.EqualTo(1),
                    "exactly one palette button is outlined, and it is the one in effect");
                Assert.That(Outlined(OutlineOf(paletteButtons.Single(b => (b.Content as string) == "default"))), Is.True,
                    "which at the start is the theme palette");
            });

            Press(paletteButtons, "night");
            Assert.Multiple(() => {
                Assert.That(panel.Planner.Scheme, Is.EqualTo(ChartScheme.Night), "the button drives the shared setting");
                Assert.That(root.GetValue(PlannerViewGlue.NightPaletteProperty), Is.EqualTo(ChartScheme.Night),
                    "the panel root follows the setting, rather than a copy of it");
                Assert.That(root.Resources.Contains("BackgroundBrush"), Is.True,
                    "night puts the red palette in this panel's own resources");
                Assert.That(Visual<TextBlock>(root).Select(t => t.Text), Does.Contain("palette: Night vision"),
                    "and the readout beside the buttons says so");
                Assert.That(Outlined(OutlineOf(paletteButtons.Single(b => (b.Content as string) == "night"))), Is.True,
                    "the outline follows the scheme: it moved off `default` and onto `night`");
                Assert.That(paletteButtons.Select(OutlineOf).Count(Outlined), Is.EqualTo(1),
                    "and only ever one button wears it");
            });

            Press(paletteButtons, "enhanced");
            Assert.Multiple(() => {
                Assert.That(panel.Planner.Scheme, Is.EqualTo(ChartScheme.Enhanced));
                Assert.That(root.Resources.Contains("BackgroundBrush"), Is.False,
                    "Enhanced is a chart-only palette: the panel keeps the user's N.I.N.A. colours");
            });

            Press(paletteButtons, "default");
            Assert.That(panel.Planner.Scheme, Is.EqualTo(ChartScheme.Default), "and the theme palette comes back");
        }

        [Test]
        public void ThePanelSaysWhenItIsWorkingOutNights() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(),
                new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            var root = Panel(panel);

            var computing = Visual<TextBlock>(root).Where(t => t.Text == "computing...").ToList();
            Assert.That(computing, Has.Count.EqualTo(1),
                "one small readout, on the status row where the rest of the panel's news goes");

            var trigger = computing[0].Style.Triggers.OfType<DataTrigger>().Single();
            var binding = (System.Windows.Data.Binding)trigger.Binding;
            Assert.Multiple(() => {
                Assert.That(binding.Path.Path, Is.EqualTo("Planner.Busy"),
                    "it follows the flag the compute path puts up while nights are being worked out");
                Assert.That(trigger.Value, Is.EqualTo("True"),
                    "compared against the string True, which is how a DataTrigger matches a bool binding");
                Assert.That(computing[0].Visibility, Is.EqualTo(Visibility.Collapsed),
                    "and while nothing is running it takes up no room, because a trigger hides it rather than an empty string");
            });
        }

        [Test]
        public void ThePanelHasNoRefreshButtonBecauseItAsksForItsOwnNights() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(),
                new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            var root = Panel(panel);

            var labels = Visual<Button>(root).Select(b => b.Content as string).ToList();
            Assert.Multiple(() => {
                Assert.That(labels, Does.Not.Contain("Refresh"),
                    "the button is gone: the panel widens its own window when the calendar nears an edge");
                Assert.That(typeof(PlannerDockableVM).GetProperty("RefreshCommand"), Is.Null,
                    "and the command went with it, so nothing can quietly bind to it again");
                Assert.That(typeof(NightPlannerVM).GetMethod("RequestRecompute",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public), Is.Null,
                    "recompute is the view model's decision now, not something a view can press");
            });
        }

        /// <summary>
        /// The panel's sizing grammar, measured on the panel N.I.N.A. actually builds. Round 9 made
        /// every pressable thing the same height; round 10 raised that to 32 DIP and found that a
        /// shared *minimum* width does not survive contact with the chrome - MinWidth is a floor, so
        /// word buttons grew past it with their labels while symbol buttons stayed put, and the
        /// review came back with "symbols &lt; &gt; + - used as the size of the button instead of
        /// design we agreed". Round 11 fixes it the other way round: explicit widths in two classes,
        /// 48 DIP modifier and 126 DIP central control, plus one exception for the three-way palette
        /// where no button is a modifier of another. Every number here is asserted on ActualWidth and
        /// ActualHeight after a real layout pass, because an attribute the chrome then ignores is
        /// exactly the failure that was visible on screen - and the text inside each button is
        /// measured too, since a fixed width that clips its own label is not a design, it is a bug
        /// of a different colour.
        /// </summary>
        [Test]
        public void ThePrimaryHeaderButtonsAreTheAgreedSizeAndSayWhatTheyDo() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] { PlannerFixture.Night(2026, 11, 24, 691, 0.99) });
            panel.Planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            var root = Panel(panel);
            // The stock DatePicker brings its own popup button in its template, and that one is
            // N.I.N.A.'s business, not this panel's - so it is excluded before anything is
            // counted or measured.
            var buttons = Visual<ControlGroup>(root).SelectMany(Visual<Button>)
                .Except(Visual<Button>(Only<DatePicker>(root)))
                .ToList();

            // Two night arrows, Tonight, three scrub buttons, three zoom buttons (`-`, `reset
            // zoom`, `+`), three palettes.
            Assert.That(buttons, Has.Count.EqualTo(12));
            Assert.Multiple(() => {
                Assert.That(buttons.Select(b => b.ActualHeight), Is.All.EqualTo(32.0),
                    "every button renders exactly 32 DIP tall, whatever is written in it");
                Assert.That(buttons.Select(b => b.FontSize), Is.All.EqualTo(13.0),
                    "one text size across the panel, the size the design settled on");
                Assert.That(buttons.Select(b => b.FontWeight),
                    Is.All.EqualTo(System.Windows.FontWeights.SemiBold));
                Assert.That(buttons.All(b => !string.IsNullOrWhiteSpace(b.ToolTip as string)), Is.True,
                    "tooltip on every control, including the symbols");
                Assert.That(buttons.Select(b => b.Style), Is.All.Null,
                    "the size is carried by attributes: a Style on a Button replaces N.I.N.A.'s "
                    + "implicit template and the button loses its chrome");
            });

            // The two classes, and the one cluster that has no modifier relationship: the palette.
            var modifiers = new[] { "\u2039", "\u203A", "-", "+", "< hr", "hr >" };
            var central = new[] { "Tonight", "reset zoom", "off" };
            var trio = new[] { "default", "night", "enhanced" };
            Assert.Multiple(() => {
                Assert.That(buttons.Count(b => modifiers.Contains(b.Content as string)), Is.EqualTo(6),
                    "both night arrows, both zoom symbols, and both hour buttons are the modifier class");
                Assert.That(buttons.Count(b => central.Contains(b.Content as string)), Is.EqualTo(3),
                    "and Tonight, reset zoom and the scrub on/off button are the central control of their cluster");
                Assert.That(buttons.Count(b => trio.Contains(b.Content as string)), Is.EqualTo(3));
                Assert.That(buttons.Where(b => modifiers.Contains(b.Content as string)).Select(b => b.ActualWidth).Distinct(),
                    Is.EquivalentTo(new[] { 48.0 }), "the modifier class renders 48 DIP wide, in every cluster it appears in");
                Assert.That(buttons.Where(b => central.Contains(b.Content as string)).Select(b => b.ActualWidth).Distinct(),
                    Is.EquivalentTo(new[] { 126.0 }), "and the central control 126 DIP, whether it says `off` or `reset zoom`");
                Assert.That(buttons.Where(b => trio.Contains(b.Content as string)).Select(b => b.ActualWidth).Distinct(),
                    Is.EquivalentTo(new[] { 86.0 }), "the palette trio is equal to itself");
                Assert.That(Only<DatePicker>(root).ActualWidth, Is.EqualTo(126.0),
                    "the calendar is a central control too - the stock control takes the width as an attribute, so it keeps N.I.N.A.'s chrome");
                Assert.That(Only<DatePicker>(root).ActualHeight, Is.EqualTo(32.0),
                    "and it sits on the same line as the buttons beside it");
            });

            // A fixed width that the label overflows is invisible in XAML and obvious on screen, so
            // the text is measured inside the button that carries it. 12 DIP is the button's own
            // padding, which the chrome adds to the text either side.
            foreach (var button in buttons) {
                var label = Visual<TextBlock>(button).SingleOrDefault(t => (string)t.Text == (string)button.Content);
                Assert.That(label, Is.Not.Null, $"the {button.Content} button has no label of its own to measure");
                Assert.That(label.DesiredSize.Width + 12.0, Is.LessThanOrEqualTo(button.ActualWidth),
                    $"`{button.Content}` does not fit the box the design gives it");
            }

            // The two non-button controls the panel asks you to use are labelled too.
            Assert.Multiple(() => {
                Assert.That(Only<DatePicker>(root).ToolTip, Is.Not.Null.And.Not.Empty);
                Assert.That(Only<NightChartControl>(root).ToolTip, Is.Not.Null.And.Not.Empty,
                    "the chart is a control you press too, and it is where Shift + wheel applies");
            });
        }

        /// <summary>
        /// Pull the palette button the markup wired to a command, and run that command.
        /// Running the binding rather than synthesising a mouse click is deliberate: what the
        /// panel template can get wrong is which lever the button pulls, and a synthetic
        /// click would be testing WPF's input routing instead.
        /// </summary>
        private void Press(IEnumerable<Button> buttons, string label) {
            var button = buttons.Single(b => string.Equals(b.Content as string, label, StringComparison.Ordinal));
            var command = (System.Windows.Input.ICommand)button.GetValue(
                System.Windows.Controls.Primitives.ButtonBase.CommandProperty);
            Assert.That(command, Is.Not.Null, $"the {label} palette button is not bound to a command");
            command.Execute(null);
            host.UpdateLayout();
        }

        /// <summary>
        /// Round 10: `reset zoom`, sitting between `-` and `+`. Seven presses of `-` to get home
        /// from a week is a chore rather than a reset, so one button does it. It runs through the
        /// button's own binding, because what the markup can get wrong is which lever a button
        /// pulls, and the enabled state is checked too: the button shares Planner.CanZoomIn with
        /// `-`, and in a dockable pane that is a binding rather than a command's CanExecute.
        /// </summary>
        [Test]
        public void ResetZoomGoesStraightBackToOneNight() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] {
                PlannerFixture.Night(2026, 11, 22, 686, 0.99),
                PlannerFixture.Night(2026, 11, 23, 688, 0.98),
                PlannerFixture.Night(2026, 11, 24, 691, 0.99),
                PlannerFixture.Night(2026, 11, 25, 692, 0.97),
                PlannerFixture.Night(2026, 11, 26, 694, 0.92),
                PlannerFixture.Night(2026, 11, 27, 697, 0.88),
                PlannerFixture.Night(2026, 11, 28, 701, 0.80),
            });
            panel.Planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            panel.Planner.SetZoom(5);

            var root = Panel(panel);
            var buttons = Visual<Button>(root).Except(Visual<Button>(Only<DatePicker>(root))).ToList();
            var reset = buttons.Single(b => (b.Content as string) == "reset zoom");
            var minus = buttons.Single(b => (b.Content as string) == "-");
            Assert.Multiple(() => {
                Assert.That(reset.IsEnabled, Is.True, "at five nights there is something left to shrink");
                Assert.That(reset.IsEnabled, Is.EqualTo(minus.IsEnabled), "and it is live exactly when `-` is");
            });

            Press(buttons, "reset zoom");
            Assert.Multiple(() => {
                Assert.That(panel.Planner.ZoomNights, Is.EqualTo(1), "one press, one night, from five");
                Assert.That(Visual<TextBlock>(root).Select(t => t.Text), Does.Contain("1 night"),
                    "the readout beside the group follows, as it does for `-` and `+`");
                Assert.That(reset.IsEnabled, Is.False, "at one night it greys itself instead of doing nothing");
            });
        }

        /// <summary>
        /// Round 11's grammar: four labelled clusters side by side, each with its readout directly
        /// below its own buttons rather than scattered across the header. The review asked for
        /// "a reusable, symmetrical control-group template" because of "the inconsistent sizing,
        /// alignment, spacing and relationship between controls and their associated status text",
        /// so this test is mostly about the relationship: which readout belongs to which cluster,
        /// and that the readout is the same shape in every cluster. The geometry of the old round-10
        /// header still matters - the night figures still share a line with the status readout, and
        /// the chart still gets the rest of the panel - so those travel here.
        /// </summary>
        [Test]
        public void EachClusterCarriesItsOwnReadoutBelowItsOwnButtons() {
            var panel = new PlannerDockableVM(ProfilesWithNoSite(), new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.ShowWindow(new[] { PlannerFixture.Night(2026, 11, 24, 691, 0.99) });
            panel.Planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            panel.Planner.SetZoom(3);
            panel.Planner.SetScrub(20 * 60);

            var root = Panel(panel);
            var clusters = Visual<ControlGroup>(root).ToList();
            Assert.That(clusters, Has.Count.EqualTo(4), "one cluster per thing the header can change");
            var existingRed = Visual<TextBlock>(root)
                .Single(t => TextSource(t) == "Planner.StatusText").Foreground;

            Assert.Multiple(() => {
                Assert.That(clusters.Select(g => (string)g.Header),
                    Is.EquivalentTo(new[] { "DATE", "ZOOM", "TIME SCRUB", "DISPLAY PALETTE" }),
                    "and each is captioned, so the arrows say what they step through");
                Assert.That(clusters.Select(StatusBox).Select(b => b.ActualHeight).Distinct(),
                    Is.EquivalentTo(new[] { 26.0 }), "every readout box is the same 26 DIP tall");
                Assert.That(clusters.Select(StatusBox).Select(b => b.BorderThickness.Left).Distinct(),
                    Is.EquivalentTo(new[] { 1.0 }), "with the same thin outline, on all four");
                Assert.That(clusters.Select(StatusBox).SelectMany(b => Visual<TextBlock>(b))
                        .Select(t => t.Foreground).Distinct(),
                    Is.EquivalentTo(new[] { existingRed }),
                    "and their text is the colour of the panel's existing status line, which is the red "
                    + "the brief asks for - not a colour this plugin invented");
            });

            // The pairing itself, on the resolved text rather than a binding path: a readout that
            // belongs to the wrong cluster would look perfectly laid out.
            Assert.Multiple(() => {
                Assert.That(Group(root, "ZOOM").Status, Is.EqualTo("3 nights"),
                    "the zoom box answers the zoom buttons");
                Assert.That(Group(root, "DATE").Status, Is.EqualTo(panel.Planner.SelectedDateLabel),
                    "the date box answers the calendar and the arrows");
                Assert.That(Group(root, "TIME SCRUB").Status, Is.EqualTo(panel.Planner.ScrubLabel),
                    "the scrub box answers the hour buttons");
                Assert.That(Group(root, "DISPLAY PALETTE").Status, Is.EqualTo(panel.Planner.SchemeLabel),
                    "and the palette box answers the palette buttons");
            });

            // Where the readout sits relative to the buttons it belongs to. Asserted on rendered
            // geometry: "directly below" is the part of the request a template attribute cannot show.
            var zoom = Group(root, "ZOOM");
            var zoomBox = StatusBox(zoom);
            Assert.Multiple(() => {
                Assert.That(zoomBox.ActualWidth, Is.EqualTo(zoom.ActualWidth),
                    "the readout box spans the whole cluster, so it reads as part of it");
                Assert.That(zoomBox.TranslatePoint(new System.Windows.Point(0, 0), zoom).Y,
                    Is.GreaterThan(32.0), "and it sits below the buttons, not beside them");
            });

            // The clusters are the wrap unit, so a narrow dock gets 2x2 rather than a clipped
            // `reset zoom`. The 900 DIP host these tests run in is already narrow enough to show it:
            // the four clusters ask for more than a row of that width, so at least one of them is
            // drawn on a second line here, and nothing has been squeezed to get there.
            var strip = (WrapPanel)zoom.Parent;
            var rows = clusters
                .Select(g => Math.Round(g.TranslatePoint(new System.Windows.Point(0, 0), root).Y))
                .Distinct()
                .ToList();
            Assert.Multiple(() => {
                Assert.That(strip.Children, Has.Count.EqualTo(4), "the clusters wrap as whole clusters");
                Assert.That(clusters.Sum(g => g.Width + g.Margin.Left + g.Margin.Right), Is.EqualTo(1140.0),
                    "the four clusters ask for 1140 DIP side by side, which is what the agreed numbers add up to");
                Assert.That(rows, Has.Count.GreaterThanOrEqualTo(2),
                    "and in a panel this width they wrap onto a second line instead of shrinking");
                Assert.That(clusters.Select(g => g.ActualWidth).Max(), Is.AtMost(root.ActualWidth),
                    "no cluster is wider than the panel holding it, which is what a clipped button starts as");
            });

            var figures = new[] { "Planner.SelectedNight.SunChip", "Planner.SelectedNight.DarkChip", "Planner.SelectedNight.MoonChip" };
            var chipBoxes = Visual<Border>(root)
                .Where(b => Visual<TextBlock>(b).Any(t => figures.Contains(TextSource(t))))
                .ToList();
            var status = Visual<TextBlock>(root).Single(t => TextSource(t) == "Planner.StatusText");
            var chipStrip = (FrameworkElement)chipBoxes.Select(b => b.Parent).Distinct().Single();
            Assert.Multiple(() => {
                Assert.That(((Grid)root).RowDefinitions, Has.Count.EqualTo(3),
                    "clusters, figures, chart - the header is one band now, not three rows of loose controls");
                Assert.That(Grid.GetRow(Only<NightChartControl>(root)), Is.EqualTo(2),
                    "and the chart still gets the rest of the panel");
                Assert.That(chipBoxes, Has.Count.EqualTo(3), "the night figures are still three bordered chips");
                Assert.That(chipStrip.Parent, Is.SameAs(status.Parent),
                    "they share their line with the status readout, one line for the night's news");
                Assert.That(status.TextTrimming, Is.EqualTo(System.Windows.TextTrimming.CharacterEllipsis),
                    "and it ellipsises when the panel is narrow, which is what 620 DIP needs");
            });
        }

        /// <summary>The cluster with this caption, as the template built it.</summary>
        private static ControlGroup Group(DependencyObject root, string caption) =>
            Visual<ControlGroup>(root).Single(g => (string)g.Header == caption);

        /// <summary>
        /// The outlined readout box a cluster is built with. Found by its height because it comes
        /// from the cluster's own template, and it is the only box in a cluster at that height.
        /// </summary>
        private static Border StatusBox(ControlGroup cluster) =>
            Visual<Border>(cluster).Single(b => b.ActualHeight == 26.0);

        /// <summary>The Border a palette button is wrapped in: the active-scheme outline.</summary>
        private static Border OutlineOf(Button button) => button.Parent as Border;

        /// <summary>
        /// Whether that outline is drawn. The untouched case is the style's own transparent brush;
        /// a theme brush is the active case, and a null brush - a theme key this host never defined
        /// - counts as drawn too, because what is being checked is that the trigger replaced
        /// "transparent", not that a colour happens to exist to be seen.
        /// </summary>
        private static bool Outlined(Border outline) =>
            outline != null
            && !(outline.BorderBrush is SolidColorBrush paint && paint.Color == Colors.Transparent);

        /// <summary>The path a TextBlock's Text is bound to, or empty when it is a literal.</summary>
        private static string TextSource(TextBlock text) =>
            text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding?.Path?.Path ?? string.Empty;

        /// <summary>Load the real dockable template, bind it, host it, and lay it out.</summary>
        private FrameworkElement Panel(PlannerDockableVM panel) {
            var dictionary = new Zakfo.Unphased.Features.planner.Resources();
            var key = typeof(PlannerDockableVM).FullName + "_Dockable";
            var root = (FrameworkElement)((DataTemplate)dictionary[key]).LoadContent();
            root.DataContext = panel;
            host.Content = root;
            host.UpdateLayout();
            return root;
        }

        private static T Only<T>(DependencyObject root) where T : DependencyObject {
            var found = Visual<T>(root).ToList();
            Assert.That(found, Has.Count.EqualTo(1), $"the panel must contain exactly one {typeof(T).Name}");
            return found[0];
        }

        private static IEnumerable<T> Visual<T>(DependencyObject root) where T : DependencyObject {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++) {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var deeper in Visual<T>(child)) yield return deeper;
            }
        }

        /// <summary>A profile with no astrometry settings, so the panel has no site to compute for.</summary>
        private static IProfileService ProfilesWithNoSite() {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            return profiles.Object;
        }
    }
}
