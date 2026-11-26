using Moq;
using NUnit.Framework;
using NINA.Profile.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Plugin.Interfaces;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using System.Windows;

namespace Zakfo.Unphased.Tests {
    [NonParallelizable, Apartment(ApartmentState.STA)]
    public class CompositionTests {
        private static CompositionContainer Create(Mock<ITelescopeMediator> telescope) {
            var container = new CompositionContainer(new AssemblyCatalog(typeof(Plugin).Assembly));
            var profile = new Mock<IProfile>();
            profile.SetupGet(p => p.PluginSettings).Returns(new NINA.Profile.PluginSettings());
            var profiles = new Mock<IProfileService>();
            profiles.SetupGet(p => p.ActiveProfile).Returns(profile.Object);
            container.ComposeExportedValue(profiles.Object);
            container.ComposeExportedValue(telescope.Object);
            container.ComposeExportedValue(Mock.Of<IImageSaveMediator>());
            container.ComposeExportedValue(Mock.Of<IOptionsVM>());
            container.ComposeExportedValue(Mock.Of<IMessageBroker>());
            return container;
        }
        [Test]
        public async Task ComposesManifestAndPartsInSeparateHostCatalogs() {
            var telescope = new Mock<ITelescopeMediator>();
            using var manifestCatalog = Create(telescope);
            using var partsCatalog = Create(telescope);
            var manifest = manifestCatalog.GetExportedValue<IPluginManifest>();
            var panels = partsCatalog.GetExportedValues<IDockableVM>().ToList();
            Assert.That(panels, Is.Not.Empty);
            await manifest.Initialize();
            await manifest.Teardown();
        }
        /// <summary>
        /// The WPF Application every interface test shares lives in <see cref="WpfHost"/>;
        /// this is the only suite that needs it before touching N.I.N.A. markup.
        /// </summary>
        private static void EnsureWpfHost() => WpfHost.Ensure();

        [Test]
        public void ExportedResourceDictionariesAndTemplatesConstruct() {
            EnsureWpfHost();
            foreach (var type in typeof(Plugin).Assembly.GetTypes().Where(t => typeof(ResourceDictionary).IsAssignableFrom(t))) {
                var dictionary = (ResourceDictionary)Activator.CreateInstance(type);
                foreach (var key in dictionary.Keys) {
                    if (dictionary[key] is not DataTemplate template) continue;
                    var content = template.LoadContent();
                    Assert.That(content, Is.Not.Null, type.FullName);
                    // The panel root itself has to carry the keyboard scope: the arrow keys
                    // are handled there, so a template that forgets it puts Up/Down back in
                    // the hands of WPF's directional navigation (round 7, item 4).
                    if (key.ToString().EndsWith("_Dockable", StringComparison.Ordinal))
                        Assert.That(
                            Zakfo.Unphased.Features.planner.PlannerViewGlue.GetKeyboardScope((DependencyObject)content),
                            "the panel root must carry the keyboard scope, or Up/Down go back to " +
                            "directional navigation instead of the scrub: " + key);
                    // The options dictionary re-keys OptionsTemplate to "<plugin name>_Options"
                    // in its constructor, so that is the name to look for here.
                    if (key.ToString().EndsWith("_Options", StringComparison.Ordinal))
                        AssertTheOptionsPageIsStillInTheRoundEightShape(content);
                }
            }
        }

        /// <summary>
        /// The options page as the round-7 review asked for it: the site fields are the
        /// narrow N.I.N.A. unit fields rather than two boxes stretched across the tab, the
        /// colours are clickable swatches rather than a hex box plus a separate button, and
        /// the palette can actually be reached and read - which is why it is three named
        /// buttons now rather than the CheckBox it used to be, whose N.I.N.A. template has
        /// no ContentPresenter and therefore never showed a label of its own.
        /// </summary>
        private static void AssertTheOptionsPageIsStillInTheRoundEightShape(DependencyObject root) {
            var all = Descendants(root).ToList();
            var fields = all.OfType<System.Windows.Controls.TextBox>().ToList();
            var buttons = all
                .OfType<System.Windows.Controls.Primitives.ButtonBase>()
                // A CheckBox is a ToggleButton is a ButtonBase; it has its own state instead
                // of a command, and there should be none of them on this page at all.
                .Where(b => b is not System.Windows.Controls.Primitives.ToggleButton)
                .ToList();
            // The template is loaded without a DataContext, so the commands are still just
            // bindings - which is what to look at, since the point is which lever each
            // control pulls and that is fixed in the template.
            var commands = buttons
                .Select(b => b.GetBindingExpression(System.Windows.Controls.Primitives.ButtonBase.CommandProperty))
                .Select(expression => expression?.ParentBinding?.Path?.Path ?? string.Empty)
                .ToList();

            Assert.Multiple(() => {
                Assert.That(
                    fields.All(f => f is NINA.CustomControlLibrary.UnitTextBox),
                    "every site field must be N.I.N.A.'s UnitTextBox, and no colour may still be a hex box");
                Assert.That(fields, Has.Count.EqualTo(2), "latitude and longitude, nothing else typed");
                Assert.That(
                    fields.All(f => !double.IsNaN(f.Width) && f.Width <= 160),
                    "UnitTextBox sizes itself to its content, so the width has to be stated");
                Assert.That(
                    commands.OrderBy(p => p, StringComparer.Ordinal),
                    Is.EquivalentTo(new[] {
                        "Settings.ApplySiteCommand", "Settings.DefaultSchemeCommand",
                        "Settings.EnhancedSchemeCommand", "Settings.NightSchemeCommand",
                        "Settings.PickHorizonColorCommand", "Settings.PickMoonColorCommand",
                        "Settings.PickSunColorCommand", "Settings.ResetColorsCommand",
                        "Settings.ResetSiteCommand",
                    }),
                    "three swatches, Apply, Reset, reset-colours, and one button per palette");
                Assert.That(
                    buttons
                        .Where(b => (b.GetBindingExpression(System.Windows.Controls.Primitives.ButtonBase.CommandProperty)
                            ?.ParentBinding?.Path?.Path ?? string.Empty).Contains("Pick", StringComparison.Ordinal))
                        .All(b => b.Content is System.Windows.Controls.Border),
                    "a colour's button is its own swatch, not a decorative box beside a text field");
                // Round 8, T5: a CheckBox is on or off, and a palette is neither - there are
                // three of them and no way to have none. So the night switch is gone.
                Assert.That(all.OfType<System.Windows.Controls.CheckBox>(), Is.Empty,
                    "the night switch has been replaced by the three palette buttons");
                Assert.That(
                    buttons
                        .Where(b => (b.GetBindingExpression(System.Windows.Controls.Primitives.ButtonBase.CommandProperty)
                            ?.ParentBinding?.Path?.Path ?? string.Empty).EndsWith("SchemeCommand", StringComparison.Ordinal))
                        .Select(b => b.Content as string),
                    Is.EquivalentTo(new[] { "N.I.N.A. theme", "Night", "Enhanced" }),
                    "each palette is named on the button that selects it");
            });
        }

        /// <summary>
        /// N.I.N.A.'s UnitTextBox styles itself from its own theme dictionary, which is only
        /// pulled in when the control is measured. A control that finds no template paints
        /// nothing, so the site fields would have vanished into thin air rather than been
        /// merely wide. This measures the real page and checks the number box and its ° unit
        /// both came out.
        /// </summary>
        [Test]
        public void TheSiteFieldsGetNinasOwnFieldTemplate() {
            EnsureWpfHost();
            var dictionary = new Zakfo.Unphased.Features.settings.Resources();
            var key = new Plugin().Name + "_Options";
            var page = (System.Windows.FrameworkElement)((DataTemplate)dictionary[key]).LoadContent();
            page.Measure(new Size(600, 600));
            page.Arrange(new Rect(new Point(0, 0), page.DesiredSize));

            var fields = Descendants(page).OfType<NINA.CustomControlLibrary.UnitTextBox>().ToList();
            Assert.That(fields, Has.Count.EqualTo(2));
            foreach (var field in fields) {
                Assert.That(
                    VisualDescendants(field).OfType<System.Windows.Controls.TextBox>().Count(),
                    Is.GreaterThanOrEqualTo(2),
                    "the field must paint its own number box plus the unit box, not an empty rectangle");
            }
        }

        private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root) {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++) {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var deeper in VisualDescendants(child)) yield return deeper;
            }
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root) {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) {
                yield return child;
                foreach (var deeper in Descendants(child)) yield return deeper;
            }
        }
    }
}
