// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CosineKitty;
using Moq;
using NINA.Astrometry;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using NUnit.Framework;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;
using PlannerResources = Zakfo.Unphased.Features.planner.Resources;

namespace Zakfo.Unphased.Tests.Features.planner {
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class OverlayTests {
        [Test]
        public void PlanetSwitchesStartOffAndStayOnWhenCollapsed() {
            WpfHost.Ensure();
            var resources = new PlannerResources();
            var key = typeof(PlannerDockableVM).FullName + "_Dockable";
            var view = (FrameworkElement)((DataTemplate)resources[key]).LoadContent();
            var planner = PlannerFixture.Planner();
            var list = (Expander)view.FindName("PlanetOverlays");
            var targets = (Expander)view.FindName("TargetOverlays");
            Assert.Multiple(() => {
                Assert.That(list.IsExpanded, Is.False);
                Assert.That(targets.IsExpanded, Is.False);
                Assert.That(planner.Planets.Select(planet => planet.Name),
                    Is.EqualTo(new[] { "Mercury", "Venus", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" }));
                Assert.That(planner.PlanetSelection, Is.EqualTo("0 selected"));
            });
            list.IsExpanded = true;
            planner.Planets.Single(planet => planet.Name == "Jupiter").Enabled = true;
            list.IsExpanded = false;
            Assert.That(planner.PlanetSelection, Is.EqualTo("1 selected"));
        }

        [Test]
        public void FramingCommandSnapshotsTargetsWithoutDuplicatingThemAndKeepsThemPerProfile() {
            WpfHost.Ensure();
            PluginSettings.Shared?.Dispose();
            var profiles = new Mock<IProfileService>();
            IProfile active = Profile();
            var first = active;
            profiles.SetupGet(service => service.ActiveProfile).Returns(() => active);
            var framing = new Mock<IFramingAssistantVM>();
            var panel = new PlannerDockableVM(profiles.Object, Mock.Of<ITelescopeMediator>(), framing.Object);
            using var settings = PluginSettings.Shared;
            var planner = panel.Planner;

            panel.AddFramingTargetCommand.Execute(null);
            Assert.That(planner.OverlayNotice, Does.Contain("Select a target"));

            var coordinates = new Coordinates(5.588, -5.39, Epoch.J2000, Coordinates.RAType.Hours);
            framing.SetupGet(vm => vm.DSO).Returns(new DeepSkyObject("M42", coordinates, null));
            panel.AddFramingTargetCommand.Execute(null);
            var firstChoice = planner.Targets.Single();
            Assert.That(firstChoice.Enabled, Is.True);
            firstChoice.Enabled = false;
            panel.AddFramingTargetCommand.Execute(null);
            Assert.That(planner.Targets.Single(), Is.SameAs(firstChoice));
            Assert.That(firstChoice.Enabled, Is.True);

            var other = new Coordinates(0.71, 41.27, Epoch.J2000, Coordinates.RAType.Hours);
            framing.SetupGet(vm => vm.DSO).Returns(new DeepSkyObject("M31", other, null));
            panel.AddFramingTargetCommand.Execute(null);
            Assert.That(firstChoice.Target.RightAscensionHours, Is.EqualTo(5.588).Within(1e-8));
            Assert.That(planner.Targets.Count, Is.EqualTo(2));

            active = Profile();
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            Assert.That(planner.Targets, Is.Empty);
            active = first;
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            Assert.That(planner.Targets.Select(choice => choice.Target.Id), Is.EqualTo(new[] { "M42", "M31" }));
            Assert.That(planner.TargetSelection, Is.EqualTo("0 selected"));
            planner.RemoveTargetCommand.Execute(planner.Targets[0]);
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            Assert.That(planner.Targets.Single().Target.Id, Is.EqualTo("M31"));
        }

        [Test, Explicit("Requires N.I.N.A.'s External NOVAS, SOFA and JPLEPH files beside the test assembly.")]
        public void OverlayAltitudesAgreeWithIndependentEphemerisAndFollowTheLatestFrame() {
            WpfHost.Ensure();
            var astroTime = new AstroTime(new DateTime(2026, 11, 26, 23, 0, 0, DateTimeKind.Utc));
            foreach (var site in new[] { new Site(50.088, 14.421), new Site(-33.869, 151.209) }) {
                var observer = new Observer(site.Lat, site.Lon, site.Elev);
                foreach (var (planet, body) in new[] { (NOVAS.Body.Mercury, Body.Mercury), (NOVAS.Body.Venus, Body.Venus),
                    (NOVAS.Body.Jupiter, Body.Jupiter), (NOVAS.Body.Saturn, Body.Saturn) }) {
                    var equatorial = Astronomy.Equator(body, astroTime, observer, EquatorEpoch.OfDate, Aberration.Corrected);
                    var expected = Astronomy.Horizon(astroTime, observer, equatorial.ra, equatorial.dec, Refraction.None).altitude;
                    var actual = OverlayEphemeris.PlanetAltitude(planet, site, new DateTimeOffset(astroTime.ToUtcDateTime()));
                    Assert.That(actual, Is.EqualTo(expected).Within(.05), $"{planet} at {site}");
                }
            }
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.AddFramingTarget(new FramingTarget("M42", "Orion Nebula", 5.588, -5.39));
            planner.Planets.Single(planet => planet.Name == "Jupiter").Enabled = true;
            planner.SelectedDate = new DateTime(2026, 11, 26);
            planner.SetZoom(2);
            var row = planner.SelectedNight;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher) {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            var deadline = DateTime.UtcNow.AddSeconds(8);
            timer.Tick += (_, _) => {
                if (planner.OverlayTracks.Count == 2 || DateTime.UtcNow >= deadline) frame.Continue = false;
            };
            timer.Start();
            try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
            Assert.That(planner.OverlayTracks.Count, Is.EqualTo(2), planner.OverlayNotice);
            foreach (var track in planner.OverlayTracks) {
                Assert.That(track.Samples[0].Time, Is.EqualTo(row.Payload.FrameNoon));
                Assert.That(track.Samples[^1].Time, Is.EqualTo(row.Chart.TimeAtMinutes(row.Chart.FrameMinutes)));
            }
        }

        private static IProfile Profile() {
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            return profile.Object;
        }
    }
}
