using NINA.Profile.Interfaces;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel.Composition;

namespace Zakfo.Unphased {
    [Export(typeof(IPluginManifest))]
    public class Plugin : PluginBase {
        /// <summary>
        /// Handed out by N.I.N.A. in the container that composes the manifest. Used only to
        /// build the settings object; see Features/settings/PluginSettings.cs for why the
        /// settings are a process singleton instead of an imported part.
        /// </summary>
        [Import]
        public IProfileService ProfileService { get; set; }

        /// <summary>
        /// What the options page binds to. It has to be the very object the dockable panel
        /// reads, or a colour picked here never reaches the chart.
        /// </summary>
        public Features.settings.PluginSettings Settings => Features.settings.PluginSettings.For(ProfileService);

        public override Task Initialize() {
            Settings.Initialize();
            return Task.CompletedTask;
        }

        public override Task Teardown() {
            // Shared, not Settings: after Dispose the singleton is dropped, and touching the
            // property again would quietly build a replacement during shutdown.
            Features.settings.PluginSettings.Shared?.Dispose();
            return Task.CompletedTask;
        }
    }
}
