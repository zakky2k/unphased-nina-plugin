// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    public sealed partial class NightPlannerVM {
        private sealed record HorizonProjection(IReadOnlyList<(double Minutes, double Altitude)> Samples, int ClearDarkMinutes);

        private readonly OverlayChoice horizonMoon = new("Moon", 0, planet: NOVAS.Body.Moon);
        private readonly OverlayChoice horizonSun = new("Sun", 0, planet: NOVAS.Body.Sun);
        private OverlayChoice horizonReference;
        private int horizonRevision;
        private string horizonStatus = string.Empty;

        public bool ShowCustomHorizon {
            get => ChartVisibility.CustomHorizon.Enabled;
            set => ChartVisibility.CustomHorizon.Enabled = value;
        }

        private void OnCustomHorizonToggled() {
            if (ShowCustomHorizon && horizonReference == null) {
                horizonReference = DefaultHorizonReference();
                RaisePropertyChanged(nameof(HorizonReference));
            }
            RaisePropertyChanged(nameof(ShowCustomHorizon));
            RefreshCustomHorizon();
        }

        public IEnumerable<OverlayChoice> HorizonReferenceOptions =>
            new[] { horizonMoon, horizonSun }.Concat(Planets).Concat(Targets);

        public OverlayChoice HorizonReference {
            get => horizonReference;
            set {
                if (value == null || ReferenceEquals(value, horizonReference) || !HorizonReferenceOptions.Contains(value)) return;
                horizonReference = value;
                RaisePropertyChanged();
                RefreshCustomHorizon();
            }
        }

        public string HorizonStatus {
            get => horizonStatus;
            private set {
                if (horizonStatus == value) return;
                horizonStatus = value;
                RaisePropertyChanged();
            }
        }

        public bool HasCustomHorizon => settings.ActiveHorizon != null;

        internal IReadOnlyList<(double Minutes, double Altitude)> CustomHorizonSamples { get; private set; } =
            Array.Empty<(double, double)>();

        public string HorizonDarkChip { get; private set; } = string.Empty;

        private OverlayChoice DefaultHorizonReference() =>
            Targets.FirstOrDefault(choice => choice.Enabled) ?? Planets.FirstOrDefault(choice => choice.Enabled) ?? horizonMoon;

        private void HorizonTargetsChanged() {
            bool replace = horizonReference?.Target != null && !Targets.Contains(horizonReference);
            if (replace) horizonReference = DefaultHorizonReference();
            RaisePropertyChanged(nameof(HorizonReferenceOptions));
            RaisePropertyChanged(nameof(HorizonReference));
            if (replace) RefreshCustomHorizon();
        }

        private void HorizonProfileChanged() {
            RaisePropertyChanged(nameof(HasCustomHorizon));
            RefreshCustomHorizon();
        }

        private void PublishCustomHorizon(HorizonProjection projection, OverlayChoice reference = null) {
            CustomHorizonSamples = projection?.Samples ?? Array.Empty<(double, double)>();
            HorizonDarkChip = projection == null
                ? string.Empty
                : $"{reference.Name} · {NightRowVM.FormatDuration(projection.ClearDarkMinutes)} dark & clear";
            RaisePropertyChanged(nameof(CustomHorizonSamples));
            RaisePropertyChanged(nameof(HorizonDarkChip));
        }

        private void RefreshCustomHorizon() {
            int revision = Interlocked.Increment(ref horizonRevision);
            if (CustomHorizonSamples.Count > 0) PublishCustomHorizon(null);
            var row = selectedNight;
            var reference = horizonReference;
            var horizon = settings.ActiveHorizon;
            HorizonStatus = !ShowCustomHorizon || horizon != null ? string.Empty : "No custom horizon in the active profile.";
            if (!ShowCustomHorizon || row == null || reference == null || horizon == null) return;

            _ = Task.Run(async () => {
                await overlayWorker.WaitAsync().ConfigureAwait(false);
                HorizonProjection projection = null;
                try {
                    projection = ProjectHorizon(row, reference, horizon, () => revision != Volatile.Read(ref horizonRevision));
                } catch (Exception ex) {
                    Logger.Error("Unphased could not project the custom horizon", ex);
                } finally {
                    overlayWorker.Release();
                }
                if (revision != Volatile.Read(ref horizonRevision)) return;
                OnUi(() => {
                    if (revision != Volatile.Read(ref horizonRevision)) return;
                    if (projection != null) PublishCustomHorizon(projection, reference);
                    else HorizonStatus = "Could not draw the custom horizon for this object.";
                });
            });
        }

        private static HorizonProjection ProjectHorizon(NightRowVM row,
            OverlayChoice reference, CustomHorizon horizon, Func<bool> cancelled) {
            const int sampleMinutes = 5;
            var site = row.Payload.Location;
            var dark = SelectedNightDarkIntervals(row.Payload);
            var samples = new List<(double Minutes, double Altitude)>();
            var clearance = new List<(double Minutes, double Clearance)>();
            double frameEnd = row.Chart.FrameMinutes;
            double darkEnd = dark.Count == 0 ? 0 : dark[^1].End;
            double end = Math.Max(frameEnd, darkEnd);
            for (double minutes = 0; ;) {
                if (cancelled()) return null;
                var time = row.Chart.TimeAtMinutes(minutes);
                var (altitude, azimuth) = reference.Planet switch {
                    NOVAS.Body.Sun or NOVAS.Body.Moon => OverlayEphemeris.SolarHorizontal(reference.Planet.Value, site, time),
                    { } planet => OverlayEphemeris.PlanetHorizontal(planet, site, time),
                    _ => OverlayEphemeris.TargetHorizontal(reference.Target, site, time)
                };
                double horizonAltitude = horizon.GetAltitude(azimuth);
                if (!double.IsFinite(horizonAltitude) || !double.IsFinite(altitude))
                    throw new InvalidOperationException("The reference altitude or custom horizon is unavailable.");
                if (minutes <= frameEnd) samples.Add((minutes, horizonAltitude));
                if (dark.Count > 0 && (clearance.Count == 0 || clearance[^1].Minutes < darkEnd))
                    clearance.Add((minutes, altitude - horizonAltitude));
                if (minutes == end) break;
                minutes = Math.Min(minutes + sampleMinutes, minutes < frameEnd ? frameEnd : end);
            }
            double clearDarkMinutes = dark.Sum(interval => TimeAboveHorizon(clearance, interval.Start, interval.End));
            return new HorizonProjection(samples, (int)Math.Floor(clearDarkMinutes + 1e-9));
        }

        private static List<(double Start, double End)> SelectedNightDarkIntervals(NightPayload payload) {
            var intervals = new List<(double, double)>();
            double? start = null;
            foreach (var transition in payload.Events) {
                if (transition.Time < payload.NightStart || transition.Time > payload.NightEnd || transition.Band != "astronomical")
                    continue;
                double minutes = (transition.Time - payload.FrameNoon).TotalMinutes;
                if (transition.Edge == "start") start = minutes;
                else if (transition.Edge == "end" && start.HasValue && minutes > start.Value) {
                    intervals.Add((start.Value, minutes));
                    start = null;
                }
            }
            return intervals;
        }

        internal static double TimeAboveHorizon(IReadOnlyList<(double Minutes, double Clearance)> samples, double start, double end) {
            double duration = 0;
            for (int i = 1; i < samples.Count; i++) {
                var (before, low) = samples[i - 1];
                var (after, high) = samples[i];
                if (after <= start) continue;
                if (before >= end) break;
                if (low <= 0 && high <= 0) continue;
                // Clearance is linear within each sample interval. Trim its positive portion
                // to the exact astronomical twilight crossings before adding it to the total.
                if (low <= 0 || high <= 0) {
                    double crossing = before - low / (high - low) * (after - before);
                    if (low <= 0) before = crossing;
                    else after = crossing;
                }
                duration += Math.Max(0, Math.Min(after, end) - Math.Max(before, start));
            }
            return duration;
        }
    }
}
