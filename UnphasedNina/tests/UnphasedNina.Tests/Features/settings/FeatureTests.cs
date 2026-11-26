using NUnit.Framework;
using Zakfo.Unphased.Features.planner;
using Zakfo.Unphased.Features.settings;
using Moq;
using NINA.Profile.Interfaces;

namespace Zakfo.Unphased.Tests.Features.settings {
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class FeatureTests {
        private static Mock<IProfileService> ProfilesWithAstrometry(double lat, double lon) {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            var astro = new Mock<IAstrometrySettings>();
            astro.SetupGet(a => a.Latitude).Returns(lat);
            astro.SetupGet(a => a.Longitude).Returns(lon);
            profile.SetupGet(p => p.AstrometrySettings).Returns(astro.Object);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            return profiles;
        }

        [Test]
        public void EffectiveSiteFallsBackToProfileAstrometry() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            Assert.That(settings.EffectiveSite(), Is.EqualTo((50.0875, 14.4213)));
        }

        [Test]
        public void OverrideWinsOverProfileAndPartialOverrideIsIgnored() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            settings.SiteLatOverride = "-33.8688";
            // Only latitude set: incomplete override must not half-apply.
            Assert.That(settings.EffectiveSite(), Is.EqualTo((50.0875, 14.4213)));
            settings.SiteLonOverride = "151.2093";
            Assert.That(settings.EffectiveSite(), Is.EqualTo((-33.8688, 151.2093)));
            // Garbage input falls back to the profile instead of breaking.
            settings.SiteLonOverride = "not a number";
            Assert.That(settings.EffectiveSite(), Is.EqualTo((50.0875, 14.4213)));
        }

        /// <summary>
        /// Round 7, item 1e: the site fields wrote straight into the stored override, so a
        /// half-typed pair sat in the settings while the chart quietly used the profile, and
        /// the boxes looked empty even when a site was in effect. They now edit drafts
        /// behind an Apply/Reset pair.
        /// </summary>
        [Test]
        public void ApplyPinsThePairAndResetFollowsTheProfileAgain() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();

            // The boxes start by showing what is in effect rather than two blanks.
            Assert.Multiple(() => {
                Assert.That(settings.SiteLatDraft, Is.EqualTo("50.0875"));
                Assert.That(settings.SiteLonDraft, Is.EqualTo("14.4213"));
                Assert.That(settings.SiteReadout, Does.Contain("astrometry settings"));
            });

            settings.SiteLatDraft = "-33.8688";
            settings.SiteLonDraft = "151.2093";
            Assert.That(settings.ApplySiteCommand.CanExecute(null), Is.True, "a complete pair can be applied");
            settings.ApplySiteCommand.Execute(null);

            Assert.Multiple(() => {
                Assert.That(settings.EffectiveSite(), Is.EqualTo((-33.8688, 151.2093)), "the pinned pair is what the chart uses");
                Assert.That(settings.SiteOverrideActive, Is.True);
                Assert.That(settings.SiteNotice, Does.Contain("pinned"));
                Assert.That(settings.SiteReadout, Does.Contain("pinned here"));
            });

            settings.ResetSiteCommand.Execute(null);
            Assert.Multiple(() => {
                Assert.That(settings.SiteOverrideActive, Is.False, "the override is gone");
                Assert.That(settings.EffectiveSite(), Is.EqualTo((50.0875, 14.4213)), "the profile site is back in effect");
                Assert.That(settings.SiteLatDraft, Is.EqualTo("50.0875"), "and the boxes show it");
                Assert.That(settings.SiteNotice, Does.Contain("Following this profile's site"));
            });
        }

        [Test]
        public void AHalfFilledPairIsRefusedOutLoudInsteadOfHalfApplying() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();

            // The boxes start full, so an unusable pair means an emptied box rather than a
            // box nobody noticed was blank.
            settings.SiteLatDraft = "-33.8688";
            settings.SiteLonDraft = string.Empty;

            Assert.Multiple(() => {
                Assert.That(settings.CanApplySite(), Is.False, "one coordinate is not a site");
                Assert.That(settings.ApplySiteCommand.CanExecute(null), Is.False, "so the button is disabled, not silently reverting");
                Assert.That(settings.SiteNotice, Does.Contain("both"), "and the page says why");
                Assert.That(settings.EffectiveSite(), Is.EqualTo((50.0875, 14.4213)), "nothing was stored");
                Assert.That(settings.SiteLatOverride, Is.Empty);
            });
        }

        [Test]
        public void ApplyClampsToWhatASphereHoldsAndSaysSo() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();

            settings.SiteLatDraft = "95";
            settings.SiteLonDraft = "-200";
            settings.ApplySite();

            Assert.Multiple(() => {
                Assert.That(settings.EffectiveSite(), Is.EqualTo((90.0, -180.0)));
                Assert.That(settings.SiteNotice, Does.Contain("+/-90"), "the clamp is not silent");
                Assert.That(settings.SiteLatDraft, Is.EqualTo("90"), "the boxes show what was stored");
            });

            settings.ResetSite();
        }

        [Test]
        public void TheSwatchToolTipSaysWhatTheChartActuallyPaints() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            settings.ResetColors();

            Assert.That(settings.SunColorNote, Does.Contain("follows the palette"));

            settings.SunColor = "#FFD700";
            Assert.Multiple(() => {
                Assert.That(settings.SunColorNote, Does.Contain("pinned to #FFD700"));
                Assert.That(settings.SunColorNote, Does.Contain("#FFD700"));
                Assert.That(settings.MoonColorNote, Does.Contain("follows the palette"), "only the role that changed speaks up");
            });

            settings.ResetColors();
        }

        [Test]
        public void NoProfileSiteAndNoOverrideYieldsNull() {
            var profiles = new Mock<IProfileService>();
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            profile.SetupGet(p => p.AstrometrySettings).Returns((IAstrometrySettings)null);
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            Assert.That(settings.EffectiveSite(), Is.Null);
        }

        [Test]
        public void ProfileSwitchRaisesOverridePropertyNotifications() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using var settings = new PluginSettings(profiles.Object);
            settings.Initialize();
            var notifications = new List<string>();
            settings.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            profiles.Raise(p => p.ProfileChanged += null, EventArgs.Empty);
            Assert.That(notifications, Does.Contain(nameof(PluginSettings.SiteLatOverride)));
            Assert.That(notifications, Does.Contain(nameof(PluginSettings.SiteLonOverride)));
            // After dispose the handler is detached: no more notifications.
            settings.Dispose();
            notifications.Clear();
            profiles.Raise(p => p.ProfileChanged += null, EventArgs.Empty);
            Assert.That(notifications, Is.Empty);
        }

        /// <summary>
        /// Round 8, T5: the night switch became a three-way palette, and the palette stopped
        /// being saved at all - a scheme you picked at 2am should not be waiting for you the
        /// next afternoon with the room light on. A file left behind by an older build still
        /// has to reach the night palette once, and then lose the key, because a flag left in
        /// the file would put night mode back on at every launch.
        /// </summary>
        [Test]
        public void TheSchemeIsNotRememberedButAnOldNightFlagArrivesOnceAndThenGoes() {
            var path = System.IO.Path.Combine(
                NINA.Core.Utility.CoreUtil.APPLICATIONTEMPPATH, "PluginSettings",
                new Plugin().Identifier, "settings.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            try {
                // Written by hand, because the current build no longer writes that key.
                System.IO.File.WriteAllText(path,
                    "{\"darkMode\": true, \"sunColor\": \"FFD700\", \"moonColor\": \"\", \"horizonColor\": \"\"}");
                var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
                using (var migrated = new PluginSettings(profiles.Object)) {
                    migrated.Initialize();
                    Assert.Multiple(() => {
                        Assert.That(migrated.Scheme, Is.EqualTo(ChartScheme.Night),
                            "a file that only knew one night flag arrives as the night palette");
                        Assert.That(migrated.SunColor, Is.EqualTo("#FFD700"),
                            "and reading the old key does not lose what the file also held");
                    });
                    migrated.Scheme = ChartScheme.Enhanced;
                }
                using (var fresh = new PluginSettings(profiles.Object)) {
                    fresh.Initialize();
                    Assert.Multiple(() => {
                        Assert.That(fresh.Scheme, Is.EqualTo(ChartScheme.Default),
                            "the palette is not carried between runs: each one starts on the theme");
                        Assert.That(System.IO.File.ReadAllText(path), Does.Not.Contain("darkMode"),
                            "the old key leaves the file, or it would set night mode again next launch");
                        Assert.That(fresh.SunColor, Is.EqualTo("#FFD700"),
                            "while the colours still persist as they always did");
                    });
                }
            } finally {
                // This file is shared with the colour tests below, so leave nothing behind.
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void PickedChartColoursPersistAndResetToTheScheme() {
            // Same caveat as the palette test above: these settings live in the real
            // plugin options file, so the test leaves every role unpicked again.
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            using (var settings = new PluginSettings(profiles.Object)) {
                settings.Initialize();
                settings.SunColor = "FFD700";
                Assert.Multiple(() => {
                    Assert.That(settings.SunColor, Is.EqualTo("#FFD700"), "a typed value gains its # and keeps it");
                    Assert.That(((System.Windows.Media.SolidColorBrush)settings.SunColorBrush).Color,
                        Is.EqualTo(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)));
                });
            }
            using (var restored = new PluginSettings(profiles.Object)) {
                restored.Initialize();
                Assert.Multiple(() => {
                    Assert.That(restored.SunColor, Is.EqualTo("#FFD700"), "the pick survives a restart");
                    Assert.That(restored.MoonColor, Is.Empty, "and only the role that was picked changes");
                });
                restored.ResetColors();
                Assert.That(restored.SunColor, Is.Empty);
            }
            using var clean = new PluginSettings(profiles.Object);
            clean.Initialize();
            Assert.Multiple(() => {
                Assert.That(clean.SunColor, Is.Empty, "the reset is persisted too");
                // An unpicked swatch is not blank: it shows what the chart paints.
                Assert.That(clean.SunColorBrush, Is.Not.Null);
                Assert.That(clean.HorizonColorBrush, Is.Not.Null);
            });
        }

        /// <summary>
        /// N.I.N.A. composes a plugin's manifest and its parts in two separate MEF
        /// containers, so a [Shared] exported settings part exists twice - one behind the
        /// options page, one behind the panel - and a colour picked there never reaches the
        /// chart here. The plugin therefore asks for one object by hand.
        /// </summary>
        [Test]
        public void TheOptionsPageAndThePanelShareOneSettingsObject() {
            var profiles = ProfilesWithAstrometry(50.0875, 14.4213);
            var pickedOnTheOptionsPage = PluginSettings.For(profiles.Object);
            var readByThePanel = PluginSettings.For(profiles.Object);

            Assert.That(readByThePanel, Is.SameAs(pickedOnTheOptionsPage));

            pickedOnTheOptionsPage.SunColor = "#FFD700";
            Assert.That(readByThePanel.SunColor, Is.EqualTo("#FFD700"),
                "the object the chart reads is the object the options page wrote");

            pickedOnTheOptionsPage.Dispose();
            Assert.That(PluginSettings.Shared, Is.Null, "a stopped object is not handed out again");
            try {
                var rebuilt = PluginSettings.For(profiles.Object);
                Assert.That(rebuilt, Is.Not.SameAs(pickedOnTheOptionsPage));
                Assert.That(rebuilt.SunColor, Is.EqualTo("#FFD700"), "the rebuild reads the file back");
                rebuilt.ResetColors();
            } finally {
                PluginSettings.Shared?.Dispose();
            }
        }

        [Test]
        public void OptionsLookupUsesTheManifestName() {
            var manifest = new Plugin();
            var resources = new Zakfo.Unphased.Features.settings.Resources();
            Assert.That(resources.Contains("OptionsTemplate"), Is.False);
            Assert.That(resources.Contains(manifest.Name + "_Options"), Is.True);
        }
    }
}
