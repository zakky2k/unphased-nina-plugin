// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;

namespace Zakfo.Unphased.Features.planner {
    public sealed partial class NightPlannerVM {
        private readonly OverlayChoice horizonMoon = new("Moon", 0, planet: NOVAS.Body.Moon);
        private readonly OverlayChoice horizonSun = new("Sun", 0, planet: NOVAS.Body.Sun);
        private bool showCustomHorizon;
        private OverlayChoice horizonReference;
        private int horizonRevision;
        private string horizonStatus = string.Empty;

        public bool ShowCustomHorizon {
            get => showCustomHorizon;
            set {
                if (showCustomHorizon == value) return;
                showCustomHorizon = value;
                if (value && horizonReference == null) {
                    horizonReference = DefaultHorizonReference();
                    RaisePropertyChanged(nameof(HorizonReference));
                }
                RaisePropertyChanged();
                RefreshCustomHorizon();
            }
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

        private void PublishCustomHorizon(IReadOnlyList<(double Minutes, double Altitude)> samples) {
            CustomHorizonSamples = samples;
            RaisePropertyChanged(nameof(CustomHorizonSamples));
        }

        private void RefreshCustomHorizon() {
            int revision = Interlocked.Increment(ref horizonRevision);
            if (CustomHorizonSamples.Count > 0) PublishCustomHorizon(Array.Empty<(double, double)>());
            var row = selectedNight;
            var reference = horizonReference;
            var horizon = settings.ActiveHorizon;
            HorizonStatus = !showCustomHorizon || horizon != null ? string.Empty : "No custom horizon in the active profile.";
            if (!showCustomHorizon || row == null || reference == null || horizon == null) return;

            _ = Task.Run(async () => {
                await overlayWorker.WaitAsync().ConfigureAwait(false);
                IReadOnlyList<(double Minutes, double Altitude)> samples = null;
                try {
                    samples = ProjectHorizon(row, reference, horizon, () => revision != Volatile.Read(ref horizonRevision));
                } catch (Exception ex) {
                    Logger.Error("Unphased could not project the custom horizon", ex);
                } finally {
                    overlayWorker.Release();
                }
                if (revision != Volatile.Read(ref horizonRevision)) return;
                OnUi(() => {
                    if (revision != Volatile.Read(ref horizonRevision)) return;
                    if (samples != null) PublishCustomHorizon(samples);
                    else HorizonStatus = "Could not draw the custom horizon for this object.";
                });
            });
        }

        internal static IReadOnlyList<(double Minutes, double Altitude)> ProjectHorizon(NightRowVM row,
            OverlayChoice reference, CustomHorizon horizon, Func<bool> cancelled) {
            const int sampleMinutes = 5;
            var site = row.Payload.Location;
            var result = new List<(double Minutes, double Altitude)>();
            double end = row.Chart.FrameMinutes;
            for (double minutes = 0; ; minutes = Math.Min(minutes + sampleMinutes, end)) {
                if (cancelled()) return null;
                var time = row.Chart.TimeAtMinutes(minutes);
                double azimuth = reference.Planet switch {
                    NOVAS.Body.Sun or NOVAS.Body.Moon => OverlayEphemeris.SolarAzimuth(reference.Planet.Value, site, time),
                    { } planet => OverlayEphemeris.PlanetAzimuth(planet, site, time),
                    _ => OverlayEphemeris.TargetAzimuth(reference.Target, site, time)
                };
                double altitude = horizon.GetAltitude(azimuth);
                if (!double.IsFinite(altitude)) throw new InvalidOperationException("No custom horizon altitude at the reference azimuth.");
                result.Add((minutes, altitude));
                if (minutes == end) return result;
            }
        }
    }
}
