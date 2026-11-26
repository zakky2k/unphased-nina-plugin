// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>Displays the chart glyph beside an enabled overlay switch.</summary>
    public sealed class OverlayGlyphPreview : FrameworkElement {
        private static readonly PlannerPalette Palette = new();

        public static readonly DependencyProperty PlannerProperty = DependencyProperty.Register(
            nameof(Planner), typeof(NightPlannerVM), typeof(OverlayGlyphPreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPlannerChanged));

        public NightPlannerVM Planner {
            get => (NightPlannerVM)GetValue(PlannerProperty);
            set => SetValue(PlannerProperty, value);
        }

        public OverlayGlyphPreview() {
            DataContextChanged += (_, _) => InvalidateVisual();
        }

        private static void OnPlannerChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var preview = (OverlayGlyphPreview)sender;
            if (args.OldValue is NightPlannerVM previous)
                PropertyChangedEventManager.RemoveHandler(previous, preview.OnPlannerPropertyChanged, string.Empty);
            if (args.NewValue is NightPlannerVM current)
                PropertyChangedEventManager.AddHandler(current, preview.OnPlannerPropertyChanged, string.Empty);
        }

        private void OnPlannerPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName is nameof(NightPlannerVM.GlyphStyle) or nameof(NightPlannerVM.Scheme)
                or nameof(NightPlannerVM.OverlayTracks) or nameof(NightPlannerVM.ScrubMinutes))
                InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc) {
            var planner = Planner;
            if (planner == null || DataContext is not OverlayChoice { Enabled: true } choice) return;
            var style = planner.GlyphStyle;
            var scheme = planner.Scheme;
            var centre = new Point(ActualWidth / 2, ActualHeight / 2);
            var ink = OverlayStyle.BrushFor(choice.StyleIndex, scheme);
            var background = Palette.ChipFill(scheme);
            if (choice.Planet is not { } body) {
                DeepSkyGlyph.Draw(dc, choice.Target.ObjectType, centre, DeepSkyGlyph.Radius, ink, background);
                return;
            }
            double radius = NightChartControl.GlyphRadius(style, body);
            NightChartControl.DrawPlanetGlyph(dc, body, centre, radius, ink, background, style,
                planner.PlanetPreviewPhase(choice), scheme == ChartScheme.Night,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }
    }
}
