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
        Default = SunAltitude | MoonAltitude | Sunrise | Sunset | Moonrise | Moonset | SunGlyph | MoonGlyph
    }

    public sealed class ChartElementToggle : BaseINPC {
        private bool enabled;

        internal ChartElementToggle(ChartElements element, string name, string toolTip = null) {
            Element = element;
            Name = name;
            ToolTip = toolTip;
            enabled = ChartElements.Default.HasFlag(element);
        }

        internal ChartElements Element { get; }
        public string Name { get; }
        public string ToolTip { get; }
        public bool Enabled {
            get => enabled;
            set {
                if (enabled == value) return;
                enabled = value;
                RaisePropertyChanged();
            }
        }
    }

    public sealed partial class NightPlannerVM {
        private IRelayCommand resetChartElements;

        public IReadOnlyList<ChartElementToggle> ChartElementOptions { get; } = new ChartElementToggle[] {
            new(ChartElements.SunAltitude, "Sun altitude"),
            new(ChartElements.MoonAltitude, "Moon altitude"),
            new(ChartElements.Sunrise, "Sunrise"),
            new(ChartElements.Sunset, "Sunset"),
            new(ChartElements.Moonrise, "Moonrise"),
            new(ChartElements.Moonset, "Moonset"),
            new(ChartElements.SunGlyph, "Sun glyph", "Show the Sun at the selected time on the scrub line"),
            new(ChartElements.MoonGlyph, "Moon glyph", "Show the Moon at the selected time on the scrub line")
        };

        internal ChartElements VisibleChartElements => ChartElementOptions
            .Where(option => option.Enabled)
            .Aggregate(ChartElements.None, (elements, option) => elements | option.Element);

        public IRelayCommand ResetChartElementsCommand => resetChartElements ??= new RelayCommand(() => {
            foreach (var option in ChartElementOptions)
                option.Enabled = ChartElements.Default.HasFlag(option.Element);
        });

        private void InitializeChartElements() {
            foreach (var option in ChartElementOptions)
                option.PropertyChanged += (_, _) => RaisePropertyChanged(nameof(VisibleChartElements));
        }
    }
}
