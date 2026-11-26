// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, you can obtain one at https://mozilla.org/MPL/2.0/.

namespace Zakfo.Unphased.Features.planner {
    public enum MouseScrubMode { Smooth, Minute }

    public sealed partial class NightPlannerVM {
        private MouseScrubMode mouseScrub;
        private bool measureScrub;
        private string smoothMetrics = string.Empty;
        private string minuteMetrics = string.Empty;

        public IReadOnlyList<MouseScrubMode> MouseScrubModes { get; } = Enum.GetValues<MouseScrubMode>();

        public MouseScrubMode MouseScrub {
            get => mouseScrub;
            set {
                if (mouseScrub == value) return;
                mouseScrub = value;
                RaisePropertyChanged();
            }
        }

        public bool MeasureScrub {
            get => measureScrub;
            set {
                if (measureScrub == value) return;
                measureScrub = value;
                RaisePropertyChanged();
            }
        }

        public string ScrubMetrics => string.Join("\n", new[] { smoothMetrics, minuteMetrics }.Where(line => line.Length > 0)
            .DefaultIfEmpty("Drag on the chart to measure each mode."));

        internal void ReportScrubMetrics(MouseScrubMode mode, string report) {
            if (mode == MouseScrubMode.Smooth) smoothMetrics = report;
            else minuteMetrics = report;
            RaisePropertyChanged(nameof(ScrubMetrics));
        }

        internal void SetMouseScrub(double minutes) => MoveScrub(minutes);
    }
}
