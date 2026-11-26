using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// The highlighted row, the header caption and the chart must always describe
    /// the same night. Selection is owned by NightPlannerVM, so these tests cover
    /// it without touching WPF.
    /// </summary>
    [NonParallelizable]
    public class PlannerSelectionTests {

        [Test]
        public void NavigatingToANightSelectsTheRowNamedByItsMorning() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            Assert.That(planner.SelectedNight, Is.Not.Null);
            Assert.That(planner.SelectedNight.MorningDay, Is.EqualTo(new DateTime(2026, 11, 26)));
            // The row the user sees highlighted reads 25 Nov (its evening); the
            // header quotes the same night explicitly, so there is no off-by-one.
            Assert.That(planner.SelectedDateLabel, Is.EqualTo(planner.SelectedNight.NightSpanLabel));
            Assert.That(planner.SelectedDateLabel, Does.Contain("Wed 25 Nov"));
            Assert.That(planner.SelectedDateLabel, Does.Contain("Thu 26 Nov 2026"));
        }

        [Test]
        public void ClickingARowMovesTheAnchorDateToThatNight() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            planner.SelectedNight = planner.Nights[0];

            Assert.That(planner.SelectedDate.Date, Is.EqualTo(new DateTime(2026, 11, 25)));
            Assert.That(planner.SelectedDateLabel, Is.EqualTo(planner.Nights[0].NightSpanLabel));
        }

        [Test]
        public void WithNoSiteToAskForMoreTheMiddleOfTheWindowStandsInForADateOutsideIt() {
            // The other half of this behaviour lives in PlannerWindowGrowthTests: with a site
            // the panel asks for a window that really holds the date that was picked. Here
            // there is no site, so nothing can be worked out and the middle of whatever is on
            // screen is the only answer the panel has.
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 12, 30, 0, 0);

            Assert.That(planner.SelectedNight, Is.SameAs(planner.Nights[1]));
        }

        [Test]
        public void SelectingANightRaisesSelectedNightNotification() {
            var planner = PlannerFixture.WindowOfThreeNights();
            var seen = new List<string>();
            planner.PropertyChanged += (_, args) => seen.Add(args.PropertyName);

            planner.SelectedDate = PlannerFixture.Local(2026, 11, 25, 0, 0);

            Assert.That(seen, Does.Contain(nameof(NightPlannerVM.SelectedNight)));
            Assert.That(seen, Does.Contain(nameof(NightPlannerVM.SelectedDateLabel)));
        }

        [Test]
        public void EmptyWindowClearsTheSelectionAndKeepsADateCaption() {
            var planner = PlannerFixture.Planner();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 25, 0, 0);

            Assert.That(planner.SelectedNight, Is.Null);
            Assert.That(planner.SelectedDateLabel, Does.Contain("25 Nov 2026"));
        }

        // ---- Scrub marker -----------------------------------------------------------------

        [Test]
        public void ScrubbingSnapsToWholeHoursAndStepsOneHourAtATime() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);

            planner.SetScrub(613);
            Assert.That(planner.ScrubMinutes, Is.EqualTo(600), "the marker must stop on a whole hour");

            planner.StepScrub(1);
            Assert.That(planner.ScrubMinutes, Is.EqualTo(660));
            planner.StepScrub(-3);
            Assert.That(planner.ScrubMinutes, Is.EqualTo(480));
        }

        [Test]
        public void FirstScrubStepStartsAfterSunsetRatherThanAtTheFrameStart() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            var p = planner.SelectedNight.Payload;
            double sunset = (p.Sunset - p.FrameNoon).TotalMinutes;

            planner.StepScrub(1);

            Assert.That(planner.ScrubMinutes, Is.EqualTo(Math.Round(sunset / 60, MidpointRounding.AwayFromZero) * 60 + 60));
        }

        [Test]
        public void SteppingCannotWalkTheMarkerOffTheVisibleSpan() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            planner.SetZoom(1);

            planner.SetScrub(47 * 60);
            Assert.That(planner.ScrubMinutes, Is.EqualTo(NightChartVM.OneNightMinutes), "clamped to the right edge");

            planner.SetScrub(-2 * 60);
            Assert.That(planner.ScrubMinutes, Is.EqualTo(0), "clamped to the frame start");

            planner.SetZoom(3);
            Assert.That(planner.VisibleSpanMinutes, Is.EqualTo(3 * NightChartVM.OneNightMinutes));
            Assert.That(planner.ScrubMinutes, Is.EqualTo(0));
        }

        [Test]
        public void ZoomWidensTheFrameWithoutMovingTheSelectionOrTheScrub() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            var selected = planner.SelectedNight;
            planner.SetScrub(20 * 60);

            planner.ZoomOut();
            planner.ZoomOut();

            Assert.Multiple(() => {
                Assert.That(planner.ZoomNights, Is.EqualTo(3));
                Assert.That(planner.ZoomLabel, Is.EqualTo("3 nights"));
                Assert.That(planner.VisibleSpanMinutes, Is.EqualTo(3 * NightChartVM.OneNightMinutes));
                Assert.That(planner.SelectedNight.MorningDay, Is.EqualTo(selected.MorningDay), "zoom changed which night is selected");
                Assert.That(planner.SelectedNight.Chart.FrameMinutes, Is.EqualTo(3 * NightChartVM.OneNightMinutes));
                Assert.That(planner.SelectedNight.Payload.SunSeries.Count, Is.GreaterThan(selected.Payload.SunSeries.Count),
                    "the chart frame was not stitched from the following nights");
                Assert.That(planner.SelectedNight.Payload.Sunset, Is.EqualTo(selected.Payload.Sunset),
                    "the row must still describe the night that was picked");
                Assert.That(planner.ScrubMinutes, Is.EqualTo(20 * 60), "zooming must not move the scrub marker");
            });

            planner.ZoomIn();
            Assert.Multiple(() => {
                Assert.That(planner.ZoomNights, Is.EqualTo(2));
                Assert.That(planner.ZoomLabel, Is.EqualTo("2 nights"));
            });

            planner.SetZoom(0);
            Assert.That(planner.ZoomNights, Is.EqualTo(1), "never below one night");
            planner.SetZoom(99);
            Assert.That(planner.ZoomNights, Is.EqualTo(NightChartVM.MaxZoomNights), "never past a week");
        }

        [Test]
        public void ZoomButtonsAreToldWhenTheyHaveReachedTheirEnd() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            planner.SetZoom(1);
            var announced = new List<string>();
            planner.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            planner.ZoomOut();

            Assert.Multiple(() => {
                Assert.That(planner.CanZoomIn, Is.True);
                Assert.That(planner.CanZoomOut, Is.True);
                // The buttons bind their enabled state to these two properties. Left to the
                // commands' own CanExecute the minus button stayed greyed out after the
                // plus had widened the frame - in a dockable pane nothing re-queried it - so
                // the chart could only ever grow.
                Assert.That(announced, Does.Contain(nameof(NightPlannerVM.CanZoomIn)),
                    "leaving the tightest setting has to wake the minus button up");
                Assert.That(announced, Does.Contain(nameof(NightPlannerVM.CanZoomOut)));
            });

            planner.SetZoom(NightChartVM.MaxZoomNights);
            Assert.Multiple(() => {
                Assert.That(planner.CanZoomIn, Is.True);
                Assert.That(planner.CanZoomOut, Is.False, "a week is as wide as the chart goes");
            });

            planner.SetZoom(1);
            Assert.Multiple(() => {
                Assert.That(planner.CanZoomIn, Is.False, "one night is as tight as it gets");
                Assert.That(planner.CanZoomOut, Is.True);
            });
        }

        /// <summary>
        /// The minus button in the panel stayed greyed after the plus had widened the
        /// frame: WPF ANDs a command's CanExecute into the button's enabled state, and
        /// inside a dockable pane nothing reliably re-queries it. So the commands never
        /// refuse and only the IsEnabled binding - which the panel does see change -
        /// decides what the buttons look like.
        /// </summary>
        [Test]
        public void TheZoomCommandsNeverRefuseSoOnlyTheBindingGreysAButton() {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            var panel = new PlannerDockableVM(
                profiles.Object, new Mock<NINA.Equipment.Interfaces.Mediator.ITelescopeMediator>().Object);
            panel.Planner.SetZoom(1);

            Assert.Multiple(() => {
                Assert.That(panel.Planner.CanZoomIn, Is.False, "the planner still knows one night is the end");
                Assert.That(panel.ZoomInCommand.CanExecute(null), Is.True,
                    "the command must not be the thing that greys the button out");
                Assert.That(panel.ZoomOutCommand.CanExecute(null), Is.True);
            });

            panel.ZoomInCommand.Execute(null);
            Assert.That(panel.Planner.ZoomNights, Is.EqualTo(1), "zooming past an end is a no-op, not a crash");

            panel.ZoomOutCommand.Execute(null);
            Assert.Multiple(() => {
                Assert.That(panel.Planner.ZoomNights, Is.EqualTo(2));
                Assert.That(panel.Planner.CanZoomIn, Is.True, "the binding is what unmutes the minus button");
            });
        }

        /// <summary>
        /// A colour picked on the plugin options page has to arrive on the settings object
        /// the panel reads and be announced by the planner, because that announcement is
        /// what makes the chart repaint.
        /// </summary>
        [Test]
        public void AChartColourSetAnywhereReachesThePlannerAndAnnouncesItself() {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            var planner = new NightPlannerVM(settings);
            var announced = new List<string>();
            planner.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            settings.SunColor = "#FFD700";
            settings.MoonColor = "#123456";
            settings.HorizonColor = "ABCDEF";

            Assert.Multiple(() => {
                Assert.That(planner.SunColor, Is.EqualTo("#FFD700"));
                Assert.That(planner.MoonColor, Is.EqualTo("#123456"));
                Assert.That(planner.HorizonColor, Is.EqualTo("#ABCDEF"), "a typed value gains its missing #");
                Assert.That(announced, Has.Member(nameof(NightPlannerVM.SunColor)),
                    "no announcement means no repaint, which is what made picks look ignored");
                Assert.That(announced, Has.Member(nameof(NightPlannerVM.MoonColor)));
                Assert.That(announced, Has.Member(nameof(NightPlannerVM.HorizonColor)));
            });

            settings.ResetColors();
            Assert.That(planner.SunColor, Is.Empty, "clearing a pick follows the scheme again");
        }

        [Test]
        public void TheFirstWindowLooksFurtherAheadThanBehindAndGrowsNearItsEnds() {
            Assert.Multiple(() => {
                // History only needs enough to see how the last fortnight went, while
                // planning wants the month ahead. The forward side also decides how far a
                // zoomed frame near the end of the window can still stitch, which is what
                // the blank space at the right edge used to be. These are the opening reach
                // and not a limit: paging sideways grows the window, which the next two
                // numbers are about.
                Assert.That(NightPlannerVM.PastNights, Is.EqualTo(10));
                Assert.That(NightPlannerVM.FutureNights, Is.GreaterThanOrEqualTo(20));
                Assert.That(NightPlannerVM.FutureNights, Is.GreaterThan(NightPlannerVM.PastNights));
                // Two nights, one either side of the night being looked at, so a frame zoomed
                // out to three nights still has something to draw while the new nights are
                // being worked out in the background.
                Assert.That(NightPlannerVM.EdgeNights, Is.EqualTo(2));
                // Ten at a time: enough that walking a month sideways is a handful of requests,
                // small enough that each one stays a background errand rather than a wait.
                Assert.That(NightPlannerVM.ExtensionNights, Is.EqualTo(10));
            });
        }

        [Test]
        public void TurningTheScrubOffClearsItAndSaysSoInHeader() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            planner.SetScrub(20 * 60);
            Assert.That(planner.ScrubLabel, Does.StartWith("Scrub "));

            planner.ClearScrub();

            Assert.Multiple(() => {
                Assert.That(planner.ScrubMinutes, Is.Null);
                Assert.That(planner.ScrubLabel, Does.Contain("Scrub off"));
            });
        }

        [Test]
        public void ScrubPositionSurvivesANewNightAndItsReadoutFollows() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 26, 0, 0);
            planner.SetScrub(20 * 60);
            var seen = new List<string>();
            planner.PropertyChanged += (_, args) => seen.Add(args.PropertyName);

            planner.SelectedDate = PlannerFixture.Local(2026, 11, 25, 0, 0);

            // +20 h from noon is the same clock hour in every frame, which is what
            // makes two nights comparable - so the marker stays put and only its
            // quoted altitudes change.
            Assert.Multiple(() => {
                Assert.That(planner.ScrubMinutes, Is.EqualTo(20 * 60));
                Assert.That(seen, Does.Contain(nameof(NightPlannerVM.ScrubLabel)));
            });
        }
    }
}
