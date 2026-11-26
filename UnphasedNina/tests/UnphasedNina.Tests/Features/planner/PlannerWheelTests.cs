using NUnit.Framework;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// What the wheel does while it is over the chart.
    /// <para>
    /// It used to scrub on every wheel message, whatever the keys were doing - and the chart is
    /// the tallest thing in the panel, so "anywhere the user would naturally put the pointer"
    /// meant the panel could not be scrolled at all: the wheel moved the hour marker instead of
    /// the page, and a wheel gesture aimed at scrolling silently changed the reading. Round 9's
    /// decision, asked and answered: Shift + wheel scrubs, the bare wheel is handed back to
    /// N.I.N.A.'s scroll viewer so the panel scrolls like every other pane.
    /// </para>
    /// <para>
    /// The modifier rule itself is decided in <see cref="NightChartControl.TakeWheelScrub"/>,
    /// which takes the modifier keys as a value, because <see cref="Keyboard.Modifiers"/> reads
    /// the live keyboard and a test cannot hold Shift down for it. The routed-event tests below
    /// use the real, unmodified keyboard state, which is exactly the case that has to stay
    /// unhandled, and they raise the event on a chart hosted in an off-screen window so the chart
    /// has the layout it would have in the panel.
    /// </para>
    /// </summary>
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class PlannerWheelTests {
        private Window host;

        [SetUp]
        public void HostTheChartOffScreen() {
            WpfHost.Ensure();
            host = new Window {
                Title = "Unphased planner wheel test",
                Left = -32000,
                Top = -32000,
                Width = 8,
                Height = 8,
                ShowInTaskbar = false,
                Opacity = 0,
                ShowActivated = false,
            };
            host.Show();
        }

        [TearDown]
        public void CloseTheHost() {
            host?.Close();
            host = null;
        }

        /// <summary>A chart on a night of a three-night window, hooked to its planner the way the panel hooks it.</summary>
        private NightChartControl ChartOnANight(NightPlannerVM planner) {
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            var chart = new NightChartControl();
            // DataContext, not the attached Chart property: hooking the planner is what makes
            // the chart move the shared scrub value rather than a copy of its own.
            chart.DataContext = planner;
            chart.SetRow(planner.SelectedNight);
            host.Content = chart;
            host.UpdateLayout();
            return chart;
        }

        /// <summary>Send a wheel message the way the live panel receives one.</summary>
        private bool TurnTheWheel(NightChartControl chart, int delta) {
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = chart,
            };
            chart.RaiseEvent(args);
            return args.Handled;
        }

        [Test]
        public void ShiftPlusWheelWalksTheNightOneHourAtATime() {
            var planner = PlannerFixture.WindowOfThreeNights();
            var chart = ChartOnANight(planner);
            planner.SetScrub(14 * 60);

            Assert.Multiple(() => {
                Assert.That(chart.TakeWheelScrub(120, ModifierKeys.Shift), Is.True,
                    "Shift + wheel is the scrub gesture, so it has to report that it took the wheel");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(15 * 60), "one notch up is one hour later");
                Assert.That(chart.TakeWheelScrub(-120, ModifierKeys.Shift), Is.True);
                Assert.That(chart.TakeWheelScrub(-120, ModifierKeys.Shift), Is.True);
                Assert.That(planner.ScrubMinutes, Is.EqualTo(13 * 60), "two notches down is two hours earlier");
            });
        }

        [Test]
        public void TheBareWheelLeavesTheScrubWhereItIs() {
            var planner = PlannerFixture.WindowOfThreeNights();
            var chart = ChartOnANight(planner);
            planner.SetScrub(14 * 60);

            Assert.Multiple(() => {
                Assert.That(chart.TakeWheelScrub(120, ModifierKeys.None), Is.False,
                    "no Shift means the wheel belongs to the page, not the marker");
                Assert.That(chart.TakeWheelScrub(-120, ModifierKeys.None), Is.False);
                Assert.That(chart.TakeWheelScrub(120, ModifierKeys.Control), Is.False,
                    "and no other key combination picks the scrub up either");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(14 * 60), "the reading did not move");
            });
        }

        [Test]
        public void AnOrdinaryWheelOverTheChartIsLeftForThePanelToScroll() {
            var planner = PlannerFixture.WindowOfThreeNights();
            var chart = ChartOnANight(planner);
            planner.SetScrub(14 * 60);

            // The half that cannot be simulated in code - a real Shift press - is covered by the
            // two tests above. What this one pins is the live routing: the event arrives unhandled,
            // so it keeps bubbling out to N.I.N.A.'s scroll viewer instead of dying on the chart.
            Assert.Multiple(() => {
                Assert.That(TurnTheWheel(chart, 120), Is.False,
                    "the chart must not claim a wheel message it is not going to act on");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(14 * 60), "and it must not scrub while it declines");
            });
        }

        [Test]
        public void AChartWithNoNightTakesTheWheelNoDifferently() {
            var chart = new NightChartControl();

            Assert.Multiple(() => {
                Assert.That(chart.TakeWheelScrub(120, ModifierKeys.Shift), Is.False,
                    "with nothing drawn there is nothing to scrub, Shift or not");
                Assert.That(TurnTheWheel(chart, -120), Is.False, "and the panel still gets its scroll");
            });
        }
    }
}
