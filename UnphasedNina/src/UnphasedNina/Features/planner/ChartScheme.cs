namespace Zakfo.Unphased.Features.planner {
    /// <summary>
    /// Which palette the planner paints with. Exactly one is active at a time, so the
    /// header offers three buttons rather than a toggle: the old single night switch
    /// could only say red-or-theme, and a third, colour-blind-friendly palette had
    /// nowhere to go.
    ///
    /// <see cref="Default"/> follows whatever theme the user picked in N.I.N.A.;
    /// <see cref="Night"/> is the red night-vision set (chart and this panel's chrome);
    /// <see cref="Enhanced"/> is the plugin's own navy chart - sun amber, moon pale blue,
    /// horizon teal - while the panel around it stays native N.I.N.A., because a third
    /// chrome dictionary would recolour the panel without making it look like the rest of
    /// the application.
    ///
    /// The active scheme is not persisted: it is a viewing preference for the session, and
    /// a chart that opens in red at 2am when the room light is on is a trap. A settings
    /// file written by an older build carries the old <c>darkMode</c> flag, which reads
    /// across as <see cref="Night"/> once and is never written again.
    /// </summary>
    public enum ChartScheme {
        /// <summary>The live N.I.N.A. theme palette.</summary>
        Default,
        /// <summary>Red night vision, chart plus this panel's chrome.</summary>
        Night,
        /// <summary>Navy chart with the sky roles separated by hue; native panel chrome.</summary>
        Enhanced,
    }
}
