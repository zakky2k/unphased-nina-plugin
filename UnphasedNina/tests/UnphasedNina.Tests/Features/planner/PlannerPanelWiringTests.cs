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
                Assert.That(Visual<CheckBox>(root), Is.Empty, "the night switch is gone: a palette has no off");
                Assert.That(root.GetValue(PlannerViewGlue.NightPaletteProperty), Is.EqualTo(ChartScheme.Default),
                    "the panel opens on the theme palette");
                Assert.That(root.Resources.Contains("BackgroundBrush"), Is.False,
                    "and it installs no chrome of its own while it is there");
            });

            Press(paletteButtons, "night");
            Assert.Multiple(() => {
                Assert.That(panel.Planner.Scheme, Is.EqualTo(ChartScheme.Night), "the button drives the shared setting");
                Assert.That(root.GetValue(PlannerViewGlue.NightPaletteProperty), Is.EqualTo(ChartScheme.Night),
                    "the panel root follows the setting, rather than a copy of it");
                Assert.That(root.Resources.Contains("BackgroundBrush"), Is.True,
                    "night puts the red palette in this panel's own resources");
                Assert.That(Visual<TextBlock>(root).Select(t => t.Text), Does.Contain("palette: night"),
                    "and the readout beside the buttons says so");
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
