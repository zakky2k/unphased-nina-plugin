using NUnit.Framework;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// The arrow keys drive the scrub from anywhere in the panel.
    /// <para>
    /// They used to be handled on the chart in OnKeyDown, a bubbling event, so they only
    /// worked while the chart held keyboard focus - which only a click on the chart ever
    /// gave it. Worse, Up/Down were not handled at all, so WPF's directional navigation
    /// walked the focus out of the chart into the night list, and from then on Left/Right
    /// selected rows instead of scrubbing, with no obvious way back except clicking the
    /// chart again. Round 7 user item 4: "the up/down button only selecting the night
    /// listing row, but not for the chart scrub".
    /// </para>
    /// <para>
    /// The fix is a PreviewKeyDown handler on the panel root, so these tests raise the real
    /// routed event on the root and assert both the effect on the view model and that the
    /// event was consumed. A KeyEventArgs cannot be built without a PresentationSource, so
    /// the panel is hosted in an invisible window parked off the screen: nothing is shown to
    /// the user, and the suite still never creates - or shuts down - the one Application
    /// CompositionTests is allowed to own.
    /// </para>
    /// </summary>
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class PlannerKeyboardTests {
        private Window host;
        private PresentationSource inputSource;

        [SetUp]
        public void HostThePanelOffScreen() {
            host = new Window {
                Title = "Unphased planner keyboard test",
                Left = -32000,
                Top = -32000,
                Width = 8,
                Height = 8,
                ShowInTaskbar = false,
                Opacity = 0,
                ShowActivated = false,
            };
            host.Show();
            // The input source an input event needs only exists once the window has its HWND,
            // and Show() has just created that. HwndSource is the source a real key press
            // would arrive through, so nothing here is faked.
            inputSource =
                PresentationSource.FromVisual(host)
                ?? HwndSource.FromHwnd(new WindowInteropHelper(host).Handle);
            Assert.That(inputSource, Is.Not.Null, "the off-screen host needs an input source for key events");
        }

        [TearDown]
        public void CloseTheHost() {
            host?.Close();
            host = null;
        }

        /// <summary>A panel root with the keyboard scope wired the way the template wires it.</summary>
        private Grid Panel(NightPlannerVM planner) {
            var root = new Grid();
            PlannerViewGlue.SetKeyboardScope(root, true);
            root.DataContext = planner;
            host.Content = root;
            return root;
        }

        /// <summary>
        /// Raise a key the way the live panel receives it: through a real input source,
        /// aimed at <paramref name="target"/>, the control the key was really pressed in.
        /// </summary>
        private bool PressKey(Grid root, Visual target, Key key) {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, key) {
                RoutedEvent = UIElement.PreviewKeyDownEvent,
                Source = target,
            };
            root.RaiseEvent(args);
            return args.Handled;
        }

        [Test]
        public void LeftAndRightStepTheScrubWithoutTheChartHoldingFocus() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SetScrub(12 * 60);
            var root = Panel(planner);

            Assert.Multiple(() => {
                Assert.That(PressKey(root, root, Key.Right), Is.True, "Right should be consumed");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(13 * 60));
                Assert.That(PressKey(root, root, Key.Left), Is.True, "Left should be consumed");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(12 * 60));
            });
        }

        [Test]
        public void UpAndDownChangeTheNightInsteadOfMovingTheFocus() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 25, 0, 0);
            planner.SetScrub(20 * 60);
            var root = Panel(planner);

            PressKey(root, root, Key.Down);
            var morning = planner.SelectedNight.MorningDay;
            PressKey(root, root, Key.Up);

            // Consumed, so WPF never gets the chance to walk the focus to another control,
            // and the scrub stays where it was while the night under it changes. The
            // anchor date names the morning side, so Down lands on the 25/26 night.
            Assert.Multiple(() => {
                Assert.That(morning, Is.EqualTo(new DateTime(2026, 11, 26)));
                Assert.That(planner.SelectedNight.MorningDay, Is.EqualTo(new DateTime(2026, 11, 25)));
                Assert.That(planner.ScrubMinutes, Is.EqualTo(20 * 60));
            });
        }

        [Test]
        public void HomeEndAndEscapeMoveTheMarkerToTheFrameEdges() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SetScrub(14 * 60);
            var root = Panel(planner);

            PressKey(root, root, Key.End);
            var atEnd = planner.ScrubMinutes;
            PressKey(root, root, Key.Home);
            var atStart = planner.ScrubMinutes;
            planner.SetScrub(14 * 60);
            PressKey(root, root, Key.Escape);

            Assert.Multiple(() => {
                Assert.That(atEnd, Is.EqualTo(planner.VisibleSpanMinutes));
                Assert.That(atStart, Is.EqualTo(0));
                Assert.That(planner.ScrubMinutes, Is.Null);
            });
        }

        [Test]
        public void AKeyAimedAtATextFieldKeepsItsArrowsForTheCaret() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SetScrub(12 * 60);
            var root = Panel(planner);
            // The date picker's editable part is a TextBox, and that is the one place in
            // the panel where Left/Right/Up/Down belong to something else.
            var field = new TextBox { Text = "24/11/2026" };
            root.Children.Add(field);

            var handled = PressKey(root, field, Key.Right);

            Assert.Multiple(() => {
                Assert.That(handled, Is.False, "the field must still see the key");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(12 * 60), "and the scrub must not move");
            });
        }

        [Test]
        public void APanelWithNoNightsYetIgnoresTheKeys() {
            var planner = PlannerFixture.Planner();
            var root = Panel(planner);

            Assert.That(PressKey(root, root, Key.Left), Is.False);
        }

        [Test]
        public void TheChartCanBeReachedFromTheKeyboard() {
            var chart = new NightChartControl();

            // Focusable is what the arrow-key handling needs, the attached IsTabStop is what
            // lets Tab reach it, and DirectionalNavigation=None keeps Up/Down from being
            // spent walking focus out of the chart before the panel ever sees them.
            Assert.Multiple(() => {
                Assert.That(chart.Focusable, Is.True);
                Assert.That(KeyboardNavigation.GetIsTabStop(chart), Is.True);
                Assert.That(
                    KeyboardNavigation.GetDirectionalNavigation(chart),
                    Is.EqualTo(KeyboardNavigationMode.None));
            });
        }
    }
}
