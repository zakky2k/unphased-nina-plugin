// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Moq;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Tests.Features.planner {
    /// <summary>
    /// How far the panel's window of nights reaches, and what happens when the calendar walks
    /// off the end of it. These are the only tests that drive the real calculator on the real
    /// background path, so they are also the only ones that exercise the planner's own thread
    /// marshalling: a computed window arrives through OnUi, which is why the waits below pump a
    /// dispatcher queue instead of sleeping through a budget. Deliberately no Refresh button is
    /// involved anywhere: every window in this file is one the panel asked for itself.
    /// </summary>
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class PlannerWindowGrowthTests {
        /// <summary>
        /// A planner for a real site with its first window worked out. The date is set before
        /// the window is asked for, so the tests are not probing the edges of tonight's own
        /// window and the anchor is a date they can name in an assertion.
        /// </summary>
        private static NightPlannerVM PlannerWithAWindow(out DateTime anchor) {
            anchor = new DateTime(2026, 2, 15);
            var planner = CreatePlanner(anchor);
            Assert.That(WaitFor(() => !planner.Busy && planner.Nights.Count > 0),
                "the panel works a first window out for itself, with nothing pressed");
            return planner;
        }

        private static NightPlannerVM CreatePlanner(DateTime anchor, double latitude = 50.0875) {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            var astro = new Mock<IAstrometrySettings>();
            astro.SetupGet(a => a.Latitude).Returns(latitude);
            astro.SetupGet(a => a.Longitude).Returns(14.4213);
            profile.SetupGet(p => p.AstrometrySettings).Returns(astro.Object);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            var settings = new PluginSettings(profiles.Object);
            settings.Initialize();

            var planner = new NightPlannerVM(settings);
            planner.SelectedDateForPicker = anchor;
            planner.EnsureComputed();
            return planner;
        }

        [Test]
        public void TheFirstWindowIsAnchoredOnTheSelectedNightAndNotItsMiddle() {
            var planner = PlannerWithAWindow(out var anchor);

            Assert.Multiple(() => {
                Assert.That(planner.ComputedSite, Is.Not.Null, "a site was found, so the panel could compute");
                Assert.That(planner.Nights, Has.Count.EqualTo(NightPlannerVM.PastNights + NightPlannerVM.FutureNights + 1),
                    "every night of the window is there at a mid-latitude site");
                Assert.That(planner.Nights[0].MorningDay, Is.EqualTo(anchor.AddDays(-NightPlannerVM.PastNights)));
                Assert.That(planner.Nights[planner.Nights.Count - 1].MorningDay,
                    Is.EqualTo(anchor.AddDays(NightPlannerVM.FutureNights)),
                    "the window looks further ahead than behind, because a planner is used to look ahead");
                Assert.That(planner.SelectedNight, Is.Not.Null);
                Assert.That(planner.SelectedNight.MorningDay, Is.EqualTo(anchor),
                    "the night on screen is the night that was asked for, not whichever one sits in the middle");
            });
        }

        [Test]
        public void WalkingOffTheEndOfTheWindowGrowsItRatherThanRunningOut() {
            var planner = PlannerWithAWindow(out var anchor);
            int before = planner.Nights.Count;
            var announced = new List<string>();
            planner.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
            var wanted = anchor.AddDays(NightPlannerVM.FutureNights - 1);

            planner.SelectedDate = wanted;

            Assert.Multiple(() => {
                // Busy going up is the proof the request left on its own: nothing was pressed.
                Assert.That(announced, Has.Member(nameof(NightPlannerVM.Busy)),
                    "coming within two nights of the end asks for more nights straight away");
                Assert.That(WaitFor(() => !planner.Busy && planner.Nights.Count > before),
                    "the wider window arrives on the interface thread");
                Assert.That(planner.Nights, Has.Count.EqualTo(before + NightPlannerVM.ExtensionNights));
                Assert.That(planner.Nights[planner.Nights.Count - 1].MorningDay,
                    Is.EqualTo(anchor.AddDays(NightPlannerVM.FutureNights + NightPlannerVM.ExtensionNights)),
                    "the end moved forward by ten nights");
                Assert.That(planner.Nights[0].MorningDay, Is.EqualTo(anchor.AddDays(-NightPlannerVM.PastNights)),
                    "and the other end was left where it was");
                Assert.That(planner.SelectedNight, Is.Not.Null);
                Assert.That(planner.SelectedNight.MorningDay, Is.EqualTo(wanted),
                    "the night being looked at stays selected while the window grows around it");
            });
        }

        [Test]
        public void WalkingOffTheStartOfTheWindowGrowsTheOtherEnd() {
            var planner = PlannerWithAWindow(out var anchor);
            int before = planner.Nights.Count;

            planner.SelectedDate = anchor.AddDays(-NightPlannerVM.PastNights + 1);

            Assert.That(WaitFor(() => !planner.Busy && planner.Nights.Count > before), "the wider window arrives");
            Assert.Multiple(() => {
                Assert.That(planner.Nights[0].MorningDay,
                    Is.EqualTo(anchor.AddDays(-NightPlannerVM.PastNights - NightPlannerVM.ExtensionNights)));
                Assert.That(planner.SelectedNight.MorningDay,
                    Is.EqualTo(anchor.AddDays(-NightPlannerVM.PastNights + 1)));
            });
        }

        [Test]
        public void GrowingTheWindowReusesTheNightsAlreadyWorkedOut() {
            var planner = PlannerWithAWindow(out var anchor);
            // The payload, not the row: rows are re-stitched on purpose, because a wider
            // window changes which nights a frame spans. What must not happen again is the
            // calculation - one night is hundreds of sun and moon samples plus the bisections
            // for sunset, sunrise, each twilight and moonrise.
            var alreadyWorkedOut = planner.Nights.ToDictionary(r => r.MorningDay, r => r.Payload);

            planner.SelectedDate = anchor.AddDays(NightPlannerVM.FutureNights - 1);
            Assert.That(WaitFor(() => !planner.Busy && planner.Nights.Count > alreadyWorkedOut.Count),
                "the wider window arrives");

            var stillThere = planner.Nights.Where(r => alreadyWorkedOut.ContainsKey(r.MorningDay)).ToList();
            Assert.That(stillThere, Has.Count.EqualTo(alreadyWorkedOut.Count), "no night was dropped");
            Assert.That(stillThere.All(r => ReferenceEquals(r.Payload, alreadyWorkedOut[r.MorningDay])),
                "every night held before is the very same object: extending costs the new nights only");
        }

        [Test]
        public void ADateFarOutsideTheWindowReanchorsItOnTheNightThatWasPicked() {
            var planner = PlannerWithAWindow(out var anchor);
            int before = planner.Nights.Count;
            var wanted = anchor.AddDays(60);

            planner.SelectedDateForPicker = wanted;

            Assert.That(planner.Busy, Is.True);
            Assert.That(planner.SelectedDateForPicker, Is.EqualTo(wanted));
            Assert.That(planner.SelectedNight, Is.Null);

            Assert.That(WaitFor(() => !planner.Busy && planner.SelectedNight != null
                    && planner.SelectedNight.MorningDay == wanted),
                "the re-anchored window arrives holding the night that was picked");
            Assert.Multiple(() => {
                Assert.That(planner.SelectedDate.Date, Is.EqualTo(wanted));
                Assert.That(planner.Nights, Has.Count.EqualTo(before),
                    "a date too far away re-anchors the window instead of growing it by ten");
            });
        }

        [Test]
        public void ARecomputeKeepsTheLatestDateAndZoom() {
            var planner = PlannerWithAWindow(out var anchor);
            var wanted = anchor.AddDays(61);
            planner.SelectedDateForPicker = anchor.AddDays(-60);
            planner.SelectedDateForPicker = anchor.AddDays(60);
            planner.SelectedDateForPicker = wanted;
            planner.SetZoom(3);

            Assert.That(WaitFor(() => !planner.Busy), "the latest window arrives");
            Assert.That(planner.SelectedDateForPicker, Is.EqualTo(wanted));
            Assert.That(planner.SelectedNight?.MorningDay, Is.EqualTo(wanted));
            Assert.That(planner.SelectedNight?.Chart.ZoomNights, Is.EqualTo(3));
        }

        [Test]
        public void ADateWithoutANightStaysInThePickerAfterCompute() {
            var wanted = new DateTime(2026, 6, 21);
            var planner = CreatePlanner(wanted, latitude: 89);

            Assert.That(WaitFor(() => !planner.Busy), "the polar window finishes loading");
            Assert.That(planner.SelectedDateForPicker, Is.EqualTo(wanted));
            Assert.That(planner.SelectedNight, Is.Null);
            Assert.That(planner.StatusText, Is.EqualTo("No night is available for the selected date."));
        }

        [TestCase(3, 24)]
        [TestCase(10, 20)]
        public void TheWindowGrowsAtTheSameCalendarDistanceInSpringAndAutumn(int month, int day) {
            var anchor = new DateTime(2026, month, day);
            var planner = CreatePlanner(anchor);
            Assert.That(WaitFor(() => !planner.Busy), "the first window arrives");
            var last = anchor.AddDays(NightPlannerVM.FutureNights);

            planner.SelectedDateForPicker = last.AddDays(-NightPlannerVM.EdgeNights - 1);
            Assert.That(planner.Busy, Is.False);
            planner.SelectedDateForPicker = last.AddDays(-NightPlannerVM.EdgeNights);
            Assert.That(planner.Busy, Is.True);
            Assert.That(WaitFor(() => !planner.Busy), "the extended window arrives");
            Assert.That(planner.Nights[^1].MorningDay, Is.EqualTo(last.AddDays(NightPlannerVM.ExtensionNights)));
        }

        [Test]
        public void AnEmptyPanelWithASiteStillFillsItselfWhenAsked() {
            // The panel's own kick-off: the glue asks for a window when the panel loads, and
            // twice in a row must not queue two computations.
            var planner = PlannerWithAWindow(out _);
            planner.EnsureComputed();
            Assert.That(WaitFor(() => !planner.Busy), "nothing was left running");
            Assert.That(planner.SiteDetails, Does.StartWith("Observatory "), "the date tooltip identifies the site being used");
            Assert.That(planner.StatusText, Is.Empty, "a successful window load needs no visible status notice");
        }

        /// <summary>
        /// Wait for a background compute to arrive. A computed window is applied through OnUi,
        /// which marshals onto the interface thread - and in a test the interface thread is this
        /// one, otherwise sitting in a loop. So the loop pumps its own dispatcher queue the way a
        /// real N.I.N.A. interface thread does, instead of sleeping past the budget while the
        /// callback waits for a thread that is not listening.
        /// </summary>
        private static bool WaitFor(Func<bool> ready, int budgetMs = 20000) {
            var ui = Dispatcher.CurrentDispatcher;
            var deadline = Environment.TickCount64 + budgetMs;
            while (Environment.TickCount64 < deadline) {
                if (ready()) return true;
                var frame = new DispatcherFrame();
                ui.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            return ready();
        }
    }
}
