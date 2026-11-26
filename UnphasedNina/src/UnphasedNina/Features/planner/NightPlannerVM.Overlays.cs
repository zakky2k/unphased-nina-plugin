// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Utility;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Zakfo.Unphased.Core;

namespace Zakfo.Unphased.Features.planner {
    public sealed partial class NightPlannerVM {
        private static readonly NOVAS.Body[] PlanetBodies = {
            NOVAS.Body.Mercury, NOVAS.Body.Venus, NOVAS.Body.Mars, NOVAS.Body.Jupiter,
            NOVAS.Body.Saturn, NOVAS.Body.Uranus, NOVAS.Body.Neptune
        };
        private readonly ObservableCollection<OverlayChoice> targets = new();
        private readonly SemaphoreSlim overlayWorker = new(1);
        private IReadOnlyList<OverlayTrack> overlayTracks = Array.Empty<OverlayTrack>();
        private int overlayRevision;
        private int nextTargetStyle;
        private string overlayNotice = string.Empty;
        private IRelayCommand<OverlayChoice> removeTarget;
        private PlanetGlyphStyle glyphStyle = PlanetGlyphStyle.Phase;

        public IReadOnlyList<OverlayChoice> Planets { get; private set; }
        public ReadOnlyObservableCollection<OverlayChoice> Targets { get; private set; }
        public string PlanetSelection => $"{Planets.Count(p => p.Enabled)} selected";
        public string TargetSelection => $"{Targets.Count(t => t.Enabled)} selected";
        public IReadOnlyList<PlanetGlyphStyle> GlyphStyles { get; } = Enum.GetValues<PlanetGlyphStyle>();
        public PlanetGlyphStyle GlyphStyle {
            get => glyphStyle;
            set {
                if (glyphStyle == value) return;
                glyphStyle = value;
                RaisePropertyChanged();
            }
        }
        public string OverlayNotice {
            get => overlayNotice;
            private set {
                if (overlayNotice == value) return;
                overlayNotice = value;
                RaisePropertyChanged();
            }
        }
        public IRelayCommand<OverlayChoice> RemoveTargetCommand => removeTarget ??= new RelayCommand<OverlayChoice>(RemoveTarget);

        internal IReadOnlyList<OverlayTrack> OverlayTracks => overlayTracks;

        internal BodyIllumination? PlanetPreviewPhase(OverlayChoice choice) {
            var row = selectedNight;
            var track = overlayTracks.FirstOrDefault(track => ReferenceEquals(track.Choice, choice));
            if (row == null || track == null || track.Phases.Count == 0) return null;

            var instant = scrubMinutes is { } minutes ? row.Chart.TimeAtMinutes(minutes) : row.Payload.MidnightTime;
            if (track.Phases.TryGetValue(instant, out var phase)) return phase;
            // The night midpoint is usually between two of the cached hourly samples.
            return track.Phases.MinBy(sample => Math.Abs((sample.Key - instant).Ticks)).Value;
        }

        public IEnumerable<OverlayLegendEntry> OverlayLegend {
            get {
                var instant = scrubMinutes.HasValue ? selectedNight?.Chart.TimeAtMinutes(scrubMinutes.Value) : null;
                foreach (var track in overlayTracks) {
                    int style = track.Choice.StyleIndex;
                    double altitude = instant.HasValue ? NightChartVM.AltitudeAt(track.Samples, instant.Value) : double.NaN;
                    yield return new OverlayLegendEntry(track.Choice.Name, OverlayStyle.BrushFor(style, Scheme),
                        OverlayStyle.DashFor(style).Dashes, double.IsFinite(altitude)
                            ? $"{altitude.ToString("0.#", CultureInfo.CurrentCulture)}°" : string.Empty);
                }
            }
        }

        private void InitializeOverlays() {
            Planets = PlanetBodies.Select((body, index) => new OverlayChoice(body.ToString(), index, planet: body)).ToArray();
            foreach (var planet in Planets) planet.PropertyChanged += OverlaySelectionChanged;
            Targets = new ReadOnlyObservableCollection<OverlayChoice>(targets);
            ReloadTargets();
        }

        private void ReloadTargets() {
            foreach (var target in targets) target.PropertyChanged -= OverlaySelectionChanged;
            targets.Clear();
            nextTargetStyle = PlanetBodies.Length;
            var json = settings.OverlayTargetsJson;
            if (!string.IsNullOrWhiteSpace(json)) {
                try {
                    foreach (var target in JsonSerializer.Deserialize<FramingTarget[]>(json) ?? Array.Empty<FramingTarget>())
                        if (target?.IsValid == true) AddTargetChoice(target);
                } catch (JsonException ex) {
                    Logger.Error("Unphased could not read the saved overlay targets", ex);
                }
            }
            OverlayNotice = string.Empty;
            RaisePropertyChanged(nameof(TargetSelection));
            RefreshOverlays();
        }

        internal void AddFramingTarget(FramingTarget target) {
            if (target == null || !target.IsValid) {
                OverlayNotice = "Select a target in Framing Assistant first.";
                return;
            }
            OverlayNotice = string.Empty;
            var existing = targets.FirstOrDefault(choice => choice.Target.Matches(target));
            if (existing != null) {
                if (!string.IsNullOrWhiteSpace(target.ObjectType) && target.ObjectType != existing.Target.ObjectType) {
                    existing.SetTargetType(target.ObjectType);
                    SaveTargets();
                    RaisePropertyChanged(nameof(OverlayTracks));
                }
                existing.Enabled = true;
                return;
            }
            var added = AddTargetChoice(target);
            SaveTargets();
            added.Enabled = true;
        }

        private OverlayChoice AddTargetChoice(FramingTarget target) {
            var choice = new OverlayChoice(target.Name, nextTargetStyle++, target: target);
            choice.PropertyChanged += OverlaySelectionChanged;
            targets.Add(choice);
            return choice;
        }

        private void RemoveTarget(OverlayChoice choice) {
            if (choice == null || !targets.Remove(choice)) return;
            choice.PropertyChanged -= OverlaySelectionChanged;
            SaveTargets();
            RaisePropertyChanged(nameof(TargetSelection));
            RefreshOverlays();
        }

        private void SaveTargets() => settings.OverlayTargetsJson = JsonSerializer.Serialize(targets.Select(choice => choice.Target));

        private void OverlaySelectionChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(OverlayChoice.Enabled)) return;
            RaisePropertyChanged(nameof(PlanetSelection));
            RaisePropertyChanged(nameof(TargetSelection));
            RefreshOverlays();
        }

        private void PublishTracks(IReadOnlyList<OverlayTrack> tracks) {
            overlayTracks = tracks;
            RaisePropertyChanged(nameof(OverlayTracks));
            RaisePropertyChanged(nameof(OverlayLegend));
        }

        private void RefreshOverlays() {
            int revision = Interlocked.Increment(ref overlayRevision);
            if (overlayTracks.Count > 0) PublishTracks(Array.Empty<OverlayTrack>());
            OverlayNotice = string.Empty;
            var row = selectedNight;
            if (row == null || row.Payload.SunSeries.Count == 0) return;
            var selected = Planets.Concat(Targets).Where(choice => choice.Enabled).ToArray();
            if (selected.Length == 0) return;

            var first = row.Payload.FrameNoon;
            var last = row.Chart.TimeAtMinutes(row.Chart.FrameMinutes);
            var lastSunSample = row.Payload.SunSeries[^1].Time;
            if (lastSunSample < last) last = lastSunSample;
            _ = Task.Run(async () => {
                // Keep N.I.N.A.'s native astrometry calls sequential across overlapping requests.
                await overlayWorker.WaitAsync().ConfigureAwait(false);
                var tracks = new List<OverlayTrack>(selected.Length);
                var failed = new List<string>();
                try {
                    foreach (var choice in selected) {
                        if (revision != Volatile.Read(ref overlayRevision)) return;
                        try {
                            var samples = new List<(DateTimeOffset, double)>();
                            var phases = new Dictionary<DateTimeOffset, BodyIllumination>();
                            for (var time = first; ; time = time.AddMinutes(NightCalculator.SunGridMin)) {
                                if (revision != Volatile.Read(ref overlayRevision)) return;
                                var sampleTime = time < last ? time : last;
                                var altitude = choice.Planet is { } planet
                                    ? OverlayEphemeris.PlanetAltitude(planet, row.Payload.Location, sampleTime)
                                    : OverlayEphemeris.TargetAltitude(choice.Target, row.Payload.Location, sampleTime);
                                if (!double.IsFinite(altitude)) throw new InvalidOperationException("Altitude is unavailable.");
                                samples.Add((sampleTime, altitude));
                                // Scrubbing snaps to a whole hour. Cache the appearance at those exact instants.
                                if (sampleTime == time && (sampleTime - first).Ticks % TimeSpan.TicksPerHour == 0
                                    && choice.Planet is { } phaseBody
                                    && OverlayEphemeris.Phase(phaseBody, row.Payload.Location, sampleTime) is { } phase)
                                    phases.Add(sampleTime, phase);
                                if (sampleTime == last) break;
                            }
                            var polyline = NightChartVM.TrimmedPolyline(row.Payload, samples, NightChartVM.YSun, row.Chart.FrameMinutes);
                            tracks.Add(new OverlayTrack(choice, samples, polyline, phases));
                        } catch (Exception ex) {
                            Logger.Error($"Unphased could not plot {choice.Name}", ex);
                            failed.Add(choice.Name);
                        }
                    }
                } finally {
                    overlayWorker.Release();
                }
                if (revision != Volatile.Read(ref overlayRevision)) return;
                OnUi(() => {
                    if (revision != Volatile.Read(ref overlayRevision)) return;
                    PublishTracks(tracks);
                    if (failed.Count > 0) OverlayNotice = $"Could not plot {string.Join(", ", failed)}.";
                });
            });
        }
    }
}
