using NUnit.Framework;
using System;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Zakfo.Unphased.Tests {
    public class PluginTests {
        [Test]
        public void ManifestHasValidIdentityAndHostBaseline() {
            var plugin = new Plugin();
            Assert.That(Guid.TryParse(plugin.Identifier, out _), Is.True);
            Assert.That(plugin.Name, Is.Not.Empty);
            Assert.That(plugin.MinimumApplicationVersion.ToString(), Is.EqualTo("3.2.0.9001"));
            Assert.That(typeof(Plugin).GetCustomAttributes<ExportAttribute>().Single().ContractType,
                Is.EqualTo(typeof(NINA.Plugin.Interfaces.IPluginManifest)));
        }
        [Test]
        public void AssemblyMetadataUsesDisplayNameAndDisablesComExposure() {
            var assembly = typeof(Plugin).Assembly;
            Assert.Multiple(() => {
                Assert.That(assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product, Is.EqualTo(new Plugin().Name));
                Assert.That(assembly.GetCustomAttribute<ComVisibleAttribute>()?.Value, Is.False);
            });
        }

        /// <summary>
        /// What happens when N.I.N.A. unloads the plugin - a profile switch, a plugin update, or
        /// the application closing - and loads it again in the same process.
        /// <para>
        /// The settings object is deliberately a process singleton (<c>PluginSettings.Shared</c>)
        /// because the options page and the dockable panel are composed in different MEF containers
        /// and must read one object. That only holds if unload releases it: otherwise a second load
        /// in the same process keeps talking to the <see cref="NINA.Profile.Interfaces.IProfileService"/>
        /// of a profile that no longer exists, and a colour picked on the options page stops
        /// reaching the chart with nothing logged.
        /// </para>
        /// </summary>
        [Test, NonParallelizable]
        public void TeardownReleasesTheSettingsSingletonSoReloadingThePluginStartsFresh() {
            var first = ProfilesWithAstrometry(50.08804, 14.42076);
            var plugin = new Plugin { ProfileService = first.Object };

            plugin.Initialize().GetAwaiter().GetResult();
            var loaded = Zakfo.Unphased.Features.settings.PluginSettings.Shared;

            Assert.That(loaded, Is.Not.Null, "Initialize must have built the settings object");
            Assert.That(
                ReferenceEquals(loaded, plugin.Settings),
                Is.True,
                "the options page and the panel have to bind to the very same object");

            plugin.Teardown().GetAwaiter().GetResult();

            Assert.Multiple(() => {
                Assert.That(
                    Zakfo.Unphased.Features.settings.PluginSettings.Shared,
                    Is.Null,
                    "Teardown must drop the process singleton, not leave a disposed one behind");
                // The disposed instance detached itself from the old profile service, so a profile
                // event on the service the previous load used cannot revive it.
                Assert.That(() => first.Raise(p => p.ProfileChanged += null, EventArgs.Empty), Throws.Nothing);
            });

            // The next load, against a different profile service, gets a live object of its own.
            var second = ProfilesWithAstrometry(-33.8688, 151.2093);
            var reloaded = new Plugin { ProfileService = second.Object };
            reloaded.Initialize().GetAwaiter().GetResult();

            try {
                Assert.Multiple(() => {
                    Assert.That(Zakfo.Unphased.Features.settings.PluginSettings.Shared, Is.Not.Null);
                    Assert.That(
                        Zakfo.Unphased.Features.settings.PluginSettings.Shared,
                        Is.Not.SameAs(loaded),
                        "a reload must not hand back the object the previous load disposed");
                    Assert.That(reloaded.Settings.EffectiveSite(), Is.EqualTo((-33.8688, 151.2093)));
                });
            } finally {
                Zakfo.Unphased.Features.settings.PluginSettings.Shared?.Dispose();
            }
        }

        private static Moq.Mock<NINA.Profile.Interfaces.IProfileService> ProfilesWithAstrometry(double lat, double lon) {
            var profiles = new Moq.Mock<NINA.Profile.Interfaces.IProfileService>();
            var profile = new Moq.Mock<NINA.Profile.Interfaces.IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            var astro = new Moq.Mock<NINA.Profile.Interfaces.IAstrometrySettings>();
            astro.SetupGet(a => a.Latitude).Returns(lat);
            astro.SetupGet(a => a.Longitude).Returns(lon);
            profile.SetupGet(p => p.AstrometrySettings).Returns(astro.Object);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            return profiles;
        }
    }
}
