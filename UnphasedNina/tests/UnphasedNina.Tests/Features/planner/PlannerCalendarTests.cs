using NUnit.Framework;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// The header the calendar replaced: one night on screen instead of a scrolling list, so
    /// the night's own figures have to be spelled out under it. Both halves are view-model
    /// work, so none of this needs WPF - the panel test that loads the real markup lives in
    /// PlannerPanelWiringTests.
    /// </summary>
    [NonParallelizable]
    public class PlannerCalendarTests {

        // ---- The date picker's face of the selection ---------------------------------------

        [Test]
        public void ThePickerAndTheArrowKeysDriveTheSameNight() {
            var planner = PlannerFixture.WindowOfThreeNights();

            // The fixture's window is three nights whose mornings are 25/26/27 Nov, and it
            // ends anchored on 26 Nov, so every move below has to be off that date to be a
            // move at all.
            planner.SelectedDateForPicker = new DateTime(2026, 11, 25);

            Assert.That(planner.SelectedNight, Is.Not.Null);
            Assert.That(planner.SelectedNight.MorningDay, Is.EqualTo(new DateTime(2026, 11, 25)));

            planner.NextNight();
            Assert.That(planner.SelectedDateForPicker, Is.EqualTo(new DateTime(2026, 11, 26)),
                "the picker has to show the night the arrows moved to, or it lies about what is highlighted");

            planner.PrevNight();
            Assert.That(planner.SelectedDateForPicker, Is.EqualTo(new DateTime(2026, 11, 25)));

            // One night further back lands outside the three-night window, so the middle
            // night stands in - and the picker has to show that night rather than the date
            // the arrows walked to, which is the whole point of it being a proxy.
            planner.PrevNight();
            Assert.That(planner.SelectedDateForPicker, Is.EqualTo(planner.SelectedNight.MorningDay),
                "the picker shows the night that is actually highlighted, not where the keys wandered to");
        }

        [Test]
        public void EveryWayTheDateMovesAnnouncesThePickersProperty() {
            var planner = PlannerFixture.WindowOfThreeNights();
            var announced = new List<string>();
            planner.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            planner.SelectedDate = PlannerFixture.Local(2026, 11, 25, 0, 0);

            Assert.That(announced, Does.Contain(nameof(NightPlannerVM.SelectedDateForPicker)),
                "a date changed by the buttons has to reach the picker that displays it");

            announced.Clear();
            planner.SelectedNight = planner.Nights[2];
            Assert.That(announced, Does.Contain(nameof(NightPlannerVM.SelectedDateForPicker)),
                "and so does a night selected from the chart side");
        }

        [Test]
        public void AnEmptyPickerSelectionIsRefusedRatherThanSelectingNothing() {
            var planner = PlannerFixture.WindowOfThreeNights();
            var before = planner.SelectedDate;

            planner.SelectedDateForPicker = null;

            Assert.That(planner.SelectedDate, Is.EqualTo(before));
        }

        // ---- The three chips ----------------------------------------------------------------

        [Test]
        public void TheChipsSayWhenTheNightRunsAndHowMuchOfItIsDark() {
            var row = new NightRowVM(PlannerFixture.Night(2026, 11, 24, 691, 0.99));

            Assert.Multiple(() => {
                Assert.That(row.SunChip, Is.EqualTo("16:20 \u2192 07:30"));
                // 691 minutes reads as a length of time, not as a number of minutes: the list
                // used to fit "691 min dark" in one scrolling row, a header chip can afford to
                // be read.
                Assert.That(row.DarkChip, Is.EqualTo("11 h 31 m dark"));
            });
        }

        [Test]
        public void TheMoonChipNamesTheHighestMomentInsideTheNight() {
            var row = new NightRowVM(PlannerFixture.Night(2026, 11, 24, 691, 0.99, new[] {
                // The sample grid runs past the night on both sides, exactly as the real
                // payload's does, and the highest points on the frame are the ones outside it.
                PlannerFixture.Moon(2026, 11, 24, 15, 0, 62),
                PlannerFixture.Moon(2026, 11, 24, 20, 0, 22.4),
                PlannerFixture.Moon(2026, 11, 24, 22, 30, 41.4),
                PlannerFixture.Moon(2026, 11, 25, 3, 0, -5),
                PlannerFixture.Moon(2026, 11, 25, 9, 0, 71),
            }));

            Assert.That(row.MoonChip, Is.EqualTo("moon highest 41\u00b0 at 22:30"));
        }

        [Test]
        public void ANightWithTheMoonBelowTheHorizonSaysSoRatherThanNamingAMoment() {
            var dark = new NightRowVM(PlannerFixture.Night(2026, 11, 24, 691, 0.0));
            var lowMoon = new NightRowVM(PlannerFixture.Night(2026, 11, 24, 691, 0.4, new[] {
                PlannerFixture.Moon(2026, 11, 24, 22, 0, -3),
                PlannerFixture.Moon(2026, 11, 25, 4, 0, -1),
            }));

            Assert.Multiple(() => {
                Assert.That(dark.MoonChip, Is.EqualTo("moon down all night"));
                Assert.That(lowMoon.MoonChip, Is.EqualTo("moon down all night"));
            });
        }

        [Test]
        public void WideningTheChartDoesNotChangeWhatTheChipsClaim() {
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.SelectedDate = PlannerFixture.Local(2026, 11, 25, 0, 0);
            var sun = planner.SelectedNight.SunChip;
            var dark = planner.SelectedNight.DarkChip;
            var moon = planner.SelectedNight.MoonChip;

            planner.SetZoom(3);

            // The stitched payload carries three nights of moon samples and a longer sun
            // series; the chips still describe the night that was picked, which is what makes
            // them comparable while the frame is a week wide.
            Assert.Multiple(() => {
                Assert.That(planner.SelectedNight.Payload.MoonSamples, Is.Not.Empty);
                Assert.That(planner.SelectedNight.SunChip, Is.EqualTo(sun));
                Assert.That(planner.SelectedNight.DarkChip, Is.EqualTo(dark));
                Assert.That(planner.SelectedNight.MoonChip, Is.EqualTo(moon));
            });
        }
    }
}
