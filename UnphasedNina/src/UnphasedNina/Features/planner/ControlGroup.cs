// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Windows;
using System.Windows.Controls;

namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// One labelled cluster of controls with a readout underneath: a small caption, a row of
    /// buttons, and a thin outlined box holding the dynamic status for that cluster. The panel
    /// header is four of these side by side, so "which buttons belong to which readout" is
    /// carried by the shape rather than by counting rows.
    ///
    /// It is a control rather than four copies of the same markup for one reason: the grammar is
    /// the point. Every cluster has to have the same caption height, the same gap to the buttons,
    /// and a status box that stretches to exactly the width of the buttons above it. Written four
    /// times by hand, those numbers drift the moment someone adds a fifth cluster.
    ///
    /// The buttons themselves live in Content, supplied by the panel template, and this control
    /// never sets a Style or a Template on them. N.I.N.A. styles its own buttons through an
    /// implicit style keyed by the control type, and a plugin template that puts a style on a
    /// Button replaces that chrome with whatever the plugin wrote: the buttons lose their border
    /// and go flat. So the symmetry lives in the container - caption, row, outlined box - and the
    /// buttons keep the chrome N.I.N.A. gave them, sized by attributes on each one.
    ///
    /// Its own look is one ControlTemplate in Features/planner/Resources.xaml, keyed to this type,
    /// which also means the night palette applies to it like anything else in the panel: every
    /// brush in the template is a DynamicResource on a N.I.N.A. key.
    /// </summary>
    public class ControlGroup : HeaderedContentControl {
        static ControlGroup() =>
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ControlGroup), new FrameworkPropertyMetadata(typeof(ControlGroup)));

        /// <summary>
        /// The text in the outlined box under the controls - what that cluster currently says,
        /// not a description of what the buttons do. Kept separate from Header so a cluster can
        /// say "1 night" without the caption having to become a sentence.
        /// </summary>
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(ControlGroup), new PropertyMetadata(string.Empty));

        public string Status {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }
    }
}
