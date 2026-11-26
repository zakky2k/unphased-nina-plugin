using CosineKitty;

namespace Zakfo.Unphased.Core;

/// <summary>Raised when a night cannot be computed (polar day/night, bad inputs).</summary>
public sealed class NightException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Pure C# night computation mirroring the AstroScripts planner contract:
/// sunset/sunrise by geometric altitude-zero crossings, twilight bands at
/// -6/-12/-18 degrees, a 10-minute sun grid across a fixed noon-to-noon+2
/// frame, and a topocentric moon track with barycentric phase angle.
/// </summary>
public sealed class NightCalculator
{
    public const int SunGridMin = 10;
    public const int MoonGridMin = 10;

    private readonly Site site;
    private readonly Observer observer;

    public NightCalculator(Site site)
    {
        if (site.Lat is < -90 or > 90) throw new NightException("lat_out_of_range", "latitude must be within [-90, 90]");
        if (site.Lon is < -180 or > 180) throw new NightException("lon_out_of_range", "longitude must be within [-180, 180]");
        this.site = site;
        observer = new Observer(site.Lat, site.Lon, site.Elev);
    }

    /// <summary>Topocentric horizontal coordinates, airless and un-aberrated (astropy convention).</summary>
    private (double Alt, double Az) Horizontal(Body body, AstroTime at)
    {
        var equ = Astronomy.Equator(body, at, observer, EquatorEpoch.OfDate, Aberration.None);
        var hor = Astronomy.Horizon(at, observer, equ.ra, equ.dec, Refraction.None);
        return (hor.altitude, hor.azimuth);
    }

    /// <summary>Moon altitude/azimuth/illumination at one instant, computed fresh.</summary>
    public MoonSample MoonAt(DateTimeOffset time)
    {
        var at = new AstroTime(time.UtcDateTime);
        var (alt, az) = Horizontal(Body.Moon, at);
        double phaseAngle = PhaseAngleDeg(at);
        double illum = (1 + Math.Cos(phaseAngle * Math.PI / 180.0)) / 2.0;
        return new MoonSample(time.ToUniversalTime(), alt, az % 360.0, illum, phaseAngle);
    }

    /// <summary>
    /// Compute the night anchored at a local noon pivot expressed as an
    /// absolute instant (the moment clocks read 12:00 at the site's zone).
    /// </summary>
    public NightPayload Compute(DateTimeOffset localNoon)
    {
        var pivot = localNoon.UtcDateTime;

        // Coarse sun scan to find sunset after noon, then the sunrise that follows.
        DateTime? sunset = null, sunrise = null;
        double prevAlt = SunAlt(pivot);
        for (int i = 1; i <= 3 * 24 * 12; i++)
        {
            var next = pivot.AddMinutes(5.0 * i);
            double alt = SunAlt(next);
            if (sunset is null && prevAlt >= 0 && alt < 0)
                sunset = Crossing(Body.Sun, 0.0, pivot.AddMinutes(5.0 * (i - 1)), next);
            else if (sunset is not null && prevAlt < 0 && alt >= 0)
            {
                sunrise = Crossing(Body.Sun, 0.0, pivot.AddMinutes(5.0 * (i - 1)), next);
                break;
            }
            prevAlt = alt;
        }
        if (sunset is null || sunrise is null)
            throw new NightException("polar", "no sunset/sunrise in this frame (polar day or night); not supported");

        // Twilight transitions inside the night window.
        var events = new List<TwilightEvent>();
        foreach (var (threshold, band) in new[] { (-6.0, "civil"), (-12.0, "nautical"), (-18.0, "astronomical") })
            foreach (var (t, rising) in SunCrossings(threshold, sunset.Value, sunrise.Value))
                events.Add(new TwilightEvent(band, rising ? "end" : "start", new DateTimeOffset(t, TimeSpan.Zero)));
        events.Sort((a, b) => a.Time.CompareTo(b.Time));

        // Sun altitude on a 10-minute grid across the whole fixed frame:
        // noon -> noon+2 inclusive (289 samples at 10-minute steps).
        var sunSeries = new List<SunSample>();
        for (int i = 0; ; i++)
        {
            var t = pivot.AddMinutes(SunGridMin * (double)i);
            if (t > pivot + TimeSpan.FromDays(2)) break;
            sunSeries.Add(new SunSample(new DateTimeOffset(t, TimeSpan.Zero), Math.Round(SunAlt(t), 3)));
        }

        // Moon track across the FULL fixed frame (noon -> noon+2 inclusive),
        // 10-minute sampling: the chart draws the moon's arc through daylight
        // hours and shows its rise even when that is after sunrise. Below-
        // horizon samples stay so crossings can be refined from them.
        int n = (int)(TimeSpan.FromDays(2).TotalMinutes / MoonGridMin) + 1;
        var moonTimes = new DateTime[n + 1];
        for (int i = 0; i <= n; i++) moonTimes[i] = pivot.AddMinutes(MoonGridMin * (double)i);
        var moonSamples = new List<MoonSample>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            var t = moonTimes[i];
            var at = new AstroTime(t);
            var (alt, az) = Horizontal(Body.Moon, at);
            double phaseAngle = PhaseAngleDeg(at);
            double illum = (1 + Math.Cos(phaseAngle * Math.PI / 180.0)) / 2.0;
            moonSamples.Add(new MoonSample(
                new DateTimeOffset(t, TimeSpan.Zero),
                Math.Round(alt, 3), Math.Round(az, 2), Math.Round(illum, 4), Math.Round(phaseAngle, 3)));
        }

        // Moon rise/set: interpolated zero crossings of the sampled track,
        // refined by bisection on the true altitude function.
        var rises = new List<DateTimeOffset>();
        var sets = new List<DateTimeOffset>();
        for (int i = 1; i < moonSamples.Count; i++)
        {
            double a0 = moonSamples[i - 1].Altitude, a1 = moonSamples[i].Altitude;
            if ((a0 < 0 && a1 >= 0) || (a0 >= 0 && a1 < 0))
            {
                var t = Crossing(Body.Moon, 0.0, moonTimes[i - 1], moonTimes[i]);
                (a1 > a0 ? rises : sets).Add(new DateTimeOffset(t, TimeSpan.Zero));
            }
        }

        // Midnight: midpoint of sunset..sunrise; the planner reports the moon
        // state at grid sample int((mid-sunset)/10min) counted from SUNSET, so
        // locate that time on our pivot-anchored frame grid.
        var mid = sunset.Value + (sunrise.Value - sunset.Value) / 2;
        var midSampleTime = sunset.Value.AddMinutes(MoonGridMin * Math.Floor((mid - sunset.Value).TotalMinutes / MoonGridMin));
        int midIdx = Math.Min(
            (int)Math.Round((midSampleTime - pivot).TotalMinutes / MoonGridMin), moonSamples.Count - 1);
        var midnightSample = moonSamples[midIdx];

        // Waxing from elongation sign: the Moon's eastward angular distance
        // from the Sun runs 0->360 through a lunation, so under 180 is waxing.
        var atMid = new AstroTime(mid);
        double elong = (MoonRaOfDate(atMid) - SunRaOfDate(atMid)) % 360.0;
        if (elong < 0) elong += 360.0;
        bool waxing = elong < 180.0;
        // A waxing moon is lit on the eastern limb: right for a northern
        // observer, left for a southern one.
        string limb = waxing ? "right" : "left";
        if (site.Lat < 0) limb = limb == "right" ? "left" : "right";

        var astro = events.Where(e => e.Band == "astronomical").ToList();
        DateTime? darkStart = astro.FirstOrDefault(e => e.Edge == "start")?.Time.UtcDateTime;
        DateTime? darkEnd = astro.FirstOrDefault(e => e.Edge == "end")?.Time.UtcDateTime;

        bool? upDuringDark = null;
        if (darkStart is not null && darkEnd is not null)
            upDuringDark = moonSamples.Any(m =>
                m.Time.UtcDateTime >= darkStart && m.Time.UtcDateTime <= darkEnd && m.Altitude > 0);

        return new NightPayload
        {
            Location = site,
            FrameNoon = new DateTimeOffset(pivot, TimeSpan.Zero),
            NightStart = new DateTimeOffset(sunset.Value, TimeSpan.Zero),
            NightEnd = new DateTimeOffset(sunrise.Value, TimeSpan.Zero),
            Sunset = new DateTimeOffset(sunset.Value, TimeSpan.Zero),
            Sunrise = new DateTimeOffset(sunrise.Value, TimeSpan.Zero),
            Events = events,
            TrueDarkness = darkStart is not null && darkEnd is not null,
            DeepestSunAltitude = Math.Round(sunSeries.Min(s => s.Altitude), 2),
            SunSeries = sunSeries,
            MoonSamples = moonSamples,
            Moon = new MoonEvents(rises, sets),
            Waxing = waxing,
            Limb = limb,
            MidnightTime = new DateTimeOffset(mid, TimeSpan.Zero),
            MidnightIllumFraction = midnightSample.IllumFraction,
            MidnightMoonAltitude = midnightSample.Altitude,
            MidnightMoonAzimuth = midnightSample.Azimuth,
            DarkMinutes = darkStart is not null && darkEnd is not null
                ? (int)(darkEnd.Value - darkStart.Value).TotalMinutes // planner floors whole minutes; crossing jitter makes truncation match
                : 0,
            MoonUpDuringDark = upDuringDark,
        };
    }

    private double SunAlt(DateTime utc)
    {
        var (alt, _) = Horizontal(Body.Sun, new AstroTime(utc));
        return alt;
    }

    /// <summary>Bisect the body's altitude crossing of threshold inside [t0, t1].</summary>
    private DateTime Crossing(Body body, double threshold, DateTime t0, DateTime t1)
    {
        double a0 = Alt(body, t0) - threshold;
        for (int i = 0; i < 60; i++)
        {
            DateTime m = t0 + (t1 - t0) / 2;
            double am = Alt(body, m) - threshold;
            if ((a0 >= 0) == (am >= 0)) { t0 = m; a0 = am; } else t1 = m;
        }
        return t0 + (t1 - t0) / 2;

        double Alt(Body b, DateTime t)
        {
            var (a, _) = Horizontal(b, new AstroTime(t));
            return a;
        }
    }

    /// <summary>Every threshold crossing of the sun's altitude in [t0, t1], with direction.</summary>
    private List<(DateTime Time, bool Rising)> SunCrossings(double threshold, DateTime t0, DateTime t1)
    {
        var outp = new List<(DateTime, bool)>();
        const int stepMin = 5;
        int steps = (int)((t1 - t0).TotalMinutes / stepMin) + 1;
        double prev = SunAlt(t0) - threshold;
        for (int i = 1; i <= steps; i++)
        {
            DateTime t = t0.AddMinutes(stepMin * (double)i);
            if (t > t1) t = t1;
            double cur = SunAlt(t) - threshold;
            if ((prev < 0 && cur >= 0) || (prev >= 0 && cur < 0))
                outp.Add((Crossing(Body.Sun, threshold, t.AddMinutes(-stepMin), t), cur > prev));
            prev = cur;
            if (t >= t1) break;
        }
        return outp;
    }

    /// <summary>
    /// Sun-Moon-Earth phase angle from barycentric vectors against the
    /// geocentre (topocentric would add ~1 degree of parallax and skew it),
    /// matching the planner's phase_angle_deg.
    /// </summary>
    private static double PhaseAngleDeg(AstroTime at)
    {
        var earth = Astronomy.HelioVector(Body.Earth, at);
        var moon = Astronomy.HelioVector(Body.Moon, at);
        var sun = Astronomy.HelioVector(Body.Sun, at);
        double tmx = moon.x - earth.x, tmy = moon.y - earth.y, tmz = moon.z - earth.z;
        double tsx = moon.x - sun.x, tsy = moon.y - sun.y, tsz = moon.z - sun.z;
        double nm = Math.Sqrt(tmx * tmx + tmy * tmy + tmz * tmz);
        double ns = Math.Sqrt(tsx * tsx + tsy * tsy + tsz * tsz);
        double cos = (tmx * tsx + tmy * tsy + tmz * tsz) / (nm * ns);
        return Math.Acos(Math.Clamp(cos, -1.0, 1.0)) * 180.0 / Math.PI;
    }

    private double MoonRaOfDate(AstroTime at)
    {
        var equ = Astronomy.Equator(Body.Moon, at, observer, EquatorEpoch.OfDate, Aberration.None);
        return equ.ra;
    }

    private static double SunRaOfDate(AstroTime at)
    {
        var geo = Astronomy.GeoVector(Body.Sun, at, Aberration.None);
        return Astronomy.EquatorFromVector(geo).ra;
    }
}
