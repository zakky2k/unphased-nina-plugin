using CommunityToolkit.Mvvm.Input;
using NINA.Core.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using Zakfo.Unphased.Features.planner;
// NINA.Core.Utility has a RelayCommand of its own; the commands here are the
// CommunityToolkit ones, whose WPF integration N.I.N.A. also uses.
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace Zakfo.Unphased.Features.settings {
    /// <summary>
    /// Plugin settings. Site latitude/longitude default to the astrometry
    /// settings of the active N.I.N.A. profile but can be overridden per
    /// profile; an explicit override also keeps the chart stable when the
    /// profile has no astrometry site configured. The palette scheme and the
    /// chart colours are global: the scheme switches the chart between the
    /// N.I.N.A. theme, red night vision and the plugin's navy Enhanced chart,
    /// and the colours are per-role overrides on top of whichever scheme is
    /// active. Unlike the site and the colours, the scheme is not saved: it is
    /// a preference for the session you are in, and the file written by an older
    /// build carries a single night flag that reads across once.
    ///
    /// One object for the whole plugin, reached through <see cref="For"/> rather than a MEF
    /// import. N.I.N.A. composes a plugin's manifest and its parts in two **separate**
    /// composition containers (PluginLoader.LoadPlugin calls GetContainer for the manifest,
    /// then PluginLoader.Compose calls GetContainer again for the dockables and templates),
    /// and CreationPolicy.Shared only shares within one container. Exported as a part it
    /// therefore existed twice: one copy behind the options page, another behind the panel.
    /// A colour picked on the options page updated the copy the panel never looked at - and
    /// whichever copy wrote last overwrote the other's values in settings.json.
    /// </summary>
    public sealed class PluginSettings : BaseINPC, IDisposable {
        private readonly IProfileService profiles;
        private readonly PluginOptionsAccessor profileOptions;
        private readonly string globalPath;
        private ChartScheme scheme = ChartScheme.Default;
        private bool initialized;

        private static readonly object SharedGate = new();

        /// <summary>The object handed out by <see cref="For"/>, or null before anything asks for it.</summary>
        public static PluginSettings Shared { get; private set; }

        /// <summary>
        /// The plugin's settings object, built once per application run. The options page
        /// reaches it through the plugin manifest and the dockable panel through its own
        /// import; N.I.N.A. hands both the same IProfileService, so both get the same
        /// instance and share one set of change notifications.
        /// </summary>
        public static PluginSettings For(IProfileService profiles) {
            lock (SharedGate) {
                Shared ??= new PluginSettings(profiles);
                // Idempotent and cheap: whoever asks first gets the file read.
                Shared.Initialize();
                return Shared;
            }
        }

        // Chart colours, global like the scheme - they are a viewing preference, not a
        // property of the site. Stored as hex so the file stays hand-editable; empty
        // means "follow the scheme", which for the sun and the moon is the live N.I.N.A.
        // theme brush, and for the horizon is the plugin's own green.
        private string sunColor = string.Empty, moonColor = string.Empty, horizonColor = string.Empty;

        [ImportingConstructor]
        public PluginSettings(IProfileService profiles) {
            this.profiles = profiles;
            var id = new Plugin().Identifier;
            profileOptions = new PluginOptionsAccessor(profiles, Guid.Parse(id));
            globalPath = Path.Combine(CoreUtil.APPLICATIONTEMPPATH, "PluginSettings", id, "settings.json");
        }

        public void Initialize() {
            if (initialized) return;
            var carriedNightFlag = false;
            if (File.Exists(globalPath)) {
                try {
                    using var doc = JsonDocument.Parse(File.ReadAllText(globalPath));
                    // Migration only. Before the three-way scheme this file carried a
                    // single night flag, so a file written by an older build reads across to
                    // Night - and then the key has to come out of the file, because the
                    // scheme itself is deliberately not saved and a key left behind would
                    // put night mode back on at every launch. ValueKind rather than
                    // GetBoolean so a file holding "darkMode": "yes" is ignored, not thrown.
                    if (doc.RootElement.TryGetProperty("darkMode", out var dark)) {
                        if (dark.ValueKind == JsonValueKind.True)
                            scheme = ChartScheme.Night;
                        carriedNightFlag = true;
                    }
                    // The file is written from the fields below, so its keys are camelCase
                    // like "sunColor"; nameof(SunColor) would look for a key never written.
                    sunColor = ReadHex(doc, "sunColor");
                    moonColor = ReadHex(doc, "moonColor");
                    horizonColor = ReadHex(doc, "horizonColor");
                } catch (JsonException) {
                    // Corrupt settings file: keep defaults rather than crash the plugin.
                }
            }
            profiles.ProfileChanged += OnProfileChanged;
            SeedSiteDrafts();
            initialized = true;
            if (carriedNightFlag) SaveGlobal();
            RaisePropertyChanged(nameof(Scheme));
            RaiseColours();
            RaiseSiteState();
        }

        /// <summary>
        /// Which palette the chart paints with: the N.I.N.A. theme, red night vision, or
        /// the plugin's navy Enhanced chart. Exactly one, which is why the panel offers
        /// three buttons instead of a night switch with a third palette nowhere to go.
        ///
        /// Not saved, unlike the site and the colours. Night mode is what you want at 2am
        /// under a torch and not what you want with the room light on and the chart still
        /// open in the morning, so a saved choice is a trap - a fresh run starts on the
        /// theme palette and the buttons are one click away.
        /// </summary>
        public ChartScheme Scheme {
            get => scheme;
            set {
                if (scheme == value) return;
                scheme = value;
                RaisePropertyChanged();
                // The scheme changes what an unpicked colour resolves to, so the
                // options page has to repaint its swatches.
                RaiseColours();
            }
        }

        private IRelayCommand schemeDefault, schemeNight, schemeEnhanced;

        /// <summary>
        /// The three palette buttons, one per scheme. They are on the settings object
        /// rather than each view model so the panel's buttons and the options page's
        /// buttons are the same action; the panel just hands them out.
        /// </summary>
        public IRelayCommand DefaultSchemeCommand =>
            schemeDefault ??= new RelayCommand(() => Scheme = ChartScheme.Default, () => true);

        public IRelayCommand NightSchemeCommand =>
            schemeNight ??= new RelayCommand(() => Scheme = ChartScheme.Night, () => true);

        public IRelayCommand EnhancedSchemeCommand =>
            schemeEnhanced ??= new RelayCommand(() => Scheme = ChartScheme.Enhanced, () => true);

        // ---- Chart colours ---------------------------------------------------------------
        // Three roles the user can pin a colour to. Empty means "follow the scheme", and
        // the palette resolves that to the live N.I.N.A. theme brush (sun, moon) or the
        // plugin's own green (horizon). A value that cannot be parsed is stored as typed -
        // so it can be corrected - but is ignored when drawing.

        /// <summary>The sun curve colour, as #rrggbb; empty follows the scheme.</summary>
        public string SunColor {
            get => sunColor;
            set {
                var normalized = NormalizeHex(value);
                if (sunColor == normalized) return;
                sunColor = normalized;
                SaveGlobal();
                RaiseColours();
            }
        }

        /// <summary>The moon track colour, as #rrggbb; empty follows the scheme.</summary>
        public string MoonColor {
            get => moonColor;
            set {
                var normalized = NormalizeHex(value);
                if (moonColor == normalized) return;
                moonColor = normalized;
                SaveGlobal();
                RaiseColours();
            }
        }

        /// <summary>The horizon line colour, as #rrggbb; empty follows the scheme.</summary>
        public string HorizonColor {
            get => horizonColor;
            set {
                var normalized = NormalizeHex(value);
                if (horizonColor == normalized) return;
                horizonColor = normalized;
                SaveGlobal();
                RaiseColours();
            }
        }

        // What the swatches on the options page show: the picked colour if there is one,
        // otherwise what the current scheme resolves to - for the sun and the moon that is
        // a live N.I.N.A. theme brush, which is what "take from the N.I.N.A. palette" means.
        public Brush SunColorBrush => PlannerPalette.EffectiveSun(sunColor, scheme);
        public Brush MoonColorBrush => PlannerPalette.EffectiveMoon(moonColor, scheme);
        public Brush HorizonColorBrush => PlannerPalette.EffectiveHorizon(horizonColor, scheme);

        // The swatch buttons replaced the hex boxes, so the exact value the chart paints
        // moves to the tooltip: still discoverable, no longer a wide text field per role.
        public string SunColorNote => ColourNote("Sun path", sunColor, SunColorBrush);
        public string MoonColorNote => ColourNote("Moon track", moonColor, MoonColorBrush);
        public string HorizonColorNote => ColourNote("Horizon", horizonColor, HorizonColorBrush);

        private static string ColourNote(string role, string pinned, Brush effective) {
            var painted = effective is SolidColorBrush brush
                ? $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}"
                : "nothing yet";
            return pinned.Length == 0
                ? $"{role}: follows the palette, currently {painted}"
                : $"{role}: pinned to {pinned}, currently {painted}";
        }

        private IRelayCommand pickSun, pickMoon, pickHorizon, resetColours;

        /// <summary>Pick the sun curve colour.</summary>
        public IRelayCommand PickSunColorCommand =>
            pickSun ??= new RelayCommand(() => PickColour(sunColor, PlannerPalette.EffectiveSun(sunColor, scheme), c => SunColor = c));

        /// <summary>Pick the moon track colour.</summary>
        public IRelayCommand PickMoonColorCommand =>
            pickMoon ??= new RelayCommand(() => PickColour(moonColor, PlannerPalette.EffectiveMoon(moonColor, scheme), c => MoonColor = c));

        /// <summary>Pick the horizon line colour.</summary>
        public IRelayCommand PickHorizonColorCommand =>
            pickHorizon ??= new RelayCommand(() => PickColour(horizonColor, PlannerPalette.EffectiveHorizon(horizonColor, scheme), c => HorizonColor = c));

        /// <summary>
        /// Drop every picked colour. The sun and the moon go back to the live N.I.N.A.
        /// theme palette; the horizon goes back to the plugin's green, because red was
        /// the thing that made it read as one of the tracks.
        /// </summary>
        public void ResetColors() {
            sunColor = moonColor = horizonColor = string.Empty;
            SaveGlobal();
            RaiseColours();
        }

        /// <summary>Reset the three chart colours to their scheme defaults.</summary>
        public IRelayCommand ResetColorsCommand => resetColours ??= new RelayCommand(ResetColors, () => true);

        /// <summary>
        /// Open the colour dialog seeded with what the chart currently shows for that
        /// role, and store the choice as hex. The dialog comes from WinForms because WPF
        /// ships no colour picker; that is the only place in the plugin that uses it.
        /// </summary>
        private void PickColour(string hex, Brush fallback, Action<string> apply) {
            var seed = PlannerPalette.ParseHex(hex)?.Color
                ?? (fallback as SolidColorBrush)?.Color
                ?? Color.FromRgb(0xC0, 0xC0, 0xC0);
            using var dialog = new System.Windows.Forms.ColorDialog {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(seed.R, seed.G, seed.B),
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            apply($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        }

        private void RaiseColours() {
            RaisePropertyChanged(nameof(SunColor));
            RaisePropertyChanged(nameof(MoonColor));
            RaisePropertyChanged(nameof(HorizonColor));
            RaisePropertyChanged(nameof(SunColorBrush));
            RaisePropertyChanged(nameof(MoonColorBrush));
            RaisePropertyChanged(nameof(HorizonColorBrush));
            RaisePropertyChanged(nameof(SunColorNote));
            RaisePropertyChanged(nameof(MoonColorNote));
            RaisePropertyChanged(nameof(HorizonColorNote));
        }

        private static string ReadHex(JsonDocument doc, string name) =>
            doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? NormalizeHex(value.GetString())
                : string.Empty;

        /// <summary>
        /// Trim and add the missing # so a typed value, a picked value and the file all
        /// look the same. Deliberately does not reject nonsense: it gets ignored when
        /// drawn and stays editable.
        /// </summary>
        private static string NormalizeHex(string text) {
            var trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0) return string.Empty;
            return trimmed.StartsWith("#", StringComparison.Ordinal) ? trimmed : "#" + trimmed;
        }

        /// <summary>
        /// Latitude override. Empty means "take it from the profile's
        /// astrometry settings". Stored as an invariant-culture decimal string.
        /// </summary>
        public string SiteLatOverride {
            get => profileOptions.GetValueString(nameof(SiteLatOverride), string.Empty);
            set { profileOptions.SetValueString(nameof(SiteLatOverride), value ?? string.Empty); RaisePropertyChanged(); }
        }

        /// <summary>Longitude override; empty means "from the profile".</summary>
        public string SiteLonOverride {
            get => profileOptions.GetValueString(nameof(SiteLonOverride), string.Empty);
            set { profileOptions.SetValueString(nameof(SiteLonOverride), value ?? string.Empty); RaisePropertyChanged(); }
        }

        // ---- The two boxes on the options page --------------------------------------------
        // They edit drafts, never the stored pair. Until now the boxes wrote straight into
        // SiteLatOverride/SiteLonOverride, so a half-typed or mistyped pair sat in the
        // persisted settings while EffectiveSite() quietly fell back to the profile: the
        // chart moved or did not move with no explanation, and the only way out was to
        // notice which box was wrong. Apply writes a validated pair; Reset puts the profile
        // site in the boxes and clears the override, which is what "follow N.I.N.A." means.

        private string siteLatDraft = string.Empty, siteLonDraft = string.Empty, siteNotice = string.Empty;

        /// <summary>The latitude the box is showing. Nothing is stored until <see cref="ApplySite"/>.</summary>
        public string SiteLatDraft {
            get => siteLatDraft;
            set {
                var normalized = (value ?? string.Empty).Trim();
                if (siteLatDraft == normalized) return;
                siteLatDraft = normalized;
                RaiseSite();
            }
        }

        /// <summary>The longitude the box is showing. Nothing is stored until <see cref="ApplySite"/>.</summary>
        public string SiteLonDraft {
            get => siteLonDraft;
            set {
                var normalized = (value ?? string.Empty).Trim();
                if (siteLonDraft == normalized) return;
                siteLonDraft = normalized;
                RaiseSite();
            }
        }

        /// <summary>
        /// The line under the buttons: why the pair is not usable yet, or what Apply or
        /// Reset just did. There was no feedback at all before, which is why a rejected
        /// pair read as the plugin ignoring the user.
        /// </summary>
        public string SiteNotice {
            get => siteNotice;
            private set {
                if (siteNotice == value) return;
                siteNotice = value ?? string.Empty;
                RaisePropertyChanged();
            }
        }

        /// <summary>True while the planner is drawing with a pinned site rather than the profile's.</summary>
        public bool SiteOverrideActive => TryCoord(SiteLatOverride, out _) && TryCoord(SiteLonOverride, out _);

        /// <summary>
        /// What the planner is actually using, spelled out, because two text boxes that
        /// happen to be empty looked like "no site" even when the profile had one.
        /// </summary>
        public string SiteReadout {
            get {
                var site = EffectiveSite();
                if (site == null) {
                    return "No site in effect: this profile has no astrometry coordinates and nothing is pinned.";
                }

                var where = SiteOverrideActive ? "pinned here" : "this profile's astrometry settings";
                return $"Planner uses {FormatCoord(site.Value.Lat)}, {FormatCoord(site.Value.Lon)} - {where}";
            }
        }

        /// <summary>Both boxes hold a usable number, so Apply has something honest to store.</summary>
        public bool CanApplySite() => TryCoord(siteLatDraft, out _) && TryCoord(siteLonDraft, out _);

        private IRelayCommand applySite, resetSite;

        /// <summary>Store the pair in the boxes as this profile's pinned site.</summary>
        public IRelayCommand ApplySiteCommand => applySite ??= new RelayCommand(ApplySite, CanApplySite);

        /// <summary>Put this profile's site in the boxes and stop pinning one.</summary>
        public IRelayCommand ResetSiteCommand => resetSite ??= new RelayCommand(ResetSite);

        /// <summary>
        /// Write the drafted pair to the per-profile override. Out-of-range values are
        /// clamped to what a sphere can hold, and the notice says so rather than
        /// pretending the number typed is the number stored.
        /// </summary>
        public void ApplySite() {
            if (!TryCoord(siteLatDraft, out var lat) || !TryCoord(siteLonDraft, out var lon)) {
                SiteNotice = IncompleteSite;
                return;
            }

            var clamped = false;
            if (lat is < -90 or > 90) {
                lat = Math.Clamp(lat, -90, 90);
                clamped = true;
            }

            if (lon is < -180 or > 180) {
                lon = Math.Clamp(lon, -180, 180);
                clamped = true;
            }

            SiteLatDraft = FormatCoord(lat);
            SiteLonDraft = FormatCoord(lon);
            SiteLatOverride = SiteLatDraft;
            SiteLonOverride = SiteLonDraft;
            RaiseSiteState();
            SiteNotice = clamped
                ? $"Latitude and longitude are limited to +/-90 and +/-180: pinned at {FormatCoord(lat)}, {FormatCoord(lon)} instead."
                : $"Site pinned to {FormatCoord(lat)}, {FormatCoord(lon)}.";
        }

        /// <summary>
        /// Follow the profile again: the override is cleared and the profile's own
        /// coordinates are shown in the boxes, so the page says what is in effect instead
        /// of presenting two empty fields.
        /// </summary>
        public void ResetSite() {
            SiteLatOverride = string.Empty;
            SiteLonOverride = string.Empty;
            siteLatDraft = siteLonDraft = string.Empty;
            SeedSiteDrafts();
            RaiseSiteState();
            SiteNotice = string.IsNullOrEmpty(siteLatDraft)
                ? "Nothing to follow: this profile has no astrometry coordinates."
                : $"Following this profile's site {siteLatDraft}, {siteLonDraft}.";
        }

        private const string IncompleteSite =
            "Needs both a latitude and a longitude as decimal degrees, or Reset to follow this profile.";

        /// <summary>
        /// Start the boxes off showing whatever is in effect - a pinned pair, otherwise the
        /// profile's coordinates - so they are never blank while a site is being used.
        /// </summary>
        private void SeedSiteDrafts() {
            if (SiteOverrideActive) {
                siteLatDraft = SiteLatOverride;
                siteLonDraft = SiteLonOverride;
                return;
            }

            var site = EffectiveSite();
            if (site != null) {
                siteLatDraft = FormatCoord(site.Value.Lat);
                siteLonDraft = FormatCoord(site.Value.Lon);
            }
        }

        private static string FormatCoord(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private void RaiseSite() {
            RaiseSiteState();
            if (!CanApplySite() && (siteLatDraft.Length != 0 || siteLonDraft.Length != 0)) {
                SiteNotice = IncompleteSite;
            } else if (!string.IsNullOrEmpty(siteNotice) && siteNotice != IncompleteSite) {
                SiteNotice = string.Empty;
            }
        }

        /// <summary>Everything the page shows that depends on which site is in effect.</summary>
        private void RaiseSiteState() {
            RaisePropertyChanged(nameof(SiteLatDraft));
            RaisePropertyChanged(nameof(SiteLonDraft));
            RaisePropertyChanged(nameof(SiteReadout));
            RaisePropertyChanged(nameof(SiteOverrideActive));
            applySite?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Effective site: override if both fields parse, otherwise the
        /// profile's astrometry lat/lon. Null when neither is available.
        /// </summary>
        public (double Lat, double Lon)? EffectiveSite() {
            var latText = SiteLatOverride;
            var lonText = SiteLonOverride;
            if (TryCoord(latText, out var lat) && TryCoord(lonText, out var lon))
                return (lat, lon);
            var astro = profiles.ActiveProfile?.AstrometrySettings;
            if (astro != null && double.IsFinite(astro.Latitude) && double.IsFinite(astro.Longitude))
                return (astro.Latitude, astro.Longitude);
            return null;
        }

        private static bool TryCoord(string text, out double value) {
            value = 0;
            return !string.IsNullOrWhiteSpace(text) &&
                double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                double.IsFinite(value);
        }

        private void SaveGlobal() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(globalPath));
                var payload = JsonSerializer.Serialize(new { sunColor, moonColor, horizonColor });
                var temporaryPath = globalPath + ".tmp";
                File.WriteAllText(temporaryPath, payload);
                File.Move(temporaryPath, globalPath, true);
            } catch (IOException) {
                // Settings persistence is best-effort; in-memory state stays correct.
            }
        }

        private void OnProfileChanged(object sender, EventArgs args) {
            RaisePropertyChanged(nameof(SiteLatOverride));
            RaisePropertyChanged(nameof(SiteLonOverride));
            // A different profile means a different site to follow, so the boxes move with
            // it - unless this profile pins its own.
            if (!SiteOverrideActive) {
                siteLatDraft = siteLonDraft = string.Empty;
                SeedSiteDrafts();
            }

            siteNotice = string.Empty;
            RaiseSiteState();
            RaisePropertyChanged(nameof(SiteNotice));
        }

        public void Dispose() {
            if (!initialized) return;
            profiles.ProfileChanged -= OnProfileChanged;
            initialized = false;
            // Stop handing out a stopped object: if the plugin is reloaded within one run of
            // N.I.N.A., For() builds a fresh one that reads the file again.
            lock (SharedGate) {
                if (ReferenceEquals(Shared, this)) Shared = null;
            }
        }
    }
}
