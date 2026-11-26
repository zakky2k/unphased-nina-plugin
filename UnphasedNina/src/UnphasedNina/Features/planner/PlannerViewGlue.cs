using NINA.Core.Utility;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zakfo.Unphased.Features.settings;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// Small code-behind glue for the panel template: feeds the selected night
    /// row into the chart control and triggers the first computation when the
    /// panel becomes visible. No custom controls, no global styles.
    /// </summary>
    public static class PlannerViewGlue {
        public static readonly DependencyProperty ChartProperty =
            DependencyProperty.RegisterAttached("Chart", typeof(NightChartControl), typeof(PlannerViewGlue),
                new PropertyMetadata(null, OnChartChanged));

        public static NightChartControl GetChart(DependencyObject d) => (NightChartControl)d.GetValue(ChartProperty);
        public static void SetChart(DependencyObject d, NightChartControl v) => d.SetValue(ChartProperty, v);

        private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            // The host is whatever element carries the attached property - the header date
            // picker, since the night list it used to hang off is gone. Nothing about the
            // wiring is list-specific: the view model owns the selection, so the picker, the
            // header caption and the chart all follow one value.
            if (d is not FrameworkElement host || e.NewValue is not NightChartControl chart) return;
            var planner = FindPlanner(host.DataContext);
            if (planner == null) return;

            planner.PropertyChanged += (_, args) => {
                if (args.PropertyName == nameof(NightPlannerVM.SelectedNight))
                    chart.SetRow(planner.SelectedNight);
            };
            chart.SetRow(planner.SelectedNight);

            // Kick off the first computation the first time the panel loads. Since Refresh
            // went away this and the calendar are the only ways a window is ever asked for,
            // and a panel that already has nights - a docked pane being reloaded - is left
            // alone rather than recomputed.
            host.Loaded += (_, _) => {
                if (planner.Nights.Count == 0) planner.EnsureComputed();
            };
        }

        private static NightPlannerVM FindPlanner(object dc) => dc switch {
            NightPlannerVM p => p,
            PlannerDockableVM d => d.Planner,
            _ => null
        };

        /// <summary>
        /// Handle the arrow keys for the whole panel instead of for the chart alone.
        /// <para>
        /// The chart used to take keys in OnKeyDown, a **bubbling** event, so it only ever
        /// saw them while it held keyboard focus - and nothing but a click on the chart ever
        /// gives it focus. Up/Down had no case at all there, so the event stayed unhandled
        /// and WPF's directional navigation moved focus down into the night list, which
        /// then ate Left/Right as its own selector navigation: after one press of Down the
        /// scrub was dead until the user thought to click the chart again.
        /// </para>
        /// <para>
        /// PreviewKeyDown is a tunneling event, so the panel root sees every key press aimed
        /// at anything inside it before the child does, and handling it here also stops the
        /// directional navigation that moved the focus in the first place. A field the user
        /// is typing into keeps its keys: Left/Right/Up/Down inside a TextBox (including the
        /// text part of the date picker) belong to the caret and the picker, and their popup
        /// lives in its own visual tree, which this route never reaches.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty KeyboardScopeProperty =
            DependencyProperty.RegisterAttached("KeyboardScope", typeof(bool), typeof(PlannerViewGlue),
                new PropertyMetadata(false, OnKeyboardScopeChanged));

        public static bool GetKeyboardScope(DependencyObject d) => (bool)d.GetValue(KeyboardScopeProperty);
        public static void SetKeyboardScope(DependencyObject d, bool value) => d.SetValue(KeyboardScopeProperty, value);

        private static void OnKeyboardScopeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not UIElement element) return;
            element.PreviewKeyDown -= OnPanelPreviewKeyDown;
            if ((bool)e.NewValue) element.PreviewKeyDown += OnPanelPreviewKeyDown;
        }

        /// <summary>
        /// One key map for the panel: left/right step the scrub, up/down change night,
        /// Home/End jump to the frame edges, Escape/Delete take the marker off. Raised
        /// through the routed event, so the test exercises the registration and not a
        /// private helper.
        /// </summary>
        private static void OnPanelPreviewKeyDown(object sender, KeyEventArgs e) {
            if (sender is not FrameworkElement root) return;
            var planner = FindPlanner(root.DataContext);
            if (planner == null || planner.Nights.Count == 0) return;
            if (IsTextEntry(e.OriginalSource) || IsTextEntry(e.Source) || IsTextEntry(Keyboard.FocusedElement)) return;
            switch (e.Key) {
                case Key.Left: planner.StepScrub(-1); break;
                case Key.Right: planner.StepScrub(1); break;
                case Key.Up: planner.PrevNight(); break;
                case Key.Down: planner.NextNight(); break;
                case Key.Home: planner.SetScrub(0); break;
                case Key.End: planner.SetScrub(planner.VisibleSpanMinutes); break;
                case Key.Escape:
                case Key.Delete: planner.ClearScrub(); break;
                default: return;
            }
            // Handled, so WPF's directional navigation never moves keyboard focus out of
            // the scrub control - which is the whole reason the keys used to stop working.
            e.Handled = true;
        }

        /// <summary>
        /// Whether a key press aimed at this element is typing. A DatePicker's editable
        /// part is a TextBox (DatePickerTextBox), so the caret and the picker's own keys are
        /// covered by the same test; the ancestor walk catches a control that hosts one.
        /// </summary>
        private static bool IsTextEntry(object source) {
            for (var node = source as DependencyObject; node != null;) {
                if (node is TextBox || node is PasswordBox) return true;
                node = node is Visual
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return false;
        }

        /// <summary>
        /// Scope the red night-vision palette to one element's subtree. N.I.N.A.'s styles
        /// resolve their colours through DynamicResource, and a DynamicResource lookup
        /// walks outwards from the element asking each ancestor in turn, so brushes held
        /// in this panel's own resource dictionary win for this panel alone. The imaging
        /// tab, every other tab and the rest of the application keep the colours the user
        /// picked in N.I.N.A. - the plugin never edits the application theme.
        ///
        /// It takes the whole scheme rather than a bool, because the panel needs to know
        /// which palette is on, not merely that one is: Night installs the chrome,
        /// Enhanced installs nothing on purpose - it is a chart-only palette, and a panel
        /// that went navy while every other tab stayed the user's colours would look
        /// broken rather than themed.
        /// </summary>
        public static readonly DependencyProperty NightPaletteProperty =
            DependencyProperty.RegisterAttached("NightPalette", typeof(ChartScheme), typeof(PlannerViewGlue),
                new PropertyMetadata(ChartScheme.Default, OnNightPaletteChanged));

        public static ChartScheme GetNightPalette(DependencyObject d) => (ChartScheme)d.GetValue(NightPaletteProperty);
        public static void SetNightPalette(DependencyObject d, ChartScheme value) => d.SetValue(NightPaletteProperty, value);

        private static void OnNightPaletteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not FrameworkElement element) return;
            // Resources is an inherited property, so reading it hands back the nearest
            // ancestor's dictionary - writing into that would recolour everything above
            // this panel. The panel therefore gets a dictionary of its own. WPF looks a
            // resource key up by walking the element tree, so every key this dictionary
            // does not define still resolves to the user's N.I.N.A. theme; any scheme other
            // than Night simply installs an empty one. (The panel template defines no local
            // resources of its own, so replacing the dictionary here gives nothing away.)
            var palette = new ResourceDictionary();
            if ((ChartScheme)e.NewValue == ChartScheme.Night) {
                foreach (var entry in PlannerPalette.NightChrome) palette[entry.Key] = entry.Value;
            }
            element.Resources = palette;
        }
    }
}
