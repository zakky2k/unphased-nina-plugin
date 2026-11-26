// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Windows;
using System.Windows.Media;

namespace Zakfo.Unphased.Features.planner {
    internal static class DeepSkyGlyph {
        internal const double Radius = 7.5;

        private const string CloudOutline = "M2,11 Q0,8 4,6 Q4,3 8,4 Q12,2 13,6 Q17,8 13,11 Q12,14 8,12 Q5,15 2,11 Z";
        private const string RingOutline = "M8,2 A6,6 0 1 1 8,14 A6,6 0 1 1 8,2 Z";
        private const string StarOutline = "M8,2 9.5,6.5 14,8 9.5,9.5 8,14 6.5,9.5 2,8 6.5,6.5 Z";
        private static readonly Geometry Cloud = Path(CloudOutline);
        private static readonly Geometry Ring = Path(RingOutline);
        private static readonly Geometry Galaxy = Path("M2,11 C0,7 11,1 14,5 C16,9 5,15 2,11 Z");
        private static readonly Geometry GalaxyGroup = Path("M1,8 C0,5 8,1 10,4 C11,7 3,11 1,8 Z M6,12 C5,9 13,5 15,8 C16,11 8,15 6,12 Z");
        private static readonly Geometry Globular = Path(RingOutline + " M4,8 H12 M8,4 V12");
        private static readonly Geometry Star = Path(StarOutline);
        private static readonly Geometry DoubleStar = Path("M5,2 6,5 9,6 6,7 5,10 4,7 1,6 4,5 Z M12,7 13,10 16,11 13,12 12,15 11,12 8,11 11,10 Z");
        private static readonly Geometry Remnant = Path("M8,2 10,5 14,4 12,8 15,11 10,11 8,15 6,11 1,11 4,8 2,4 6,5 Z");
        private static readonly Geometry Crosshair = Path("M2,8 H6 M10,8 H14 M8,2 V6 M8,10 V14");
        private static readonly Point[] OpenCluster = { new(4, 5), new(10, 3), new(8, 8), new(13, 9), new(5, 12), new(11, 13) };
        private static readonly Point[] EmbeddedCluster = { new(5, 8), new(8, 10), new(11, 7) };
        private static readonly Point[] CentralStar = { new(8, 8) };

        internal static void Draw(DrawingContext dc, string objectType, Point centre, double radius, Brush ink, Brush background) {
            var type = objectType?.Trim().ToUpperInvariant();
            (Geometry outline, Point[] dots) = type switch {
                "GALXY" => (Galaxy, null),
                "GALCL" => (GalaxyGroup, null),
                "BRTNB" or "DRKNB" or "LMCDN" or "SMCDN" or "GX+DN" => (Cloud, null),
                "PLNNB" or "QUASR" => (Ring, CentralStar),
                "GLOCL" or "LMCGC" or "SMCGC" or "GX+GC" => (Globular, null),
                "OPNCL" or "LMCOC" or "SMCOC" or "ASTER" or "3STAR" or "4STAR" or "8STAR" => (null, OpenCluster),
                "CL+NB" or "LMCCN" or "SMCCN" or "G+C+N" => (Cloud, EmbeddedCluster),
                "1STAR" => (Star, null),
                "2STAR" => (DoubleStar, null),
                "SNREM" => (Remnant, null),
                _ => (Crosshair, null)
            };
            var pen = new Pen(ink, 1.45) {
                StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round
            };
            if (type == "DRKNB") pen.DashStyle = DashStyles.Dash;

            double scale = radius / 8;
            dc.DrawEllipse(background, null, centre, radius + 1, radius + 1);
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, centre.X - radius, centre.Y - radius));
            if (outline != null) dc.DrawGeometry(null, pen, outline);
            if (dots != null) {
                foreach (var dot in dots) dc.DrawEllipse(ink, null, dot, 1.1, 1.1);
            }
            dc.Pop();
        }

        private static Geometry Path(string data) {
            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            return geometry;
        }
    }
}
