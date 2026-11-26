using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// Chart palette, asked by <see cref="ChartScheme"/>. Default borrows the live
    /// N.I.N.A. theme brushes by key (BackgroundBrush, PrimaryBrush, ...), so the panel
    /// follows whatever palette the user picked in the N.I.N.A. settings. Night swaps
    /// every role to a red night-vision hue at comparable lightness - same idea as the
    /// Unphased web app's BLUE/RED palette swap. Enhanced is the plugin's own navy chart,
    /// where the sky roles are separated by hue because their luminance is deliberately
    /// close (see the measured values on the Enhanced brushes below).
    ///
    /// Three roles - the sun track, the moon track and the horizon - can be
    /// overridden by colour pickers in the plugin options (hex strings; empty
    /// means "use the scheme default"). Overrides live on the palette instance,
    /// so the scheme itself stays untouched and "reset" is simply clearing them.
    /// </summary>
    public sealed class PlannerPalette {
        private static readonly SolidColorBrush RedChart = Freeze(new SolidColorBrush(Color.FromRgb(0x14, 0x04, 0x04)));
        private static readonly SolidColorBrush RedCurve = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x6A)));
        private static readonly SolidColorBrush RedMoon = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0xD2)));
        private static readonly SolidColorBrush RedGrid = Freeze(new SolidColorBrush(Color.FromArgb(0x8C, 0x5C, 0x1A, 0x1A)));
        private static readonly SolidColorBrush RedAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x3B)));

        // The horizon needs to read as the reference line of the whole chart, so it
        // gets its own, stronger ink instead of the faint gridline colour.
        private static readonly SolidColorBrush RedHorizon = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x64, 0x64)));

        // The horizon is the one line the chart is measured against, and it used to be
        // the theme's SecondaryBrush - red on N.I.N.A.'s default palette, where the moon
        // track is red too and the two read as the same object. A mid green sits well on
        // both the dark and the light N.I.N.A. themes and is not used by any other role.
        // Night mode keeps its red horizon: a green line in an all-red chart defeats the
        // point of the red chart.
        private static readonly SolidColorBrush GreenHorizon = Freeze(new SolidColorBrush(Color.FromRgb(0x4F, 0xC8, 0x7C)));

        // ---- The Enhanced scheme -------------------------------------------------------
        // A navy chart on which the sky roles are told apart by hue, not brightness: sun
        // amber, moon pale blue, horizon teal, scrub orange. The values were picked by
        // measuring WCAG contrast against the canvas rather than by eye - sun 10.31:1,
        // moon 11.05:1, horizon 9.27:1, scrub 7.69:1, ink 16.82:1, so every role clears
        // the 4.5:1 body-text bar and most clear 7:1. The gridline is 1.36:1 on purpose:
        // it is scaffolding, and a visible gridline would compete with the curves.
        //
        // The curve pairs sit only 1.07-1.19 apart in luminance. That is deliberate - the
        // N.I.N.A. palette can put a pale yellow moon track next to a near-white sun curve
        // and a red-green viewer sees one line - so here the three lines separate by hue
        // while staying equally bright.
        private static readonly SolidColorBrush EnhancedCanvas = Freeze(new SolidColorBrush(Color.FromRgb(0x0B, 0x10, 0x20)));
        private static readonly SolidColorBrush EnhancedSun = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x42)));
        private static readonly SolidColorBrush EnhancedMoon = Freeze(new SolidColorBrush(Color.FromRgb(0x9B, 0xD0, 0xF5)));
        private static readonly SolidColorBrush EnhancedHorizon = Freeze(new SolidColorBrush(Color.FromRgb(0x45, 0xD0, 0xA0)));
        private static readonly SolidColorBrush EnhancedScrub = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x5C)));
        private static readonly SolidColorBrush EnhancedInk = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF7)));
        private static readonly SolidColorBrush EnhancedGrid = Freeze(new SolidColorBrush(Color.FromRgb(0x2C, 0x3A, 0x5C)));
        private static readonly SolidColorBrush EnhancedChip = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x38)));

        // Label backing: near-black and almost opaque, so a word reads the same over
        // the bright daylight band and over full night, in either of the two schemes
        // that sit on top of a theme background. Enhanced has its own card colour below.
        private static readonly SolidColorBrush Chip = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0x00, 0x00, 0x00)));

        // Day -> civil -> nautical -> astronomical twilight overlays, in drawing order.
        // Alpha overlays so they read on both a light and a dark N.I.N.A. theme; below
        // -18 deg the base background is left alone, which is the darkest layer.
        private static readonly Brush[] TwilightOverlays = {
            Overlay(0x3D, 0xFF, 0xD9, 0x8A),   // sun up
            Overlay(0x2E, 0x86, 0xB8, 0xE8),   // civil  0 .. -6
            Overlay(0x24, 0x4A, 0x76, 0xB0),   // nautical -6 .. -12
            Overlay(0x1A, 0x24, 0x3C, 0x64),   // astronomical -12 .. -18
        };

        // Same four bands in the red night-vision scheme.
        private static readonly Brush[] TwilightOverlaysRed = {
            Overlay(0x3D, 0xFF, 0xB4, 0x5A),
            Overlay(0x2E, 0xFF, 0x8A, 0x6A),
            Overlay(0x24, 0xE0, 0x50, 0x40),
            Overlay(0x1A, 0xA8, 0x2E, 0x26),
        };

        // And in Enhanced, where they cannot be alpha overlays: the other two schemes lay
        // translucent washes over a background that comes from wherever the user's theme
        // happens to be, so the blend is unknown. Enhanced's canvas is a fixed navy, so the
        // bands are solid steps down to it - 1.10, 1.04 and 1.03 in luminance, which is
        // shading you read as depth rather than stripes you read as data.
        private static readonly Brush[] TwilightOverlaysEnhanced = {
            Freeze(Color.FromRgb(0x16, 0x23, 0x3F)),   // sun up
            Freeze(Color.FromRgb(0x12, 0x1C, 0x33)),   // civil  0 .. -6
            Freeze(Color.FromRgb(0x0E, 0x16, 0x28)),   // nautical -6 .. -12
            EnhancedCanvas,                            // astronomical -12 .. -18: the canvas itself
        };

        private static SolidColorBrush Overlay(byte a, byte r, byte g, byte b) =>
            Freeze(new SolidColorBrush(Color.FromArgb(a, r, g, b)));

        private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
        private static SolidColorBrush Freeze(Color c) => Freeze(new SolidColorBrush(c));

        private static Brush Theme(string key, string fallbackKey) {
            if (Application.Current != null && Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key] as Brush
                    ?? TryTheme(fallbackKey);
            return TryTheme(fallbackKey);
        }

        private static Brush TryTheme(string key) =>
            Application.Current?.Resources.Contains(key) == true
                ? Application.Current.Resources[key] as Brush ?? Brushes.Gray
                : Brushes.Gray;

        /// <summary>Chart canvas background (deepest panel layer).</summary>
        public Brush ChartBackground(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedChart,
            ChartScheme.Enhanced => EnhancedCanvas,
            _ => Theme("TertiaryBackgroundBrush", "BackgroundBrush"),
        };

        /// <summary>Text, axis labels and degree numbers.</summary>
        public Brush Ink(ChartScheme scheme) => InkDefault(scheme);

        /// <summary>
        /// The sun curve. Its own role rather than Ink, so picking a sun colour in the
        /// options repaints the curve and leaves the text alone.
        /// </summary>
        public Brush SunTrack(ChartScheme scheme) => sunOverride ?? SunDefault(scheme);

        /// <summary>The moon track - lighter than the ink but same family by default.</summary>
        public Brush MoonTrack(ChartScheme scheme) => moonOverride ?? MoonDefault(scheme);

        private static Brush InkDefault(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedCurve,
            ChartScheme.Enhanced => EnhancedInk,
            _ => Theme("PrimaryBrush", "ButtonForegroundBrush"),
        };

        // Enhanced breaks the habit of taking the sun curve from the ink: on navy the ink is
        // near-white, and a near-white curve next to the pale blue moon track is the same
        // collision the N.I.N.A. palette can produce. Amber keeps the sun on its own. The
        // other two palettes still draw the sun in the ink colour, which is how the chart
        // looked before this palette existed.
        private static Brush SunDefault(ChartScheme scheme) =>
            scheme == ChartScheme.Enhanced ? EnhancedSun : InkDefault(scheme);

        private static Brush MoonDefault(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedMoon,
            ChartScheme.Enhanced => EnhancedMoon,
            _ => Theme("SecondaryBrush", "ButtonForegroundBrush"),
        };

        // The three pickable colours, from the plugin options, parsed once per change.
        // Null means "no override": the role follows the scheme.
        private SolidColorBrush sunOverride, moonOverride, horizonOverride;

        /// <summary>
        /// Take the pickable colours from the plugin options (hex strings; empty means
        /// "follow the scheme"). The chart control calls this whenever the options
        /// change, and the settings page uses the same defaults for its swatches.
        /// </summary>
        public void SetOverrides(string sun, string moon, string horizon) {
            sunOverride = ParseHex(sun);
            moonOverride = ParseHex(moon);
            horizonOverride = ParseHex(horizon);
        }

        /// <summary>
        /// A colour typed into the options as a frozen brush: accepts #rgb, #rrggbb and
        /// #aarrggbb, with or without the leading #. Null for empty or malformed input,
        /// which is how "no override" and a typo are both handled - a bad colour must
        /// never take the chart down.
        /// </summary>
        public static SolidColorBrush ParseHex(string hex) {
            var text = (hex ?? string.Empty).Trim();
            if (text.StartsWith("#")) text = text.Substring(1);
            byte a = 0xFF, r, g, b;
            switch (text.Length) {
                case 3:
                    if (!byte.TryParse(text.Substring(0, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)) return null;
                    if (!byte.TryParse(text.Substring(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)) return null;
                    if (!byte.TryParse(text.Substring(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return null;
                    r = (byte)(r * 0x11); g = (byte)(g * 0x11); b = (byte)(b * 0x11);
                    break;
                case 6:
                case 8:
                    int offset = text.Length == 8 ? 2 : 0;
                    if (text.Length == 8 && !byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a)) return null;
                    if (!byte.TryParse(text.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)) return null;
                    if (!byte.TryParse(text.Substring(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)) return null;
                    if (!byte.TryParse(text.Substring(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return null;
                    break;
                default:
                    return null;
            }
            return Freeze(new SolidColorBrush(Color.FromArgb(a, r, g, b)));
        }

        /// <summary>
        /// The colour a swatch in the options should show for one role: the override if
        /// there is one, otherwise what the current scheme resolves to - which for the
        /// sun and the moon is a live N.I.N.A. theme brush.
        /// </summary>
        public static Brush Effective(string hex, ChartScheme scheme, Func<ChartScheme, Brush> fallback) =>
            ParseHex(hex) ?? fallback(scheme);

        public static Brush EffectiveSun(string hex, ChartScheme scheme) => Effective(hex, scheme, SunDefault);
        public static Brush EffectiveMoon(string hex, ChartScheme scheme) => Effective(hex, scheme, MoonDefault);
        public static Brush EffectiveHorizon(string hex, ChartScheme scheme) => Effective(hex, scheme, HorizonDefault);

        /// <summary>Faint gridlines and band separators.</summary>
        public Brush Gridline(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedGrid,
            ChartScheme.Enhanced => EnhancedGrid,
            _ => Theme("BorderBrush", "AltBorderBrush"),
        };

        /// <summary>Selection markers (selected night, sunset/sunrise ticks).</summary>
        public Brush Accent(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedAccent,
            // Same orange as the scrub line: in Enhanced both mean "this is the marker",
            // and a second marker colour on a navy chart would be a fourth hue to learn.
            ChartScheme.Enhanced => EnhancedScrub,
            _ => Theme("ButtonBackgroundSelectedBrush", "ButtonSelectBrush"),
        };

        /// <summary>
        /// The scrub line, its handle and its readout. Its own role rather than
        /// Accent, because it is the one vertical on the chart and it is a control, not
        /// a piece of the sky - it has to read as "the thing I moved".
        /// </summary>
        public Brush Scrub(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedMoon,
            ChartScheme.Enhanced => EnhancedScrub,
            _ => Theme("ButtonBackgroundSelectedBrush", "ButtonSelectBrush"),
        };

        /// <summary>
        /// Which twilight band a sun altitude sits in: 0 daylight, 1 civil (0..-6),
        /// 2 nautical (-6..-12), 3 astronomical (-12..-18), -1 full night. Exposed as
        /// a number so the chart can merge consecutive samples of one band into a
        /// single background run instead of one rectangle per sample.
        /// </summary>
        public static int TwilightBand(double sunAltitude) {
            if (sunAltitude > 0) return 0;
            if (sunAltitude > -6) return 1;
            if (sunAltitude > -12) return 2;
            if (sunAltitude > -18) return 3;
            return -1;
        }

        /// <summary>Overlay for one band; null is full night, left as the base background.</summary>
        public Brush TwilightBandBrush(ChartScheme scheme, int band) {
            if (band < 0) return null;
            var table = scheme switch {
                ChartScheme.Night => TwilightOverlaysRed,
                ChartScheme.Enhanced => TwilightOverlaysEnhanced,
                _ => TwilightOverlays,
            };
            return band < table.Length ? table[band] : null;
        }

        /// <summary>
        /// Background overlay for the band a sun altitude falls in: daylight,
        /// civil, nautical or astronomical twilight. Null means astronomical
        /// night, where the chart background is left as the darkest layer.
        /// </summary>
        public Brush Twilight(ChartScheme scheme, double sunAltitude) => TwilightBandBrush(scheme, TwilightBand(sunAltitude));

        /// <summary>
        /// The horizon (alt 0) line: stronger than the twilight gridlines, and the line
        /// every degree on the chart is measured from.
        /// </summary>
        public Brush Horizon(ChartScheme scheme) => horizonOverride ?? HorizonDefault(scheme);

        /// <summary>The horizon colour when nothing has been picked for it.</summary>
        public static Brush HorizonDefault(ChartScheme scheme) => scheme switch {
            ChartScheme.Night => RedHorizon,
            ChartScheme.Enhanced => EnhancedHorizon,
            _ => GreenHorizon,
        };

        /// <summary>
        /// The red night-vision palette for the panel's **chrome**, keyed by the brush
        /// names N.I.N.A. itself defines. The Night scheme installs them; Enhanced is a
        /// chart-only palette, so its panel keeps the user's own N.I.N.A. colours rather
        /// than becoming a third look the rest of the application does not share.
        /// The hexes are copied from the built-in scheme
        /// N.I.N.A. calls "Dark", which is the one its own night-vision eye button
        /// switches the whole application to - so the panel matches the desktop app
        /// rather than an approximation of it. PlannerViewGlue.NightPalette installs them
        /// in the panel's own resource dictionary, and a DynamicResource lookup walks out
        /// from the element, so they apply inside this panel and nowhere else.
        ///
        /// Two things worth knowing about the source scheme, both left as they are:
        /// PrimaryColor/BorderColor are a dark red (#550C18) rather than a bright one,
        /// and SecondaryColor is navy - the desktop night palette is not purely red.
        /// Only what the plugin draws follows these: N.I.N.A.'s own control styles bound
        /// their colours to the application's palette when the application loaded, so a
        /// plugin cannot recolour the button style itself without editing that palette,
        /// which would change every other tab too.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Brush> NightChrome =
            new Dictionary<string, Brush> {
                ["BackgroundBrush"] = Freeze(Color.FromArgb(0xFF, 0x02, 0x01, 0x0A)),
                ["SecondaryBackgroundBrush"] = Freeze(Color.FromArgb(0xFF, 0x23, 0x04, 0x09)),
                ["TertiaryBackgroundBrush"] = Freeze(Color.FromArgb(0xFF, 0x2D, 0x06, 0x0D)),
                ["PrimaryBrush"] = Freeze(Color.FromArgb(0xFF, 0x55, 0x0C, 0x18)),
                ["SecondaryBrush"] = Freeze(Color.FromArgb(0xFF, 0x1B, 0x2A, 0x41)),
                ["BorderBrush"] = Freeze(Color.FromArgb(0xFF, 0x55, 0x0C, 0x18)),
                ["ButtonForegroundBrush"] = Freeze(Color.FromArgb(0xFF, 0x02, 0x01, 0x0A)),
                ["ButtonForegroundDisabledBrush"] = Freeze(Color.FromArgb(0xFF, 0x44, 0x37, 0x30)),
            };

        /// <summary>
        /// Backing behind chart labels. Near-black and almost opaque in the two schemes
        /// that sit on top of a theme background; in Enhanced, whose canvas is a known
        /// navy, it is a solid navy a step lighter than the canvas so a word still has a
        /// card under it without turning into a black box on blue.
        /// </summary>
        public Brush ChipFill(ChartScheme scheme) => scheme == ChartScheme.Enhanced ? EnhancedChip : Chip;
    }
}
