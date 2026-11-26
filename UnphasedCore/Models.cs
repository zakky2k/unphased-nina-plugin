namespace Zakfo.Unphased.Core;

/// <summary>Observer location: geodetic latitude/longitude (degrees) and elevation (metres).</summary>
public readonly record struct Site(double Lat, double Lon, double Elev = 0.0);

/// <summary>A sun-altitude threshold crossing inside the night window.</summary>
public sealed record TwilightEvent(string Band, string Edge, DateTimeOffset Time);

/// <summary>Moon altitude/azimuth/illumination sample on the fixed-frame grid.</summary>
public sealed record MoonSample(
    DateTimeOffset Time, double Altitude, double Azimuth, double IllumFraction, double PhaseAngle);

/// <summary>Sun altitude sample on the fixed-frame grid (noon -> noon+2).</summary>
public sealed record SunSample(DateTimeOffset Time, double Altitude);

/// <summary>Moon rise/set instants across the full frame. (The planner's §6 payload derives them from its night-clipped sample window only.)</summary>
public sealed record MoonEvents(IReadOnlyList<DateTimeOffset> Rise, IReadOnlyList<DateTimeOffset> Set);

/// <summary>
/// The computed night. Times are absolute instants; MoonSamples covers the
/// whole noon->noon+2 frame (the planner's §6 payload clips it to the night
/// window and derives moonrise/moonset from that clipped window only).
/// </summary>
public sealed class NightPayload
{
    public required Site Location { get; init; }
    public required DateTimeOffset FrameNoon { get; init; }
    public required DateTimeOffset NightStart { get; init; }   // sunset
    public required DateTimeOffset NightEnd { get; init; }     // sunrise
    public required DateTimeOffset Sunset { get; init; }
    public required DateTimeOffset Sunrise { get; init; }
    public required IReadOnlyList<TwilightEvent> Events { get; init; }
    public required IReadOnlyList<SunSample> SunSeries { get; init; }
    public required bool TrueDarkness { get; init; }
    /// <summary>Lowest sun altitude on the whole noon->noon+2 frame grid, rounded to 0.01 deg.</summary>
    public required double DeepestSunAltitude { get; init; }
    /// <summary>Moon samples over the whole noon->noon+2 frame (planner's payload clips to the night window).</summary>
    public required IReadOnlyList<MoonSample> MoonSamples { get; init; }
    public required MoonEvents Moon { get; init; }
    public required bool Waxing { get; init; }
    public required string Limb { get; init; }               // "left"/"right" as seen by this observer
    public required DateTimeOffset MidnightTime { get; init; }
    /// <summary>Moon state at the frame-grid sample nearest midnight (planner convention: index counted from sunset).</summary>
    public required double MidnightIllumFraction { get; init; }
    public required double MidnightMoonAltitude { get; init; }
    public required double MidnightMoonAzimuth { get; init; }
    public required int DarkMinutes { get; init; }
    public bool? MoonUpDuringDark { get; init; }
}
