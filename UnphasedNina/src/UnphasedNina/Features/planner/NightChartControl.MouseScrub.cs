// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, you can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace Zakfo.Unphased.Features.planner {
    public partial class NightChartControl {
        private double? pendingScrubPixel;
        private DragMetrics dragMetrics;
        private DrawingGroup scrubBackground;
        private (double Width, double Height, double Dpi) scrubBackgroundSize;

        private MouseScrubMode MouseMode => hookedPlanner?.MouseScrub ?? MouseScrubMode.Smooth;

        internal static double MouseMinutes(double pixelX, double width, double spanMinutes, MouseScrubMode mode) {
            double minutes = Math.Clamp(pixelX / width, 0, 1) * spanMinutes;
            return mode == MouseScrubMode.Minute ? Math.Round(minutes, MidpointRounding.AwayFromZero) : minutes;
        }

        private void MoveMouseScrub(double pixelX, bool immediately = false) {
            if (dragMetrics != null) dragMetrics.Inputs++;
            if (immediately || MouseMode == MouseScrubMode.Minute) {
                ApplyMouseScrub(pixelX);
                return;
            }
            if (!pendingScrubPixel.HasValue) CompositionTarget.Rendering += FlushMouseScrub;
            pendingScrubPixel = pixelX;
        }

        private void ApplyMouseScrub(double pixelX) {
            if (row == null || ActualWidth <= 0) return;
            var before = scrubMinutes;
            double minutes = MouseMinutes(pixelX, ActualWidth, SpanMinutes, MouseMode);
            if (hookedPlanner != null) hookedPlanner.SetMouseScrub(minutes);
            else SetScrub(minutes);
            if (dragMetrics != null && before != scrubMinutes) dragMetrics.Updates++;
        }

        private void FlushMouseScrub(object sender, EventArgs args) {
            var pixel = pendingScrubPixel;
            CancelMouseScrub();
            if (pixel.HasValue && IsMouseCaptured) ApplyMouseScrub(pixel.Value);
        }

        private void CancelMouseScrub() {
            CompositionTarget.Rendering -= FlushMouseScrub;
            pendingScrubPixel = null;
        }

        private void FinishMouseScrub() {
            CancelMouseScrub();
            scrubBackground = null;
            var measurement = dragMetrics;
            var planner = hookedPlanner;
            if (measurement == null) return;
            var elapsed = Stopwatch.GetElapsedTime(measurement.Started);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                if (!ReferenceEquals(dragMetrics, measurement)) return;
                dragMetrics = null;
                if (planner?.MeasureScrub == true) planner.ReportScrubMetrics(measurement.Mode, measurement.Format(elapsed));
            }));
        }

        private sealed class DragMetrics(MouseScrubMode mode) {
            internal MouseScrubMode Mode { get; } = mode;
            internal long Started { get; } = Stopwatch.GetTimestamp();
            internal int Inputs { get; set; }
            internal int Updates { get; set; }
            private int renders;
            private double totalMs;
            private double peakMs;

            internal void RecordRender(TimeSpan time) {
                renders++;
                totalMs += time.TotalMilliseconds;
                peakMs = Math.Max(peakMs, time.TotalMilliseconds);
            }

            internal string Format(TimeSpan elapsed) {
                if (renders == 0) return $"{Mode} · {elapsed.TotalSeconds:0.00} s · {Inputs} inputs → {Updates} updates → 0 paints";
                return $"{Mode} · {elapsed.TotalSeconds:0.00} s · {Inputs} inputs → {Updates} updates → {renders} paints"
                    + $" · {totalMs:0.00} ms drawing ({totalMs / renders:0.00} avg / {peakMs:0.00} peak per paint)";
            }
        }
    }
}
