// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CosineKitty;
using NINA.Astrometry;
using NINA.Core.Utility;
using System.Windows;
using System.Windows.Media;
using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    internal sealed record FramingTarget(string Id, string Name, double RightAscensionHours, double DeclinationDegrees,
        string ObjectType = null) {
        public bool IsValid => !string.IsNullOrWhiteSpace(Name)
            && double.IsFinite(RightAscensionHours) && RightAscensionHours >= 0 && RightAscensionHours < 24
            && double.IsFinite(DeclinationDegrees) && Math.Abs(DeclinationDegrees) <= 90;

        public static FramingTarget FromObject(DeepSkyObject selected) {
            if (selected?.Coordinates == null || string.IsNullOrWhiteSpace(selected.Name) && string.IsNullOrWhiteSpace(selected.Id))
                return null;
            var coordinates = selected.Coordinates.Transform(Epoch.J2000);
            var target = new FramingTarget(selected.Id, string.IsNullOrWhiteSpace(selected.Name) ? selected.Id : selected.Name,
                coordinates.RA, coordinates.Dec, selected.DSOType);
            return target.IsValid ? target : null;
        }

        public bool Matches(FramingTarget other) =>
            (string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Id)
                || string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase))
            && Math.Abs(RightAscensionHours - other.RightAscensionHours) < 1e-6
            && Math.Abs(DeclinationDegrees - other.DeclinationDegrees) < 1e-5;
    }

    public sealed class OverlayChoice : BaseINPC {
        internal OverlayChoice(string name, int styleIndex, NOVAS.Body? planet = null, FramingTarget target = null) {
            Name = name;
            StyleIndex = styleIndex;
            Planet = planet;
            Target = target;
        }

        public string Name { get; }
        internal int StyleIndex { get; }
        internal NOVAS.Body? Planet { get; }
        internal FramingTarget Target { get; private set; }

        internal void SetTargetType(string objectType) {
            Target = Target with { ObjectType = objectType };
            RaisePropertyChanged(nameof(Target));
        }

        private bool enabled;
        public bool Enabled {
            get => enabled;
            set {
                if (enabled == value) return;
                enabled = value;
                RaisePropertyChanged();
            }
        }
    }

    internal sealed record OverlayTrack(OverlayChoice Choice, IReadOnlyList<(DateTimeOffset Time, double Altitude)> Samples,
        string Polyline, IReadOnlyDictionary<DateTimeOffset, BodyIllumination> Phases) {
        internal BodyIllumination? PhaseAt(DateTimeOffset time) {
            if (Phases.Count == 0) return null;
            var start = Samples[0].Time;
            double hours = (time - start).TotalHours;
            var lower = start.AddHours(Math.Floor(hours));
            bool hasBefore = Phases.TryGetValue(lower, out var before);
            bool hasAfter = Phases.TryGetValue(lower.AddHours(1), out var after);
            if (!hasBefore) return hasAfter ? after : null;
            if (!hasAfter) return before;
            double fraction = (time - lower).TotalHours;
            double angle = Math.IEEERemainder(after.BrightSideDegrees - before.BrightSideDegrees, 360);
            return new BodyIllumination(before.Fraction + fraction * (after.Fraction - before.Fraction),
                before.BrightSideDegrees + fraction * angle);
        }
    }

    public sealed record OverlayLegendEntry(string Name, Brush Ink, DoubleCollection Dashes, string Altitude, bool ShowCurve);

    internal static class OverlayStyle {
        private static readonly Brush[] Day = {
            Ink(0xD1, 0xB2, 0xEE), Ink(0xE8, 0xA4, 0xBC), Ink(0xEE, 0x9A, 0x80),
            Ink(0xBA, 0xA1, 0xFF), Ink(0xA3, 0xDB, 0x98), Ink(0x9C, 0xB6, 0xF5),
            Ink(0xE6, 0xAA, 0xCE), Ink(0xF0, 0xA8, 0x80), Ink(0xA2, 0xDD, 0xCB)
        };
        private static readonly Brush[] Night = {
            Ink(0xE8, 0x5E, 0x58), Ink(0xF0, 0x79, 0x67), Ink(0xD9, 0x55, 0x4E)
        };
        private static readonly Brush[] Light = {
            Ink(0x72, 0x40, 0x95), Ink(0x97, 0x2F, 0x59), Ink(0xA4, 0x39, 0x24),
            Ink(0x62, 0x3A, 0xB0), Ink(0x26, 0x68, 0x36), Ink(0x33, 0x58, 0x9B),
            Ink(0x91, 0x3D, 0x86), Ink(0x97, 0x4A, 0x1C), Ink(0x20, 0x6A, 0x5C)
        };
        private static readonly DashStyle[] Patterns = {
            Dash(2, 3), Dash(6, 3), Dash(3, 2, 1, 2), Dash(8, 3), Dash(1, 3),
            Dash(7, 2, 2, 2), Dash(4, 4), Dash(9, 3, 1, 3), Dash(5, 2, 1, 2)
        };

        internal static Brush BrushFor(int index, ChartScheme scheme) {
            var background = Application.Current?.TryFindResource("TertiaryBackgroundBrush") as SolidColorBrush
                ?? Application.Current?.TryFindResource("BackgroundBrush") as SolidColorBrush;
            var colours = scheme switch {
                ChartScheme.Night => Night,
                ChartScheme.Default when background?.Color is { } color &&
                    .2126 * color.R + .7152 * color.G + .0722 * color.B > 155 => Light,
                _ => Day
            };
            return colours[index % colours.Length];
        }

        internal static DashStyle DashFor(int index) => Patterns[index % Patterns.Length];

        private static Brush Ink(byte r, byte g, byte b) {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static DashStyle Dash(params double[] lengths) {
            var dash = new DashStyle(lengths, 0);
            dash.Freeze();
            return dash;
        }
    }

    internal static class OverlayEphemeris {
        internal static BodyIllumination? Phase(NOVAS.Body planet, Site site, DateTimeOffset time) {
            var body = planet switch {
                NOVAS.Body.Mercury => Body.Mercury,
                NOVAS.Body.Venus => Body.Venus,
                _ => (Body?)null
            };
            if (!body.HasValue) return null;
            var at = new AstroTime(time.UtcDateTime);
            var observer = new Observer(site.Lat, site.Lon, site.Elev);
            var planetPosition = Horizontal(body.Value, at, observer);
            var sun = Horizontal(Body.Sun, at, observer);
            const double radians = Math.PI / 180;
            double azimuthDifference = (sun.azimuth - planetPosition.azimuth) * radians;
            double sunAltitude = sun.altitude * radians;
            double planetAltitude = planetPosition.altitude * radians;
            // Project the Sun onto the planet's local sky plane. Positive X follows increasing
            // azimuth, and the screen's Y is down. The phase geometry starts lit on the right.
            double right = Math.Cos(sunAltitude) * Math.Sin(azimuthDifference);
            double up = Math.Cos(planetAltitude) * Math.Sin(sunAltitude)
                - Math.Sin(planetAltitude) * Math.Cos(sunAltitude) * Math.Cos(azimuthDifference);
            return new BodyIllumination(Astronomy.Illumination(body.Value, at).phase_fraction,
                Math.Atan2(-up, right) / radians);
        }

        private static Topocentric Horizontal(Body body, AstroTime time, Observer observer) {
            var coordinates = Astronomy.Equator(body, time, observer, EquatorEpoch.OfDate, Aberration.Corrected);
            return Astronomy.Horizon(time, observer, coordinates.ra, coordinates.dec, Refraction.None);
        }

        internal static double SolarAzimuth(NOVAS.Body body, Site site, DateTimeOffset time) {
            var at = new AstroTime(time.UtcDateTime);
            var observer = new Observer(site.Lat, site.Lon, site.Elev);
            var solarBody = body == NOVAS.Body.Sun ? Body.Sun : Body.Moon;
            var position = Astronomy.Equator(solarBody, at, observer, EquatorEpoch.OfDate, Aberration.None);
            return Astronomy.Horizon(at, observer, position.ra, position.dec, Refraction.None).azimuth;
        }

        internal static double PlanetAltitude(NOVAS.Body planet, Site site, DateTimeOffset time) {
            var (hourAngle, declination) = PlanetPosition(planet, site, time);
            return AstroUtil.GetAltitude(hourAngle, site.Lat, declination);
        }

        internal static double PlanetAzimuth(NOVAS.Body planet, Site site, DateTimeOffset time) {
            var (hourAngle, declination) = PlanetPosition(planet, site, time);
            double altitude = AstroUtil.GetAltitude(hourAngle, site.Lat, declination);
            return AstroUtil.GetAzimuth(hourAngle, altitude, site.Lat, declination);
        }

        private static (double HourAngle, double Declination) PlanetPosition(NOVAS.Body planet, Site site, DateTimeOffset time) {
            var utc = time.UtcDateTime;
            // Chart sampling can run for dates without an Earth-orientation database entry.
            // Use UTC for UT1 and the existing astronomy library's Delta T approximation.
            var ephemerisTime = new AstroTime(utc);
            double deltaDays = ephemerisTime.tt - ephemerisTime.ut;
            double julian = AstroUtil.GetJulianDate(utc);
            var observer = new NOVAS.Observer {
                Where = (short)NOVAS.ObserverLocation.EarthSurface,
                OnSurf = new NOVAS.OnSurface { Latitude = site.Lat, Longitude = site.Lon, Height = site.Elev }
            };
            var body = new NOVAS.CelestialObject {
                Type = (short)NOVAS.ObjectType.MajorPlanetSunOrMoon,
                Number = (short)planet,
                Name = planet.ToString()
            };
            var position = new NOVAS.SkyPosition();
            short status = NOVAS.Place(julian + deltaDays,
                body, observer, deltaDays * 86400, NOVAS.CoordinateSystem.EquinoxOfDate, NOVAS.Accuracy.Full, ref position);
            if (status != 0) throw new InvalidOperationException($"N.I.N.A. could not calculate {planet} (NOVAS status {status}).");
            var wholeDay = Math.Floor(julian);
            double greenwich = 0;
            status = NOVAS.SiderealTime(wholeDay, julian - wholeDay, deltaDays * 86400,
                NOVAS.GstType.GreenwichApparentSiderealTime, NOVAS.Method.EquinoxBased, NOVAS.Accuracy.Full, ref greenwich);
            if (status != 0) throw new InvalidOperationException($"N.I.N.A. could not calculate sidereal time (NOVAS status {status}).");
            var siderealTime = greenwich + AstroUtil.DegreesToHours(site.Lon);
            var hourAngle = AstroUtil.GetHourAngle(siderealTime, position.RA);
            return (AstroUtil.HoursToDegrees(hourAngle), position.Dec);
        }

        internal static double TargetAltitude(FramingTarget target, Site site, DateTimeOffset time) =>
            TargetPosition(target, site, time).Altitude;

        internal static double TargetAzimuth(FramingTarget target, Site site, DateTimeOffset time) =>
            TargetPosition(target, site, time).Azimuth;

        private static (double Altitude, double Azimuth) TargetPosition(FramingTarget target, Site site, DateTimeOffset time) {
            const double degreesToRadians = Math.PI / 180;
            double azimuth = 0, zenithDistance = 0, hourAngle = 0, dec = 0, ra = 0, origins = 0;
            short status = SOFA.CelestialToTopocentric(target.RightAscensionHours * 15 * degreesToRadians,
                target.DeclinationDegrees * degreesToRadians, 0, 0, 0, 0,
                AstroUtil.GetJulianDate(time.UtcDateTime), 0, 0,
                site.Lon * degreesToRadians, site.Lat * degreesToRadians, site.Elev,
                0, 0, 0, 0, 0, 0, ref azimuth, ref zenithDistance, ref hourAngle, ref dec, ref ra, ref origins);
            if (status < 0) throw new InvalidOperationException($"N.I.N.A. could not calculate {target.Name} (SOFA status {status}).");
            return (90 - zenithDistance / degreesToRadians, azimuth / degreesToRadians);
        }
    }
}
