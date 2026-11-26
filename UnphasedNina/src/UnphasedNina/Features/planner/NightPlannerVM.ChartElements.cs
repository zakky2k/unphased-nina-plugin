// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;
using BaseINPC = NINA.Core.Utility.BaseINPC;

namespace Zakfo.Unphased.Features.planner {
    [Flags]
    internal enum ChartElements {
        None = 0,
        SunAltitude = 1 << 0,
        MoonAltitude = 1 << 1,
        Sunrise = 1 << 2,
        Sunset = 1 << 3,
        Moonrise = 1 << 4,
        Moonset = 1 << 5,
        SunGlyph = 1 << 6,
        MoonGlyph = 1 << 7,
        PlanetAltitude = 1 << 8,
        TargetAltitude = 1 << 9,
        PlanetGlyph = 1 << 10,
        TargetGlyph = 1 << 11,
        SunLabel = 1 << 12,
        MoonLabel = 1 << 13,
        PlanetLabel = 1 << 14,
        TargetLabel = 1 << 15,
        CustomHorizon = 1 << 16,
        Default = SunAltitude | MoonAltitude | Sunrise | Sunset | Moonrise | Moonset | SunGlyph | MoonGlyph
            | PlanetAltitude | TargetAltitude | PlanetGlyph | TargetGlyph | SunLabel | MoonLabel | PlanetLabel | TargetLabel,
        All = Default | CustomHorizon
    }

    public sealed class ChartElementToggle : BaseINPC {
        private bool enabled;

        internal ChartElementToggle(ChartElements element, string name) {
            Element = element;
            Name = name;
            enabled = ChartElements.Default.HasFlag(element);
        }

        internal ChartElements Element { get; }
        public string Name { get; }
        public bool Enabled {
            get => enabled;
            set {
                if (enabled == value) return;
                enabled = value;
                RaisePropertyChanged();
            }
        }
    }

    public sealed record ChartElementGroup(string Name, ChartElementToggle Curve, ChartElementToggle Glyph, ChartElementToggle Label);

    public sealed class ChartElementPreset : BaseINPC {
        private ChartElements value = ChartElements.Default;
        private bool applyingPreset;

        public IReadOnlyList<ChartElementGroup> Groups { get; } = new[] {
            Group("Sun", ChartElements.SunAltitude, ChartElements.SunGlyph, ChartElements.SunLabel),
            Group("Moon", ChartElements.MoonAltitude, ChartElements.MoonGlyph, ChartElements.MoonLabel),
            Group("Planets", ChartElements.PlanetAltitude, ChartElements.PlanetGlyph, ChartElements.PlanetLabel),
            Group("Deep sky", ChartElements.TargetAltitude, ChartElements.TargetGlyph, ChartElements.TargetLabel)
        };

        public IReadOnlyList<ChartElementToggle> RiseSetOptions { get; } = new ChartElementToggle[] {
            new(ChartElements.Sunrise, "Sunrise"),
            new(ChartElements.Sunset, "Sunset"),
            new(ChartElements.Moonrise, "Moonrise"),
            new(ChartElements.Moonset, "Moonset")
        };

        public ChartElementToggle CustomHorizon { get; } = new(ChartElements.CustomHorizon, "Custom horizon");

        internal IReadOnlyList<ChartElementToggle> Options { get; }

        public ChartElementPreset() {
            Options = Groups.SelectMany(group => new[] { group.Curve, group.Glyph, group.Label })
                .Concat(RiseSetOptions).Append(CustomHorizon).ToArray();
            foreach (var option in Options)
                option.PropertyChanged += (_, _) => {
                    if (applyingPreset) return;
                    value = option.Enabled ? value | option.Element : value & ~option.Element;
                    RaisePropertyChanged(nameof(Value));
                };
        }

        internal ChartElements Value {
            get => value;
            set {
                var flags = value & ChartElements.All;
                if (this.value == flags) return;
                this.value = flags;
                applyingPreset = true;
                try {
                    foreach (var option in Options) option.Enabled = flags.HasFlag(option.Element);
                } finally {
                    applyingPreset = false;
                }
                RaisePropertyChanged();
            }
        }

        private static ChartElementGroup Group(string name, ChartElements curve, ChartElements glyph, ChartElements label) => new(
            name, new(curve, $"{name} altitude curves"), new(glyph, $"{name} scrub glyphs"), new(label, $"{name} scrub name labels"));
    }

    public sealed partial class NightPlannerVM {
        public ChartElementPreset ChartVisibility { get; } = new();
        private IRelayCommand showAllChartElements;
        private IRelayCommand hideAllChartElements;
        private IRelayCommand showDefaultChartElements;

        internal ChartElements VisibleChartElements => ChartVisibility.Value;
        internal IReadOnlyList<ChartElementToggle> ChartElementOptions => ChartVisibility.Options;

        public IRelayCommand ShowAllChartElementsCommand =>
            showAllChartElements ??= new RelayCommand(() => ChartVisibility.Value = ChartElements.All);
        public IRelayCommand HideAllChartElementsCommand =>
            hideAllChartElements ??= new RelayCommand(() => ChartVisibility.Value = ChartElements.None);
        public IRelayCommand ShowDefaultChartElementsCommand =>
            showDefaultChartElements ??= new RelayCommand(() => ChartVisibility.Value = settings.ChartDefaults.Value);

        private void InitializeChartElements() {
            ChartVisibility.CustomHorizon.PropertyChanged += (_, _) => OnCustomHorizonToggled();
            var previous = VisibleChartElements;
            ChartVisibility.PropertyChanged += (_, _) => {
                var current = VisibleChartElements;
                var changed = current ^ previous;
                previous = current;
                RaisePropertyChanged(nameof(VisibleChartElements));
                if ((changed & (ChartElements.PlanetAltitude | ChartElements.TargetAltitude)) != 0)
                    RaisePropertyChanged(nameof(OverlayLegend));
            };
            ChartVisibility.Value = settings.ChartDefaults.Value;
        }
    }
}
