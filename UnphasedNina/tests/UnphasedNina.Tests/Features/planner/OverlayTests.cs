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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Zakfo.Unphased.Core;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;
using PlannerResources = Zakfo.Unphased.Features.planner.Resources;
using Visibility = System.Windows.Visibility;

namespace Zakfo.Unphased.Tests.Features.planner {
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class OverlayTests {
        [Test]
        public void ClearTimeCountsOnlyThePartsOfTheDarkWindowAboveTheHorizon() {
            var samples = new[] { (0.0, 2.0), (10.0, -2.0), (20.0, 2.0) };
            Assert.That(NightPlannerVM.TimeAboveHorizon(samples, 2, 18), Is.EqualTo(6));
            Assert.That(NightPlannerVM.TimeAboveHorizon(samples, 6, 14), Is.Zero);
            Assert.That(NightPlannerVM.TimeAboveHorizon(samples, 2, 4), Is.EqualTo(2));
            Assert.That(NightPlannerVM.TimeAboveHorizon(samples, 10, 18), Is.EqualTo(3));
        }

        [Test]
        public void PlanetSwitchesShowTheirSelectedGlyphStyleOnlyWhileEnabled() {
            WpfHost.Ensure();
            PluginSettings.Shared?.Dispose();
            var profiles = new Mock<IProfileService>();
            profiles.SetupGet(service => service.ActiveProfile).Returns(Profile());
            var panel = new PlannerDockableVM(profiles.Object, Mock.Of<ITelescopeMediator>());
            using var settings = PluginSettings.Shared;
            var resources = new PlannerResources();
            var key = typeof(PlannerDockableVM).FullName + "_Dockable";
            var view = (FrameworkElement)((DataTemplate)resources[key]).LoadContent();
            view.DataContext = panel;
            var planner = panel.Planner;
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
            view.Measure(new Size(620, 560));
            view.Arrange(new Rect(0, 0, 620, 560));
            view.UpdateLayout();
            var jupiter = planner.Planets.Single(planet => planet.Name == "Jupiter");
            var choices = (ItemsControl)view.FindName("PlanetSwitches");
            var item = (ContentPresenter)choices.ItemContainerGenerator.ContainerFromItem(jupiter);
            var toggle = (CheckBox)choices.ItemTemplate.FindName("PlanetSwitch", item);
            var preview = (OverlayGlyphPreview)choices.ItemTemplate.FindName("PlanetPreview", item);
            Assert.That(preview.Planner, Is.SameAs(planner));
            Assert.That(preview.Visibility, Is.EqualTo(Visibility.Collapsed));
            toggle.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            view.UpdateLayout();
            Assert.That(preview.Visibility, Is.EqualTo(Visibility.Visible));

            var styles = (ComboBox)view.FindName("GlyphStylePicker");
            var images = new HashSet<string>();
            foreach (var style in planner.GlyphStyles) {
                styles.SetCurrentValue(ComboBox.SelectedItemProperty, style);
                view.UpdateLayout();
                Assert.That(planner.GlyphStyle, Is.EqualTo(style));
                var drawing = VisualTreeHelper.GetDrawing(preview);
                Assert.That(drawing, Is.Not.Null, $"{style} should draw the enabled switch's glyph");
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen()) dc.DrawDrawing(drawing);
                var bitmap = new RenderTargetBitmap(32, 25, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var pixels = new byte[32 * 25 * 4];
                bitmap.CopyPixels(pixels, 32 * 4, 0);
                Assert.That(pixels.Any(value => value != 0), Is.True, style.ToString());
                images.Add(Convert.ToBase64String(pixels));
            }
            Assert.That(images.Count, Is.EqualTo(3), "each style paints a different preview");
            list.IsExpanded = false;
            Assert.That(planner.PlanetSelection, Is.EqualTo("1 selected"));
            Assert.That(jupiter.Enabled, Is.True);
            list.IsExpanded = true;
            toggle.SetCurrentValue(CheckBox.IsCheckedProperty, false);
            Assert.That(preview.Visibility, Is.EqualTo(Visibility.Collapsed));
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
            // Existing profiles stored these four fields without the object's type.
            settings.OverlayTargetsJson = """[{"Id":"M42","Name":"M42","RightAscensionHours":5.588,"DeclinationDegrees":-5.39}]""";
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            var firstChoice = planner.Targets.Single();
            Assert.That(firstChoice.Target.ObjectType, Is.Null);

            framing.SetupGet(vm => vm.DSO).Returns(new DeepSkyObject("M42", coordinates, null) { DSOType = "BRTNB" });
            panel.AddFramingTargetCommand.Execute(null);
            Assert.That(planner.Targets.Single(), Is.SameAs(firstChoice));
            Assert.That(firstChoice.Target.ObjectType, Is.EqualTo("BRTNB"));
            Assert.That(firstChoice.Enabled, Is.True);
            firstChoice.Enabled = false;
            panel.AddFramingTargetCommand.Execute(null);
            Assert.That(planner.Targets.Single(), Is.SameAs(firstChoice));
            Assert.That(firstChoice.Enabled, Is.True);

            var other = new Coordinates(0.71, 41.27, Epoch.J2000, Coordinates.RAType.Hours);
            framing.SetupGet(vm => vm.DSO).Returns(new DeepSkyObject("M31", other, null) { DSOType = "GALXY" });
            panel.AddFramingTargetCommand.Execute(null);
            Assert.That(firstChoice.Target.RightAscensionHours, Is.EqualTo(5.588).Within(1e-8));
            Assert.That(planner.Targets.Count, Is.EqualTo(2));

            var resources = new PlannerResources();
            var key = typeof(PlannerDockableVM).FullName + "_Dockable";
            var view = (FrameworkElement)((DataTemplate)resources[key]).LoadContent();
            view.DataContext = panel;
            ((Expander)view.FindName("TargetOverlays")).IsExpanded = true;
            view.Measure(new Size(900, 560));
            view.Arrange(new Rect(0, 0, 900, 560));
            view.UpdateLayout();
            var choices = (ItemsControl)view.FindName("TargetSwitches");
            var item = (ContentPresenter)choices.ItemContainerGenerator.ContainerFromItem(firstChoice);
            var preview = (OverlayGlyphPreview)choices.ItemTemplate.FindName("TargetPreview", item);
            Assert.That(preview.Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(VisualTreeHelper.GetDrawing(preview)?.Bounds.IsEmpty, Is.False);
            firstChoice.Enabled = false;
            Assert.That(preview.Visibility, Is.EqualTo(Visibility.Collapsed));

            active = Profile();
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            Assert.That(planner.Targets, Is.Empty);
            active = first;
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            Assert.That(planner.Targets.Select(choice => choice.Target.Id), Is.EqualTo(new[] { "M42", "M31" }));
            Assert.That(planner.Targets.Select(choice => choice.Target.ObjectType), Is.EqualTo(new[] { "BRTNB", "GALXY" }));
            Assert.That(planner.TargetSelection, Is.EqualTo("0 selected"));
            planner.RemoveTargetCommand.Execute(planner.Targets[0]);
            profiles.Raise(service => service.ProfileChanged += null, EventArgs.Empty);
            Assert.That(planner.Targets.Single().Target.Id, Is.EqualTo("M31"));
        }

        [TestCase("M 31", 10.68470833, 41.26875, "GALXY")]
        [TestCase("M 33", 23.4621, 30.65994167, "GALXY")]
        [TestCase("M 13", 250.423475, 36.46131944, "GLOCL")]
        [TestCase("M 13", 10.68470833, 41.26875, null)]
        [Explicit("Reads the installed N.I.N.A. catalog.")]
        public void CatalogTypeRequiresAnExactAliasAndMatchingPosition(string name, double raDegrees, double dec, string expected) {
            var target = new FramingTarget(name, name, raDegrees / 15, dec);
            Assert.That(TargetCatalog.FindType(target), Is.EqualTo(expected));
        }

        [Test, Explicit("Requires N.I.N.A.'s External NOVAS, SOFA and JPLEPH files beside the test assembly.")]
        public void OverlayAltitudesAgreeWithIndependentEphemerisAndFollowTheLatestFrame() {
            WpfHost.Ensure();
            var astroTime = new AstroTime(new DateTime(2026, 11, 26, 23, 0, 0, DateTimeKind.Utc));
            var orion = new FramingTarget("M42", "Orion Nebula", 5.588, -5.39);
            var radians = Math.PI / 180;
            var ra = orion.RightAscensionHours * 15 * radians;
            var dec = orion.DeclinationDegrees * radians;
            var equatorialVector = new AstroVector(Math.Cos(ra) * Math.Cos(dec), Math.Sin(ra) * Math.Cos(dec), Math.Sin(dec), astroTime);
            var ofDate = Astronomy.EquatorFromVector(Astronomy.RotateVector(Astronomy.Rotation_EQJ_EQD(astroTime), equatorialVector));
            foreach (var site in new[] { new Site(50.088, 14.421), new Site(-33.869, 151.209) }) {
                var observer = new Observer(site.Lat, site.Lon, site.Elev);
                foreach (var (planet, body) in new[] { (NOVAS.Body.Mercury, Body.Mercury), (NOVAS.Body.Venus, Body.Venus),
                    (NOVAS.Body.Mars, Body.Mars), (NOVAS.Body.Jupiter, Body.Jupiter), (NOVAS.Body.Saturn, Body.Saturn),
                    (NOVAS.Body.Uranus, Body.Uranus), (NOVAS.Body.Neptune, Body.Neptune) }) {
                    var equatorial = Astronomy.Equator(body, astroTime, observer, EquatorEpoch.OfDate, Aberration.Corrected);
                    var expected = Astronomy.Horizon(astroTime, observer, equatorial.ra, equatorial.dec, Refraction.None);
                    var actual = OverlayEphemeris.PlanetAltitude(planet, site, new DateTimeOffset(astroTime.ToUtcDateTime()));
                    var azimuth = OverlayEphemeris.PlanetHorizontal(planet, site, new DateTimeOffset(astroTime.ToUtcDateTime())).Azimuth;
                    Assert.That(actual, Is.EqualTo(expected.altitude).Within(.05), $"{planet} at {site}");
                    Assert.That(Math.IEEERemainder(azimuth - expected.azimuth, 360), Is.EqualTo(0).Within(.05), $"{planet} azimuth at {site}");
                }
                var targetExpected = Astronomy.Horizon(astroTime, observer, ofDate.ra, ofDate.dec, Refraction.None);
                Assert.That(OverlayEphemeris.TargetAltitude(orion, site, new DateTimeOffset(astroTime.ToUtcDateTime())),
                    Is.EqualTo(targetExpected.altitude).Within(.05), $"{orion.Name} at {site}");
                Assert.That(Math.IEEERemainder(
                    OverlayEphemeris.TargetHorizontal(orion, site, new DateTimeOffset(astroTime.ToUtcDateTime())).Azimuth - targetExpected.azimuth, 360),
                    Is.EqualTo(0).Within(.05), $"{orion.Name} azimuth at {site}");
            }
            var planner = PlannerFixture.WindowOfThreeNights();
            planner.AddFramingTarget(orion);
            planner.Planets.Single(planet => planet.Name == "Jupiter").Enabled = true;
            var venus = planner.Planets.Single(planet => planet.Name == "Venus");
            venus.Enabled = true;
            planner.SelectedDate = new DateTime(2026, 11, 26);
            planner.SetZoom(2);
            var row = planner.SelectedNight;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher) {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            var deadline = DateTime.UtcNow.AddSeconds(8);
            timer.Tick += (_, _) => {
                if (planner.OverlayTracks.Count == 3 || DateTime.UtcNow >= deadline) frame.Continue = false;
            };
            timer.Start();
            try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
            Assert.That(planner.OverlayTracks.Count, Is.EqualTo(3), planner.OverlayNotice);
            foreach (var track in planner.OverlayTracks) {
                Assert.That(track.Samples[0].Time, Is.EqualTo(row.Payload.FrameNoon));
                Assert.That(track.Samples[^1].Time, Is.EqualTo(row.Chart.TimeAtMinutes(row.Chart.FrameMinutes)));
            }
            var venusTrack = planner.OverlayTracks.Single(track => track.Choice == venus);
            var midpoint = venusTrack.PhaseAt(row.Payload.MidnightTime);
            Assert.That(planner.PlanetPreviewPhase(venus), Is.EqualTo(midpoint));
            planner.SetScrub(9 * 60);
            Assert.That(planner.PlanetPreviewPhase(venus), Is.EqualTo(venusTrack.Phases[row.Chart.TimeAtMinutes(9 * 60)]));
        }

        private static IProfile Profile() {
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            return profile.Object;
        }
    }
}
