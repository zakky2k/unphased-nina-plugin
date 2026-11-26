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
        Default = SunAltitude | MoonAltitude | Sunrise | Sunset | Moonrise | Moonset | SunGlyph | MoonGlyph
            | PlanetAltitude | TargetAltitude | PlanetGlyph | TargetGlyph | SunLabel | MoonLabel | PlanetLabel | TargetLabel
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

    public sealed partial class NightPlannerVM {
        private IRelayCommand resetChartElements;

        public IReadOnlyList<ChartElementGroup> ChartElementGroups { get; } = new[] {
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

        private static ChartElementGroup Group(string name, ChartElements curve, ChartElements glyph, ChartElements label) => new(
            name, new(curve, $"{name} altitude curves"), new(glyph, $"{name} scrub glyphs"), new(label, $"{name} scrub name labels"));

        internal IReadOnlyList<ChartElementToggle> ChartElementOptions { get; private set; }

        internal ChartElements VisibleChartElements { get; private set; } = ChartElements.Default;

        public IRelayCommand ResetChartElementsCommand => resetChartElements ??= new RelayCommand(() => {
            foreach (var option in ChartElementOptions)
                option.Enabled = ChartElements.Default.HasFlag(option.Element);
            ShowCustomHorizon = false;
        });

        private void InitializeChartElements() {
            ChartElementOptions = ChartElementGroups.SelectMany(group => new[] { group.Curve, group.Glyph, group.Label })
                .Concat(RiseSetOptions).ToArray();
            foreach (var option in ChartElementOptions)
                option.PropertyChanged += (_, _) => {
                    VisibleChartElements = option.Enabled
                        ? VisibleChartElements | option.Element
                        : VisibleChartElements & ~option.Element;
                    RaisePropertyChanged(nameof(VisibleChartElements));
                    if (option.Element is ChartElements.PlanetAltitude or ChartElements.TargetAltitude)
                        RaisePropertyChanged(nameof(OverlayLegend));
                };
        }
    }
}
