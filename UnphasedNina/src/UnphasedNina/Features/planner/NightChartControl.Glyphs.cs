// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Astrometry;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Zakfo.Unphased.Features.planner {
    public enum PlanetGlyphStyle { Basic, Phase, Detailed }

    internal readonly record struct BodyIllumination(double Fraction, double BrightSideDegrees);

    public partial class NightChartControl {
        private readonly record struct BodyMarker(NOVAS.Body Body, double Y, Brush Ink, BodyIllumination? Phase);
        private static readonly Typeface SymbolTypeface = new("Segoe UI Symbol");

        private void DrawScrubGlyphs(DrawingContext dc, double width, double height, double sy,
            double x, double minutes, DateTimeOffset instant) {
            var bodies = new List<BodyMarker>();
            double moonY = NightChartVM.YMoon(row.Chart.MoonAltitudeAt(instant)) * sy;
            if (Shows(ChartElements.MoonGlyph) && InChart(moonY, height)) {
                var (fraction, litOnRight) = row.Chart.ScrubMoonGlyph(minutes);
                bodies.Add(new BodyMarker(NOVAS.Body.Moon, moonY, palette.MoonTrack(scheme),
                    new BodyIllumination(fraction, litOnRight ? 0 : 180)));
            }
            if (Shows(ChartElements.SunGlyph)) {
                double sunY = NightChartVM.YSun(row.Chart.SunAltitudeAt(instant)) * sy;
                if (InChart(sunY, height)) bodies.Add(new BodyMarker(NOVAS.Body.Sun, sunY, palette.SunTrack(scheme), null));
            }
            foreach (var overlay in VisibleOverlays) {
                double y = NightChartVM.YSun(NightChartVM.AltitudeAt(overlay.Samples, instant)) * sy;
                if (!InChart(y, height)) continue;
                var ink = OverlayStyle.BrushFor(overlay.Choice.StyleIndex, scheme);
                if (overlay.Choice.Planet is { } planet) {
                    var phase = overlay.Phases.TryGetValue(instant, out var illumination) ? illumination : (BodyIllumination?)null;
                    bodies.Add(new BodyMarker(planet, y, ink, phase));
                } else {
                    DrawScrubDot(dc, x, y, ink, height);
                }
            }
            if (bodies.Count == 0) return;
            var style = hookedPlanner?.GlyphStyle ?? PlanetGlyphStyle.Phase;
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            for (int i = bodies.Count - 1; i >= 0; i--) {
                var body = bodies[i];
                double nominal = GlyphRadius(style, body.Body);
                double separation = double.PositiveInfinity;
                for (int j = 0; j < bodies.Count; j++)
                    if (j != i) separation = Math.Min(separation, Math.Abs(body.Y - bodies[j].Y));
                // The Moon draws last when two centres nearly coincide. All values remain in the scrub legend.
                double radius = Math.Clamp(separation / 2 - 1, 2.4, nominal);
                if (radius <= 3) DrawScrubDot(dc, x, body.Y, body.Ink, height);
                else DrawBodyGlyph(dc, body.Body, new Point(x, body.Y), radius, body.Ink, style, body.Phase);
            }
            dc.Pop();
        }

        private static bool InChart(double y, double height) => double.IsFinite(y) && y >= 0 && y <= height;

        internal static double GlyphRadius(PlanetGlyphStyle style, NOVAS.Body body) => style switch {
            PlanetGlyphStyle.Basic => 6.5,
            PlanetGlyphStyle.Detailed when body == NOVAS.Body.Saturn => 10,
            PlanetGlyphStyle.Detailed => 11.5,
            _ => MoonGlyphRadius
        };

        internal void DrawBodyGlyph(DrawingContext dc, NOVAS.Body body, Point centre, double radius,
            Brush ink, PlanetGlyphStyle style, BodyIllumination? phase) {
            var background = palette.ChipFill(scheme);
            if (style == PlanetGlyphStyle.Phase && body == NOVAS.Body.Moon && phase is { } lunar) {
                dc.DrawEllipse(background, new Pen(ink, 0.8), centre, radius + 2, radius + 2);
                DrawMoonGlyph(dc, centre.X, centre.Y, radius, lunar.Fraction, lunar.BrightSideDegrees == 0);
                return;
            }
            DrawPlanetGlyph(dc, body, centre, radius, ink, background, style, phase, scheme == ChartScheme.Night,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        internal static void DrawPlanetGlyph(DrawingContext dc, NOVAS.Body body, Point centre, double radius,
            Brush ink, Brush background, PlanetGlyphStyle style, BodyIllumination? phase, bool night, double pixelsPerDip) {
            if (style == PlanetGlyphStyle.Basic) {
                DrawSymbol(dc, body, centre, radius, ink, background, pixelsPerDip);
                return;
            }
            bool detailed = style == PlanetGlyphStyle.Detailed;
            var (surface, feature, highlight) = detailed ? BodyColours(body, night) : (ink, background, ink);
            var outline = new Pen(ink, 0.75);
            if (body == NOVAS.Body.Saturn) DrawRings(dc, centre, radius, highlight, false);
            dc.DrawEllipse(background, outline, centre, radius, radius);
            var lit = IlluminatedDisc(centre, radius, phase);
            if (lit != null) {
                dc.DrawGeometry(surface, null, lit);
                if (detailed) {
                    dc.PushClip(lit);
                    DrawDiscDetails(dc, body, centre, radius, feature, highlight);
                    dc.Pop();
                }
            }
            if (body == NOVAS.Body.Saturn) DrawRings(dc, centre, radius, highlight, true);
        }

        private static void DrawSymbol(DrawingContext dc, NOVAS.Body body, Point centre, double radius,
            Brush ink, Brush background, double pixelsPerDip) {
            string symbol = body switch {
                NOVAS.Body.Sun => "☉",
                NOVAS.Body.Mercury => "☿",
                NOVAS.Body.Venus => "♀",
                NOVAS.Body.Mars => "♂",
                NOVAS.Body.Jupiter => "♃",
                NOVAS.Body.Saturn => "♄",
                NOVAS.Body.Uranus => "♅",
                NOVAS.Body.Neptune => "♆",
                _ => "☽"
            };
            var text = new FormattedText(symbol, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                SymbolTypeface, radius * 2, ink, pixelsPerDip);
            var geometry = text.BuildGeometry(new Point());
            var bounds = geometry.Bounds;
            dc.DrawEllipse(background, null, centre, radius + .5, radius + .5);
            if (bounds.IsEmpty) return;
            double scale = 1.8 * radius / Math.Max(bounds.Width, bounds.Height);
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale,
                centre.X - (bounds.X + bounds.Width / 2) * scale, centre.Y - (bounds.Y + bounds.Height / 2) * scale));
            dc.DrawGeometry(ink, null, geometry);
            dc.Pop();
        }

        internal static Geometry IlluminatedDisc(Point centre, double radius, BodyIllumination? phase) {
            if (phase == null || phase.Value.Fraction >= .999) return new EllipseGeometry(centre, radius, radius);
            if (phase.Value.Fraction <= .01) return null;
            var mask = MoonPhaseGeometry(centre.X, centre.Y, radius, phase.Value.Fraction, true).Clone();
            mask.Transform = new RotateTransform(phase.Value.BrightSideDegrees, centre.X, centre.Y);
            return mask;
        }

        private static void DrawRings(DrawingContext dc, Point centre, double radius, Brush ink, bool front) {
            dc.PushTransform(new RotateTransform(-23, centre.X, centre.Y));
            if (front) dc.PushClip(new RectangleGeometry(new Rect(centre.X - 1.65 * radius, centre.Y, 3.3 * radius, radius)));
            dc.DrawEllipse(null, new Pen(ink, radius * .16), centre, radius * 1.45, radius * .40);
            if (front) dc.Pop();
            dc.Pop();
        }

        private static (Brush Surface, Brush Feature, Brush Highlight) BodyColours(NOVAS.Body body, bool night) {
            var (surface, feature, highlight) = body switch {
                NOVAS.Body.Sun => (0xF6AB40, 0xA65320, 0xFFD97A),
                NOVAS.Body.Mercury => (0xACA299, 0x716560, 0xD8CEC3),
                NOVAS.Body.Venus => (0xE7C998, 0xBC925E, 0xFFF0C6),
                NOVAS.Body.Mars => (0xCA714A, 0x854739, 0xF5E9D9),
                NOVAS.Body.Jupiter => (0xE7BE96, 0xAA694D, 0xF4DDC0),
                NOVAS.Body.Saturn => (0xD9B178, 0x9F794F, 0xF5DEB0),
                NOVAS.Body.Uranus => (0x8BD4D1, 0x5BA6A8, 0xCCF0E9),
                NOVAS.Body.Neptune => (0x5983C4, 0x365A91, 0x9DBDE9),
                _ => (0xCDCDCF, 0x858690, 0xEBEBEE)
            };
            return (ArtworkBrush(surface, night), ArtworkBrush(feature, night), ArtworkBrush(highlight, night));
        }

        private static Brush ArtworkBrush(int rgb, bool night) {
            byte red = (byte)(rgb >> 16), green = (byte)(rgb >> 8), blue = (byte)rgb;
            if (night) {
                var luminance = .2126 * red + .7152 * green + .0722 * blue;
                return new SolidColorBrush(Color.FromRgb((byte)(55 + luminance * .72), 12, 11));
            }
            return new SolidColorBrush(Color.FromRgb(red, green, blue));
        }

        private static void DrawDiscDetails(DrawingContext dc, NOVAS.Body body, Point centre, double radius,
            Brush feature, Brush highlight) {
            void Spot(Brush brush, double x, double y, double rx, double ry) =>
                dc.DrawEllipse(brush, null, new Point(centre.X + x * radius, centre.Y + y * radius), rx * radius, ry * radius);
            switch (body) {
                case NOVAS.Body.Sun:
                    Spot(highlight, -.24, -.26, .48, .40);
                    Spot(feature, .30, .16, .13, .10);
                    Spot(feature, .47, .26, .08, .07);
                    Spot(highlight, -.31, .45, .19, .11);
                    break;
                case NOVAS.Body.Moon:
                case NOVAS.Body.Mercury:
                    Spot(feature, -.28, -.24, .32, .24);
                    Spot(feature, .34, .37, .20, .17);
                    Spot(feature, -.48, .48, .13, .11);
                    Spot(highlight, .32, -.42, .17, .13);
                    Spot(highlight, .31, .31, .11, .06);
                    break;
                case NOVAS.Body.Venus:
                    Spot(feature, -.15, -.29, 1.12, .13);
                    Spot(highlight, .12, -.20, 1.02, .12);
                    Spot(feature, -.18, .33, 1.05, .10);
                    Spot(highlight, .32, .4, .9, .09);
                    break;
                case NOVAS.Body.Mars:
                    Spot(feature, -.36, .20, .57, .21);
                    Spot(feature, .29, .40, .34, .14);
                    Spot(highlight, .05, -.87, .47, .19);
                    break;
                case NOVAS.Body.Jupiter:
                    Spot(feature, 0, -.36, 1.2, .12);
                    Spot(highlight, 0, -.08, 1.2, .10);
                    Spot(feature, 0, .28, 1.2, .15);
                    Spot(feature, .43, .41, .22, .12);
                    break;
                case NOVAS.Body.Saturn:
                    Spot(feature, 0, -.40, 1.2, .12);
                    Spot(highlight, 0, .28, 1.2, .10);
                    break;
                default:
                    Spot(feature, .05, .33, 1.05, .13);
                    Spot(highlight, -.15, -.45, .8, .08);
                    break;
            }
        }
    }
}
