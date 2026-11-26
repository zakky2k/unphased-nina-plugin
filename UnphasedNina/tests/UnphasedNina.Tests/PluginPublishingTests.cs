using Newtonsoft.Json.Linq;
using NJsonSchema;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Zakfo.Unphased.Tests {
    /// <summary>
    /// The plugin's listing metadata, checked the way N.I.N.A. would check it.
    /// <para>
    /// A plugin that is installed by hand gets away with almost empty metadata: N.I.N.A.
    /// reads the manifest off the assembly (<c>PluginBase</c>) and loads the panel. A plugin
    /// in the catalogue is different - the store serves a JSON manifest that N.I.N.A.
    /// validates against <c>PluginManifest.Schema</c> before it will show the plugin at all
    /// (<c>PluginFetcher.cs:78</c>), and a manifest that fails that schema is silently
    /// dropped. So the text written in <c>Plugin.props</c> is store input, not decoration,
    /// and it has limits: Name 50 characters, ShortDescription 256, LongDescription 10000,
    /// Author 256, License 512, and at most 20 tags of at most 30 characters each.
    /// </para>
    /// <para>
    /// The schema file is the one N.I.N.A. itself uses, vendored unmodified from
    /// <c>github.com/isbeorn/nina.plugin.manifests</c> under
    /// <c>UnphasedNina/build/vendor/</c> and copied to the test output.
    /// </para>
    /// </summary>
    public class PluginPublishingTests {

        private static PluginManifestShape ShapeOf(Plugin plugin) {
            return new PluginManifestShape(plugin);
        }

        [Test]
        public async Task TheStoreManifestBuiltFromThisAssemblyValidatesAgainstTheNinaSchema() {
            var plugin = new Plugin();
            var token = JObject.Parse(ShapeOf(plugin).ToJson());

            var schema = await JsonSchema.FromJsonAsync(ReadVendoredSchema());
            var errors = schema.Validate(token);

            Assert.That(
                errors.Select(e => $"{e.Property} {e.Kind} at line {e.LineNumber}"),
                Is.Empty,
                "the store manifest built from this assembly must validate against N.I.N.A.'s own schema");
        }

        [Test]
        public void TheListingTextRespectsTheSchemaLimitsAndCarriesNoPlaceholders() {
            var plugin = new Plugin();
            var description = plugin.Descriptions;

            Assert.Multiple(() => {
                Assert.That(plugin.Name, Is.EqualTo("Unphased Night Planner"), "the store page title");
                Assert.That(plugin.Name.Length, Is.LessThanOrEqualTo(50));
                Assert.That(plugin.Author, Is.Not.Empty.And.Not.EqualTo("Plugin Author"));
                Assert.That(plugin.Author.Length, Is.LessThanOrEqualTo(256));
                Assert.That(plugin.License, Is.EqualTo("MPL-2.0"));
                Assert.That(plugin.License.Length, Is.LessThanOrEqualTo(512));

                Assert.That(
                    description.ShortDescription,
                    Is.Not.Empty.And.Not.EqualTo("A NINA plugin."),
                    "one sentence of what the plugin does, which is what a person decides to install from");
                Assert.That(description.ShortDescription.Length, Is.LessThanOrEqualTo(256));
                Assert.That(description.LongDescription, Is.Not.Empty);
                Assert.That(description.LongDescription.Length, Is.LessThanOrEqualTo(10000));

                Assert.That(plugin.Tags, Is.Not.Empty);
                Assert.That(plugin.Tags.Length, Is.LessThanOrEqualTo(20));
                Assert.That(plugin.Tags, Has.All.Length.LessThanOrEqualTo(30));
                Assert.That(plugin.Tags, Has.None.Empty);
            });
        }

        [Test]
        public void TheLinksTheStorePageShowsAreHttpsAndTheChangelogIsThisRepository() {
            var plugin = new Plugin();

            Assert.Multiple(() => {
                foreach (var (what, url) in new[] {
                    ("LicenseURL", plugin.LicenseURL),
                    ("Repository", plugin.Repository),
                    ("Homepage", plugin.Homepage),
                    ("ChangelogURL", plugin.ChangelogURL),
                }) {
                    Assert.That(
                        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps,
                        Is.True,
                        $"{what} has to be an absolute https URL for the store page to link it: '{url}'");
                }

                // The changelog tab in N.I.N.A.'s plugin view fetches this URL (PluginsView.xaml.cs:153),
                // so it must point at the changelog this repository actually serves, not a copy of it.
                Assert.That(
                    plugin.ChangelogURL,
                    Is.EqualTo("https://raw.githubusercontent.com/zakky2k/AstroScripts/main/nina-plugin/UnphasedNina/CHANGELOG.md"));
            });
        }

        [Test]
        public void TheVersionTheCatalogueComparesIsFourPartAndInsideWhatAnAssemblyVersionAllows() {
            var version = new Plugin().Version;

            Assert.Multiple(() => {
                Assert.That(version.ToString(), Does.Match(@"^\d+\.\d+\.\d+\.\d+$"), "the store keys updates off this");
                Assert.That(new[] { version.Major, version.Minor, version.Patch, version.Build },
                    Is.All.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(65534),
                    "Build-Release.ps1 rejects a component over 65534 because that is what an assembly version takes");
            });
        }

        private static string ReadVendoredSchema() {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "manifest.schema.json");
            Assert.That(File.Exists(path), Is.True, $"the vendored schema should be copied next to the tests: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// The JSON the catalogue would serve for this plugin. Everything here comes from the
        /// assembly except the <c>Installer</c> block, which <c>PluginBase</c> deliberately
        /// leaves empty ("has to be filled out by the plugin maintainer", PluginBase.cs:92)
        /// because the download URL and checksum change with every release -
        /// <c>build/Package-Plugin.ps1</c> is what produces the real values.
        /// </summary>
        private sealed class PluginManifestShape {
            private readonly Plugin plugin;

            public PluginManifestShape(Plugin plugin) {
                this.plugin = plugin;
            }

            public string ToJson() {
                var version = plugin.Version;
                var minimum = plugin.MinimumApplicationVersion;
                var description = plugin.Descriptions;
                var manifest = new JObject {
                    ["Name"] = plugin.Name,
                    ["Identifier"] = plugin.Identifier,
                    ["Version"] = FourPart(version.Major, version.Minor, version.Patch, version.Build),
                    ["Author"] = plugin.Author,
                    ["Homepage"] = plugin.Homepage,
                    ["Repository"] = plugin.Repository,
                    ["License"] = plugin.License,
                    ["LicenseURL"] = plugin.LicenseURL,
                    ["ChangelogURL"] = plugin.ChangelogURL,
                    ["Tags"] = new JArray(plugin.Tags),
                    ["MinimumApplicationVersion"] = FourPart(minimum.Major, minimum.Minor, minimum.Patch, minimum.Build),
                    ["Descriptions"] = new JObject {
                        ["ShortDescription"] = description.ShortDescription,
                        ["LongDescription"] = description.LongDescription,
                        ["FeaturedImageURL"] = description.FeaturedImageURL,
                        ["ScreenshotURL"] = description.ScreenshotURL,
                        ["AltScreenshotURL"] = description.AltScreenshotURL,
                    },
                    ["Installer"] = new JObject {
                        ["URL"] = "https://github.com/zakky2k/AstroScripts/releases/download/v0.0.0.0/UnphasedNina.0.0.0.0.zip",
                        ["Checksum"] = new string('A', 64),
                        ["ChecksumType"] = "SHA256",
                        ["Type"] = "ARCHIVE",
                    },
                };
                return manifest.ToString();
            }

            private static JObject FourPart(int major, int minor, int patch, int build) {
                return new JObject {
                    ["Major"] = major,
                    ["Minor"] = minor,
                    ["Patch"] = patch,
                    ["Build"] = build,
                };
            }
        }
    }
}
