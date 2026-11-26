// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, you can obtain one at https://mozilla.org/MPL/2.0/.

namespace Zakfo.Unphased.Features.planner {
    public sealed partial class NightPlannerVM {
        private bool measureScrub;

        public bool MeasureScrub {
            get => measureScrub;
            set {
                if (measureScrub == value) return;
                measureScrub = value;
                RaisePropertyChanged();
            }
        }

        public string ScrubMetrics { get; private set; } = "Drag on the chart to measure drawing time.";

        internal void ReportScrubMetrics(string report) {
            ScrubMetrics = report;
            RaisePropertyChanged(nameof(ScrubMetrics));
        }

        internal void SetMouseScrub(double minutes) => MoveScrub(minutes);
    }
}
