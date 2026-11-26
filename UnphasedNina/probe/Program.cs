using System.Text.Json;
using CosineKitty;
using Zakfo.Unphased.Core;

// Scratch accuracy probe (not part of the plugin): compares UnphasedCore's
// NightCalculator against the astropy golden payloads night-*.json.

static TimeZoneInfo FindTz(string id)
{
    try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
    catch (TimeZoneNotFoundException)
    {
        var all = TimeZoneInfo.GetSystemTimeZones();
        foreach (var tzi in all)
            if (tzi.Id.Replace('_', ' ').Equals(id, StringComparison.OrdinalIgnoreCase) ||
                (tzi.DisplayName.Contains(id.Split('/')[1].Replace('_', ' '), StringComparison.OrdinalIgnoreCase)))
                return tzi;
        throw;
    }
}

foreach (var name in new[] { "prague", "summer", "sydney" })
{
    var json = JsonDocument.Parse(File.ReadAllText($"night-{name}.json"));
    var root = json.RootElement;
    var loc = root.GetProperty("location");
    var site = new Site(loc.GetProperty("lat").GetDouble(), loc.GetProperty("lon").GetDouble());
    var tz = FindTz(loc.GetProperty("tz").GetString()!);

    var frameNoonLocal = DateTimeOffset.Parse(root.GetProperty("frameNoon").GetString()!);
    // Planner convention: the pivot is 12:00 wall-clock at the site zone, so
    // reconstruct it from the tz id rather than trusting the fixture's offset.
    var noonUtc = TimeZoneInfo.ConvertTimeToUtc(
        new DateTime(frameNoonLocal.Year, frameNoonLocal.Month, frameNoonLocal.Day, 12, 0, 0), tz);
    var calc = new NightCalculator(site);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var p = calc.Compute(new DateTimeOffset(noonUtc, TimeSpan.Zero));
    sw.Stop();

    static double[] GetArr(JsonElement rootEl, string path1, string path2) =>
        rootEl.GetProperty(path1).GetProperty(path2).EnumerateArray()
            .Select(x => x.GetProperty("altitude").GetDouble()).ToArray();

    // Planner convention: the payload's moon samples are CLIPPED to the night
    // window, starting at the first full-frame grid point >= sunset. Compare
    // against computed samples from that index onward.
    var wantAlt = GetArr(root, "moon", "samples");
    var wantAz = root.GetProperty("moon").GetProperty("samples").EnumerateArray()
        .Select(x => x.GetProperty("azimuth").GetDouble()).ToArray();
    int startIdx = 0;
    while (p.MoonSamples[startIdx].Time < p.Sunset) startIdx++;
    double maxA = 0, maxZ = 0;
    for (int i = 0; i < wantAlt.Length && startIdx + i < p.MoonSamples.Count; i++)
    {
        maxA = Math.Max(maxA, Math.Abs(p.MoonSamples[startIdx + i].Altitude - wantAlt[i]));
        double dAz = Math.Abs(((p.MoonSamples[startIdx + i].Azimuth - wantAz[i] + 540) % 360) - 180);
        maxZ = Math.Max(maxZ, dAz);
    }

    // sun series
    var sunWant = root.GetProperty("sun").GetProperty("series").EnumerateArray()
        .Select(x => x.GetProperty("altitude").GetDouble()).ToArray();
    double maxS = 0;
    for (int i = 0; i < Math.Min(sunWant.Length, p.SunSeries.Count); i++)
        maxS = Math.Max(maxS, Math.Abs(p.SunSeries[i].Altitude - sunWant[i]));

    // events: match each golden event to nearest same band/edge computed event
    double worstEventMin = 0;
    foreach (var e in root.GetProperty("sun").GetProperty("events").EnumerateArray())
    {
        var want = DateTimeOffset.Parse(e.GetProperty("time").GetString()!);
        var cand = p.Events.Where(x => x.Band == e.GetProperty("band").GetString() && x.Edge == e.GetProperty("edge").GetString());
        double d = cand.Min(x => Math.Abs((x.Time - want).TotalMinutes));
        worstEventMin = Math.Max(worstEventMin, d);
    }

    // moonrise/moonset: the planner derives them from its sunset-anchored
    // sample window only, so compare times within that window.
    var winStart = p.MoonSamples[startIdx].Time.UtcDateTime;
    var winEnd = DateTimeOffset.Parse(root.GetProperty("moon").GetProperty("samples").EnumerateArray().Last().GetProperty("time").GetString()!).UtcDateTime;
    static bool InWindow(DateTimeOffset t, DateTime a, DateTime b) => t.UtcDateTime >= a && t.UtcDateTime <= b;
    var wantRise = root.GetProperty("moon").GetProperty("moonrise").EnumerateArray()
        .Select(x => DateTimeOffset.Parse(x.GetString()!)).ToList();
    var wantSet = root.GetProperty("moon").GetProperty("moonset").EnumerateArray()
        .Select(x => DateTimeOffset.Parse(x.GetString()!)).ToList();
    var winRise = p.Moon.Rise.Where(w => InWindow(w, winStart, winEnd)).ToList();
    var winSet = p.Moon.Set.Where(w => InWindow(w, winStart, winEnd)).ToList();
    double riseDiff = wantRise.Count != winRise.Count ? 999
        : wantRise.Count == 0 ? 0 : wantRise.Zip(winRise).Max(x => Math.Abs((x.First - x.Second).TotalMinutes));
    double setDiff = wantSet.Count != winSet.Count ? 999
        : wantSet.Count == 0 ? 0 : wantSet.Zip(winSet).Max(x => Math.Abs((x.First - x.Second).TotalMinutes));

    // midnight + summary
    var mid = root.GetProperty("moon").GetProperty("midnight");
    var wantMidTime = DateTimeOffset.Parse(mid.GetProperty("time").GetString()!);
    double midIllumDiff = Math.Abs(p.MidnightIllumFraction - mid.GetProperty("illumFraction").GetDouble());
    double midAltDiff = Math.Abs(p.MidnightMoonAltitude - mid.GetProperty("altitude").GetDouble());
    var sum = root.GetProperty("summary");
    int wantDark = sum.GetProperty("darkMinutes").GetInt32();

    Console.WriteLine($"{name} ({sw.ElapsedMilliseconds} ms, samples {p.MoonSamples.Count}/{wantAlt.Length}, sun {p.SunSeries.Count}/{sunWant.Length})");
    Console.WriteLine($"  moon alt max {maxA:F4} az max {maxZ:F4} | sun alt max {maxS:F4}");
    Console.WriteLine($"  worst twilight/event diff {worstEventMin:F2} min | moonrise diff {riseDiff:F2} moonset diff {setDiff:F2}");
    Console.WriteLine($"  midnight time diff {(p.MidnightTime - wantMidTime).TotalSeconds:F0}s illum {midIllumDiff:F5} alt {midAltDiff:F3}");
    Console.WriteLine($"  darkMinutes {p.DarkMinutes} vs {wantDark} | trueDarkness {p.TrueDarkness} vs {root.GetProperty("sun").GetProperty("trueDarkness").GetBoolean()} | waxing {p.Waxing} limb {p.Limb}");
}
